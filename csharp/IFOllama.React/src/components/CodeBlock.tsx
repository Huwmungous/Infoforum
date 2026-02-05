import { useState, useCallback } from 'react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import './CodeBlock.scss';

interface CodeBlockProps {
  language: string;
  code: string;
}

export function CodeBlock({ language, code }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback for older browsers
      const textarea = document.createElement('textarea');
      textarea.value = code;
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }, [code]);

  const handleSave = useCallback(() => {
    const extensions: Record<string, string> = {
      javascript: 'js', typescript: 'ts', python: 'py', csharp: 'cs',
      cpp: 'cpp', java: 'java', ruby: 'rb', go: 'go', rust: 'rs',
      html: 'html', css: 'css', scss: 'scss', json: 'json', xml: 'xml',
      yaml: 'yml', sql: 'sql', bash: 'sh', shell: 'sh', powershell: 'ps1',
      markdown: 'md', delphi: 'pas', pascal: 'pas', jsx: 'jsx', tsx: 'tsx',
    };
    const ext = extensions[language] || language || 'txt';
    const filename = `code.${ext}`;

    const blob = new Blob([code], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }, [code, language]);

  return (
    <div className="code-block">
      <div className="code-block-header">
        <span className="code-block-language">{language}</span>
        <div className="code-block-actions">
          <button
            className="code-action-btn"
            onClick={handleCopy}
            title={copied ? 'Copied!' : 'Copy to clipboard'}
            aria-label="Copy code to clipboard"
          >
            {copied ? (
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
              </svg>
            )}
          </button>
          <button
            className="code-action-btn"
            onClick={handleSave}
            title="Save to file"
            aria-label="Save code to file"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
          </button>
        </div>
      </div>
      <SyntaxHighlighter
        style={vscDarkPlus}
        language={language}
        PreTag="div"
        customStyle={{ margin: 0, borderRadius: '0 0 var(--if-radius-sm) var(--if-radius-sm)' }}
      >
        {code}
      </SyntaxHighlighter>
    </div>
  );
}
