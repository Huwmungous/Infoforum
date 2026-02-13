import './ThinkingProgress.scss';

interface ThinkingProgressProps {
  active: boolean;
}

export function ThinkingProgress({ active }: ThinkingProgressProps) {
  if (!active) return null;

  return (
    <div className="thinking-overlay">
      <div className="thinking-panel">
        <img src={`${import.meta.env.BASE_URL}ai_thinking.gif`} alt="Thinking..." className="thinking-gif" />
        <span>Thinking...</span>
      </div>
    </div>
  );
}
