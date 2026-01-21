import { useState, useRef, useEffect, type FormEvent, type KeyboardEvent } from 'react';
import { useChat } from '../hooks/useChat';
import { ChatMessage } from './ChatMessage';
import { ThinkingProgress } from './ThinkingProgress';
import { ToolSelector } from './ToolSelector';
import './Chat.scss';

interface ChatProps {
  initialConversationId?: string;
}

export function Chat({ initialConversationId }: ChatProps) {
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
    clearMessages,
    loadConversation,
  } = useChat({ enabledTools });

  // Load initial conversation if provided
  useEffect(() => {
    if (initialConversationId) {
      loadConversation(initialConversationId);
    }
  }, [initialConversationId, loadConversation]);

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

      <div className="chat-header">
        <div className="header-left">
          <h1>IFOllama</h1>
          <span className={`connection-status ${isConnected ? 'connected' : 'disconnected'}`}>
            {isConnected ? '● Connected' : '○ Disconnected'}
          </span>
        </div>
        <div className="header-right">
          <button
            className="button button-secondary"
            onClick={() => setShowToolSelector(!showToolSelector)}
          >
            🔧 Tools ({enabledTools.length})
          </button>
          <button className="button button-secondary" onClick={clearMessages}>
            New Chat
          </button>
        </div>
      </div>

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
          className="prompt-input"
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={isConnected ? "Type your message... (Enter to send, Shift+Enter for new line)" : "Connecting..."}
          disabled={!isConnected || isLoading}
          rows={3}
        />
        <button
          type="submit"
          className="button send-button"
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
