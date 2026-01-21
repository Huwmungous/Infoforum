import { useState, useEffect } from 'react';
import { useAuth } from 'react-oidc-context';
import { apiService } from '../services/apiService';
import type { Conversation } from '../types';
import './ConversationList.scss';

interface ConversationListProps {
  onSelectConversation: (id: string) => void;
  selectedId: string | null;
  onNewChat: () => void;
}

export function ConversationList({ onSelectConversation, selectedId, onNewChat }: ConversationListProps) {
  const auth = useAuth();
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
  }, [userId]);

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!userId) return;

    if (!confirm('Are you sure you want to delete this conversation?')) return;

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

  return (
    <div className="conversation-list">
      <div className="list-header">
        <h3>Conversations</h3>
        <button className="button" onClick={onNewChat}>
          + New
        </button>
      </div>

      {error && <div className="error">{error}</div>}

      {loading ? (
        <div className="loading">Loading...</div>
      ) : conversations.length === 0 ? (
        <div className="empty">No conversations yet</div>
      ) : (
        <ul className="conversations">
          {conversations.map(conv => (
            <li
              key={conv.id}
              className={`conversation-item ${selectedId === conv.id ? 'selected' : ''}`}
              onClick={() => onSelectConversation(conv.id)}
            >
              <span className="title">{conv.title}</span>
              <button
                className="delete-button"
                onClick={(e) => handleDelete(conv.id, e)}
                title="Delete conversation"
              >
                🗑️
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
