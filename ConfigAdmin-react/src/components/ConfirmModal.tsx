interface ConfirmModalProps {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmVariant?: 'primary' | 'danger';
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmModal({
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmVariant = 'primary',
  onConfirm,
  onCancel
}: ConfirmModalProps) {
  return (
    <div className="if-modal-overlay" onClick={onCancel}>
      <div className="if-modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '480px' }}>
        <div className="if-modal-header">
          <h2>{title}</h2>
          <button className="if-btn if-btn-ghost if-btn-sm" onClick={onCancel}>✕</button>
        </div>
        <div className="if-modal-body">
          <p>{message}</p>
        </div>
        <div className="if-modal-footer">
          <button className="if-btn if-btn-secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button 
            className={`if-btn if-btn-${confirmVariant}`}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
