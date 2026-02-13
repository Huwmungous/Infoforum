import { useState, useRef, useEffect, type FormEvent, type KeyboardEvent } from 'react';
import { useChat } from '../hooks/useChat';
import { ChatMessage } from './ChatMessage';
import { ThinkingProgress } from './ThinkingProgress';
import { ToolSelector } from './ToolSelector';
import './Chat.scss';

const logo = new URL('/IF-Logo.png', import.meta.url).href;

interface ChatProps {
  initialConversationId?: string;
  conversationTitle?: string | null;
  onConversationCreated?: (id: string) => void;
  onTitleGenerated?: () => void;
}

export function Chat({ initialConversationId, conversationTitle, onConversationCreated, onTitleGenerated }: ChatProps) {
  const [prompt, setPrompt] = useState('');
  const [enabledTools, setEnabledTools] = useState<string[]>([]);
  const [showToolSelector, setShowToolSelector] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const chatContainerRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const {
    messages,
    isLoading,
    isConnected,
    error,
    conversationId,
    sendMessage,
    loadConversation,
  } = useChat({ enabledTools, onTitleGenerated });

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
    const files = selectedFiles.length > 0 ? [...selectedFiles] : undefined;
    setPrompt('');
    setSelectedFiles([]);
    if (fileInputRef.current) fileInputRef.current.value = '';
    await sendMessage(messageText, files);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e as unknown as FormEvent);
    }
  };

  const handleFilesSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      setSelectedFiles(prev => [...prev, ...Array.from(e.target.files!)]);
    }
  };

  const removeFile = (index: number) => {
    setSelectedFiles(prev => prev.filter((_, i) => i !== index));
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
          {conversationTitle && (
            <span className="conversation-title" title={conversationTitle}>
              {conversationTitle}
            </span>
          )}
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
        {selectedFiles.length > 0 && (
          <div className="file-chips">
            {selectedFiles.map((file, index) => (
              <span key={`${file.name}-${index}`} className="file-chip">
                <svg className="file-chip-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
                </svg>
                <span className="file-chip-name">{file.name}</span>
                <span className="file-chip-size">({formatFileSize(file.size)})</span>
                <button
                  type="button"
                  className="file-chip-remove"
                  onClick={() => removeFile(index)}
                  title="Remove file"
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}
        <div className="input-row">
          <input
            ref={fileInputRef}
            type="file"
            multiple
            className="file-input-hidden"
            onChange={handleFilesSelected}
            accept=".txt,.md,.cs,.ts,.tsx,.js,.jsx,.json,.xml,.csv,.log,.py,.java,.cpp,.c,.h,.css,.scss,.html,.sql,.sh,.yml,.yaml,.zip"
          />
          <button
            type="button"
            className="attach-button"
            onClick={() => fileInputRef.current?.click()}
            disabled={!isConnected || isLoading}
            title="Attach files"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
            </svg>
          </button>
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
        </div>
      </form>

      {conversationId && (
        <div className="conversation-id">
          Conversation: {conversationId.slice(0, 8)}...
        </div>
      )}
    </div>
  );
}
