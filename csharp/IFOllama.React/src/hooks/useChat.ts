import { useState, useCallback, useRef, useEffect } from 'react';
import { useAuth } from '@if/web-common-react';
import type { ISubscription } from '@microsoft/signalr';
import { chatService } from '../services/chatService';
import { apiService } from '../services/apiService';
import type { Message } from '../types';

interface UseChatOptions {
  model?: string;
  enabledTools?: string[];
  onTitleGenerated?: () => void;
}

interface UseChatResult {
  messages: Message[];
  isLoading: boolean;
  isConnected: boolean;
  error: string | null;
  conversationId: string | null;
  sendMessage: (content: string) => Promise<void>;
  setConversationId: (id: string | null) => void;
  clearMessages: () => void;
  loadConversation: (id: string) => Promise<void>;
  cancelStream: () => void;
}

export function useChat(options: UseChatOptions = {}): UseChatResult {
  const { model = 'qwen3-coder:latest', enabledTools = [], onTitleGenerated } = options;
  const auth = useAuth();

  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conversationId, setConversationId] = useState<string | null>(null);

  const responseBufferRef = useRef('');
  const thinkBufferRef = useRef('');
  const insideThinkRef = useRef(false);
  const subscriptionRef = useRef<ISubscription<string> | null>(null);
  const isNewConversationRef = useRef(false);
  const firstMessageRef = useRef<string | null>(null);

  // Cleanup subscription on unmount
  useEffect(() => {
    return () => {
      if (subscriptionRef.current) {
        subscriptionRef.current.dispose();
        subscriptionRef.current = null;
      }
    };
  }, []);

  // Connect to SignalR when authenticated
  useEffect(() => {
    const connect = async () => {
      if (auth.isAuthenticated) {
        try {
          const token = await auth.getAccessToken();
          if (token) {
            apiService.setAccessToken(token);
            await chatService.connect(token);
            setIsConnected(true);
            setError(null);
          }
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Failed to connect';
          setError(message);
          setIsConnected(false);
        }
      }
    };

    connect();

    return () => {
      if (subscriptionRef.current) {
        subscriptionRef.current.dispose();
        subscriptionRef.current = null;
      }
      chatService.disconnect();
      setIsConnected(false);
    };
  }, [auth.isAuthenticated]);

  const cancelStream = useCallback(() => {
    if (subscriptionRef.current) {
      subscriptionRef.current.dispose();
      subscriptionRef.current = null;
      setIsLoading(false);
    }
  }, []);

  const getUserId = (): string | undefined => {
    return auth.user?.profile?.sub;
  };

  const loadConversation = useCallback(async (id: string) => {
    const userId = getUserId();
    if (!userId) return;

    try {
      const msgs = await apiService.getMessages(id, userId);
      setMessages(msgs.map(m => ({
        ...m,
        id: crypto.randomUUID(),
        timestamp: new Date(m.timestamp),
      })));
      setConversationId(id);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load conversation';
      setError(message);
    }
  }, [auth.user]);

  const clearMessages = useCallback(() => {
    setMessages([]);
    setConversationId(null);
    setError(null);
  }, []);

  const sendMessage = useCallback(async (content: string) => {
    const userId = getUserId();
    if (!content.trim() || !userId) return;

    // Refresh token before sending
    try {
      const token = await auth.getAccessToken();
      if (token) {
        apiService.setAccessToken(token);
      }
    } catch {
      // Token refresh failed, continue with existing token
    }

    // Create conversation if needed
    let currentConversationId = conversationId;
    if (!currentConversationId) {
      try {
        const title = content.slice(0, 50) + (content.length > 50 ? '...' : '');
        const conv = await apiService.createConversation(title, userId);
        currentConversationId = conv.id;
        setConversationId(currentConversationId);
        isNewConversationRef.current = true;
        firstMessageRef.current = content;
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to create conversation';
        setError(message);
        return;
      }
    }

    // Add user message
    const userMessage: Message = {
      id: crypto.randomUUID(),
      role: 'user',
      content,
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setIsLoading(true);
    setError(null);

    // Save user message
    try {
      await apiService.appendMessage(currentConversationId, userMessage, userId);
    } catch (err) {
      console.error('Failed to save user message:', err);
    }

    // Prepare assistant message
    const assistantMessage: Message = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      showThinking: false,
    };

    setMessages(prev => [...prev, assistantMessage]);

    responseBufferRef.current = '';
    thinkBufferRef.current = '';
    insideThinkRef.current = false;

    // Build history for the model
    const history = messages.map(m => `${m.role}:${m.content}`);
    history.push(`user:${content}`);

    // Cancel any existing subscription
    if (subscriptionRef.current) {
      subscriptionRef.current.dispose();
    }

    subscriptionRef.current = chatService.streamChat(
      model,
      history,
      currentConversationId,
      userId,
      enabledTools.length > 0 ? enabledTools : null,
      // onToken
      (token: string) => {
        let processedToken = token;

        // Handle <think> tags
        if (processedToken.includes('<think>')) {
          insideThinkRef.current = true;
          processedToken = processedToken.replace('<think>', '');
        }
        if (processedToken.includes('</think>')) {
          insideThinkRef.current = false;
          processedToken = processedToken.replace('</think>', '');
        }

        if (insideThinkRef.current) {
          thinkBufferRef.current += processedToken;
        } else {
          responseBufferRef.current += processedToken;
        }

        setMessages(prev => {
          const updated = [...prev];
          const lastIdx = updated.length - 1;
          if (lastIdx >= 0 && updated[lastIdx].role === 'assistant') {
            updated[lastIdx] = {
              ...updated[lastIdx],
              content: responseBufferRef.current,
              thinkContent: thinkBufferRef.current || undefined,
            };
          }
          return updated;
        });
      },
      // onComplete
      () => {
        subscriptionRef.current = null;
        setIsLoading(false);

        // Generate AI title for new conversations
        if (isNewConversationRef.current && firstMessageRef.current && currentConversationId) {
          isNewConversationRef.current = false;
          const msg = firstMessageRef.current;
          firstMessageRef.current = null;

          apiService.generateTitle(currentConversationId, msg, userId)
            .then(() => {
              onTitleGenerated?.();
            })
            .catch((err) => {
              console.error('Failed to generate title:', err);
            });
        }
      },
      // onError
      (errorMsg: string) => {
        subscriptionRef.current = null;
        setError(errorMsg);
        setIsLoading(false);
        setMessages(prev => {
          const updated = [...prev];
          const lastIdx = updated.length - 1;
          if (lastIdx >= 0 && updated[lastIdx].role === 'assistant') {
            updated[lastIdx] = {
              ...updated[lastIdx],
              content: `⚠️ Error: ${errorMsg}`,
            };
          }
          return updated;
        });
      }
    );
  }, [auth, conversationId, messages, model, enabledTools, onTitleGenerated]);

  return {
    messages,
    isLoading,
    isConnected,
    error,
    conversationId,
    sendMessage,
    setConversationId,
    clearMessages,
    loadConversation,
    cancelStream,
  };
}
