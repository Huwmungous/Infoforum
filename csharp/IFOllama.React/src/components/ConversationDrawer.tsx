import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '@if/web-common-react';
import { apiService } from '../services/apiService';
import { ConversationList } from './ConversationList';
import type { Conversation } from '../types';
import './ConversationDrawer.scss';

interface ConversationDrawerProps {
  isOpen: boolean;
  onToggle: () => void;
  onSelectConversation: (id: string) => void;
  selectedId: string | null;
  onNewChat: () => void;
  refreshKey: number;
  onTitleChange?: (title: string | null) => void;
}

interface TooltipState {
  text: string;
  top: number;
  left: number;
}

export function ConversationDrawer({
  isOpen,
  onToggle,
  onSelectConversation,
  selectedId,
  onNewChat,
  refreshKey,
  onTitleChange,
}: ConversationDrawerProps) {
  const auth = useAuth();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tooltip, setTooltip] = useState<TooltipState | null>(null);

  const userId = auth.user?.profile?.sub;

  useEffect(() => {
    const loadConversations = async () => {
      if (!userId) return;

      try {
        setLoading(true);
        const convs = await apiService.getConversations(userId);
        setConversations(convs);
        setError(null);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to load conversations';
        setError(message);
      } finally {
        setLoading(false);
      }
    };

    loadConversations();
  }, [userId, refreshKey]);

  // Notify parent of current conversation title whenever selection or list changes
  useEffect(() => {
    if (!selectedId) {
      onTitleChange?.(null);
      return;
    }
    const match = conversations.find(c => c.id === selectedId);
    onTitleChange?.(match?.title ?? null);
  }, [selectedId, conversations]);

  const handleDelete = async (id: string) => {
    if (!userId) return;

    try {
      await apiService.deleteConversation(id, userId);
      setConversations(prev => prev.filter(c => c.id !== id));
      if (selectedId === id) {
        onNewChat();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete';
      setError(message);
    }
  };

  const showTooltip = useCallback((e: React.MouseEvent<HTMLButtonElement>, title: string) => {
    const rect = e.currentTarget.getBoundingClientRect();
    setTooltip({
      text: title,
      top: rect.top + rect.height / 2,
      left: rect.right + 10,
    });
  }, []);

  const hideTooltip = useCallback(() => {
    setTooltip(null);
  }, []);

  return (
    <>
      <aside className={`conversation-drawer ${isOpen ? 'open' : 'closed'}`}>
        {isOpen ? (
          <ConversationList
            conversations={conversations}
            loading={loading}
            error={error}
            onSelectConversation={onSelectConversation}
            selectedId={selectedId}
            onNewChat={onNewChat}
            onDelete={handleDelete}
          />
        ) : (
          <div className="drawer-rail">
            <div className="rail-header">
              <button
                className="rail-icon new-chat-icon"
                onClick={onNewChat}
                title="New conversation"
                aria-label="New conversation"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="12" y1="5" x2="12" y2="19" />
                  <line x1="5" y1="12" x2="19" y2="12" />
                </svg>
              </button>
            </div>

            <div className="rail-conversations">
              {conversations.map(conv => (
                <button
                  key={conv.id}
                  className={`rail-icon conversation-icon ${selectedId === conv.id ? 'selected' : ''}`}
                  onClick={() => onSelectConversation(conv.id)}
                  onMouseEnter={(e) => showTooltip(e, conv.title)}
                  onMouseLeave={hideTooltip}
                  aria-label={conv.title}
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                  </svg>
                </button>
              ))}
            </div>
          </div>
        )}
      </aside>

      {/* Fixed-position tooltip rendered outside scroll container */}
      {tooltip && (
        <div
          className="rail-tooltip"
          style={{ top: tooltip.top, left: tooltip.left }}
        >
          {tooltip.text}
        </div>
      )}

      <button
        className={`drawer-toggle ${isOpen ? 'open' : ''}`}
        onClick={onToggle}
        title={isOpen ? 'Collapse conversations' : 'Expand conversations'}
        aria-label={isOpen ? 'Collapse conversations panel' : 'Expand conversations panel'}
      >
        <svg
          className="drawer-toggle-icon"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          {isOpen ? (
            <polyline points="15 18 9 12 15 6" />
          ) : (
            <polyline points="9 18 15 12 9 6" />
          )}
        </svg>
      </button>

      {isOpen && (
        <div className="drawer-backdrop" onClick={onToggle} />
      )}
    </>
  );
}
