import { useState, useRef, useEffect } from 'react';
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
  onRename: (id: string, newTitle: string) => void;
}

export function ConversationList({
  conversations,
  loading,
  error,
  onSelectConversation,
  selectedId,
  onNewChat,
  onDelete,
  onRename,
}: ConversationListProps) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  // Focus input when editing starts
  useEffect(() => {
    if (editingId && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [editingId]);

  const handleDelete = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this conversation?')) return;
    onDelete(id);
  };

  const startEditing = (id: string, currentTitle: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingId(id);
    setEditValue(currentTitle);
  };

  const commitEdit = () => {
    if (editingId && editValue.trim()) {
      onRename(editingId, editValue.trim());
    }
    setEditingId(null);
    setEditValue('');
  };

  const cancelEdit = () => {
    setEditingId(null);
    setEditValue('');
  };

  const handleEditKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      commitEdit();
    } else if (e.key === 'Escape') {
      e.preventDefault();
      cancelEdit();
    }
  };

  return (
    <div className="conversation-list">
      <div className="if-sidebar-header">
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
              onClick={() => editingId !== conv.id && onSelectConversation(conv.id)}
            >
              {editingId === conv.id ? (
                <input
                  ref={inputRef}
                  className="title-edit-input"
                  value={editValue}
                  onChange={(e) => setEditValue(e.target.value)}
                  onKeyDown={handleEditKeyDown}
                  onBlur={commitEdit}
                  onClick={(e) => e.stopPropagation()}
                />
              ) : (
                <span className="title">{conv.title}</span>
              )}
              <div className="action-buttons">
                <button
                  className="action-button edit-button"
                  onClick={(e) => startEditing(conv.id, conv.title, e)}
                  title="Rename conversation"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M17 3a2.85 2.85 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z" />
                    <path d="m15 5 4 4" />
                  </svg>
                </button>
                <button
                  className="action-button delete-button"
                  onClick={(e) => handleDelete(conv.id, e)}
                  title="Delete conversation"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M3 6h18" />
                    <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6" />
                    <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2" />
                    <line x1="10" y1="11" x2="10" y2="17" />
                    <line x1="14" y1="11" x2="14" y2="17" />
                  </svg>
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
