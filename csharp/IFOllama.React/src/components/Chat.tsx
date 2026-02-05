import { useState, useRef, useEffect, type FormEvent, type KeyboardEvent } from 'react';
import { useChat } from '../hooks/useChat';
import { ChatMessage } from './ChatMessage';
import { ThinkingProgress } from './ThinkingProgress';
import { ToolSelector } from './ToolSelector';
import './Chat.scss';

const logo = new URL('/IF-Logo.png', import.meta.url).href;

interface ChatProps {
  initialConversationId?: string;
  onConversationCreated?: (id: string) => void;
}

export function Chat({ initialConversationId, onConversationCreated }: ChatProps) {
  const [prompt, setPrompt] = useState('');
  const [enabledTools, setEnabledTools] = useState<string[]>([]);
  const [showToolSelector, setShowToolSelector] = useState(false);
  const chatContainerRef = useRef<HTMLDivElement>(null);

  const {
    messages,
    isLoading,
    isConnected,
    error,
    conversationId,
    sendMessage,
    loadConversation,
  } = useChat({ enabledTools });

  // Load initial conversation if provided
  useEffect(() => {
    if (initialConversationId) {
      loadConversation(initialConversationId);
    }
  }, [initialConversationId, loadConversation]);

  // Notify parent when a new conversation is created
  useEffect(() => {
    if (conversationId && conversationId !== initialConversationId) {
      onConversationCreated?.(conversationId);
    }
  }, [conversationId]);

  // Auto-scroll to bottom
  useEffect(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, [messages]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!prompt.trim() || isLoading) return;

    const messageText = prompt;
    setPrompt('');
    await sendMessage(messageText);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e as unknown as FormEvent);
    }
  };

  const handleToolsChange = (tools: string[]) => {
    setEnabledTools(tools);
  };

  return (
    <div className="chat-container">
      <ThinkingProgress active={isLoading} />

      <header className="if-header">
        <div className="if-header-left">
          <img src={logo} alt="IF" className="if-logo-sm" />
          <h1 className="if-app-title">IFOllama</h1>
          <span className={`connection-status ${isConnected ? 'connected' : 'disconnected'}`}>
            {isConnected ? '● Connected' : '○ Disconnected'}
          </span>
        </div>
        <div className="if-header-right">
          <button
            className="if-btn if-btn-secondary"
            onClick={() => setShowToolSelector(!showToolSelector)}
          >
            🔧 Tools ({enabledTools.length})
          </button>
        </div>
      </header>

      {showToolSelector && (
        <ToolSelector
          enabledTools={enabledTools}
          onToolsChange={handleToolsChange}
          onClose={() => setShowToolSelector(false)}
        />
      )}

      {error && (
        <div className="error-banner">
          ⚠️ {error}
        </div>
      )}

      <div className="messages-container" ref={chatContainerRef}>
        {messages.length === 0 ? (
          <div className="empty-state">
            <img src={logo} alt="IF" className="empty-state-logo" />
            <h2>Welcome to IFOllama</h2>
            <p>Start a conversation by typing a message below.</p>
            {enabledTools.length > 0 && (
              <p className="tools-hint">
                🔧 {enabledTools.length} tool(s) enabled for this conversation
              </p>
            )}
          </div>
        ) : (
          messages.map((message) => (
            <ChatMessage key={message.id} message={message} />
          ))
        )}
      </div>

      <form className="input-form" onSubmit={handleSubmit}>
        <textarea
          className="if-form-input prompt-input"
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={isConnected ? "Type your message... (Enter to send, Shift+Enter for new line)" : "Connecting..."}
          disabled={!isConnected || isLoading}
          rows={3}
        />
        <button
          type="submit"
          className="if-btn if-btn-primary send-button"
          disabled={!isConnected || isLoading || !prompt.trim()}
        >
          {isLoading ? 'Sending...' : 'Send'}
        </button>
      </form>

      {conversationId && (
        <div className="conversation-id">
          Conversation: {conversationId.slice(0, 8)}...
        </div>
      )}
    </div>
  );
}
