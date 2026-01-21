import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import type { Message } from '../types';
import './ChatMessage.scss';

interface ChatMessageProps {
  message: Message;
}

export function ChatMessage({ message }: ChatMessageProps) {
  const [showThinking, setShowThinking] = useState(false);
  const isUser = message.role === 'user';

  const copyToClipboard = () => {
    const text = showThinking && message.thinkContent ? message.thinkContent : message.content;
    navigator.clipboard.writeText(text);
  };

  return (
    <div className={`chat-message ${isUser ? 'user-message' : 'ai-message'}`}>
      <div className="message-header">
        {!isUser && message.thinkContent && (
          <button
            className="show-think-button"
            onClick={() => setShowThinking(!showThinking)}
            aria-expanded={showThinking}
          >
            <span className="think-icon">💭</span>
            {showThinking ? 'Hide Thinking' : 'Show Thinking'}
          </button>
        )}
        <button className="copy-button" onClick={copyToClipboard} title="Copy to clipboard">
          📋
        </button>
      </div>

      {message.thinkContent && (
        <div className={`thinking-content ${showThinking ? 'expanded' : ''}`}>
          {message.thinkContent}
        </div>
      )}

      <div className="message-content">
        {isUser ? (
          <p>{message.content}</p>
        ) : (
          <ReactMarkdown
            components={{
              code({ className, children, ...props }) {
                const match = /language-(\w+)/.exec(className || '');
                const codeString = String(children).replace(/\n$/, '');
                
                if (match) {
                  return (
                    <SyntaxHighlighter
                      style={vscDarkPlus}
                      language={match[1]}
                      PreTag="div"
                    >
                      {codeString}
                    </SyntaxHighlighter>
                  );
                }
                
                return (
                  <code className={className} {...props}>
                    {children}
                  </code>
                );
              },
            }}
          >
            {message.content}
          </ReactMarkdown>
        )}
      </div>

      {message.attachments && message.attachments.length > 0 && (
        <div className="attachments">
          {message.attachments.map((att) => (
            <div key={att.id} className="attachment">
              📎 {att.fileName}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
