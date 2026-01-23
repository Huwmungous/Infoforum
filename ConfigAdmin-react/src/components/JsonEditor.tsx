import { useState, useEffect } from 'react';
import './JsonEditor.css';

interface JsonEditorProps {
  value: string;
  onChange: (value: string) => void;
  onFormat: () => void;
  placeholder?: string;
  hint?: string;
}

export function JsonEditor({ value, onChange, onFormat, placeholder, hint }: JsonEditorProps) {
  const [isValid, setIsValid] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!value.trim()) {
      setIsValid(true);
      setErrorMessage(null);
      return;
    }

    try {
      JSON.parse(value);
      setIsValid(true);
      setErrorMessage(null);
    } catch (err) {
      setIsValid(false);
      setErrorMessage(err instanceof Error ? err.message : 'Invalid JSON');
    }
  }, [value]);

  const lineCount = value.split('\n').length;
  const lines = Array.from({ length: Math.max(lineCount, 10) }, (_, i) => i + 1);

  return (
    <div className="json-editor">
      <div className="json-editor-toolbar">
        <div className="json-editor-status">
          {value.trim() && (
            <span className={`status-indicator ${isValid ? 'valid' : 'invalid'}`}>
              {isValid ? '✓ Valid JSON' : '✕ Invalid JSON'}
            </span>
          )}
        </div>
        <button type="button" className="btn btn-ghost btn-sm" onClick={onFormat} disabled={!isValid}>
          Format
        </button>
      </div>

      <div className={`json-editor-container ${!isValid ? 'has-error' : ''}`}>
        <div className="line-numbers">
          {lines.map(num => (
            <div key={num} className="line-number">{num}</div>
          ))}
        </div>
        <textarea
          className="form-input form-textarea json-textarea"
          value={value}
          onChange={e => onChange(e.target.value)}
          placeholder={placeholder}
          spellCheck={false}
        />
      </div>

      {errorMessage && (
        <div className="json-error">{errorMessage}</div>
      )}

      {hint && (
        <p className="json-hint">{hint}</p>
      )}
    </div>
  );
}
