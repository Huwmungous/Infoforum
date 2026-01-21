import './ThinkingProgress.scss';

interface ThinkingProgressProps {
  active: boolean;
}

export function ThinkingProgress({ active }: ThinkingProgressProps) {
  if (!active) return null;

  return (
    <div className="thinking-overlay">
      <div className="thinking-content">
        <div className="thinking-spinner"></div>
        <span>Thinking...</span>
      </div>
    </div>
  );
}
