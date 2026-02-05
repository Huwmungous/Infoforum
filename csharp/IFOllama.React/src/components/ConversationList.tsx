import type { Conversation } from '../types';
import './ConversationList.scss';

interface ConversationListProps {
  conversations: Conversation[];
  loading: boolean;
  error: string | null;
  onSelectConversation: (id: string) => void;
  selectedId: string | null;
  onNewChat: () => void;
  onDelete: (id: string) => void;
}

export function ConversationList({
  conversations,
  loading,
  error,
  onSelectConversation,
  selectedId,
  onNewChat,
  onDelete,
}: ConversationListProps) {

  const handleDelete = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this conversation?')) return;
    onDelete(id);
  };

  return (
    <div className="conversation-list">
      <div className="list-header">
        <h3>Conversations</h3>
        <button className="if-btn if-btn-primary if-btn-sm" onClick={onNewChat}>
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
