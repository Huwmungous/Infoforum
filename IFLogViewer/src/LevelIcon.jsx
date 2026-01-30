const LevelIcon = ({ level, size = 24 }) => {
  // SVG icons for each log level
  const icons = {
    'Trace': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="1" fill="currentColor" />
      </svg>
    ),
    'Debug': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
        <ellipse cx="12" cy="14" rx="7" ry="8" />
        <line x1="12" y1="6" x2="12" y2="22" />
        <line x1="10" y1="7" x2="8" y2="3" />
        <line x1="14" y1="7" x2="16" y2="3" />
        <circle cx="9" cy="12" r="1.2" fill="currentColor" />
        <circle cx="15" cy="12" r="1.2" fill="currentColor" />
        <circle cx="9" cy="17" r="1.2" fill="currentColor" />
        <circle cx="15" cy="17" r="1.2" fill="currentColor" />
      </svg>
    ),
    'Information': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="16" x2="12" y2="12" />
        <line x1="12" y1="8" x2="12.01" y2="8" />
      </svg>
    ),
    'Warning': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
        <line x1="12" y1="9" x2="12" y2="13" />
        <line x1="12" y1="17" x2="12.01" y2="17" />
      </svg>
    ),
    'Error': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <circle cx="12" cy="12" r="10" />
        <line x1="15" y1="9" x2="9" y2="15" />
        <line x1="9" y1="9" x2="15" y2="15" />
      </svg>
    ),
    'Critical': (
      <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <polygon points="7.86 2 16.14 2 22 7.86 22 16.14 16.14 22 7.86 22 2 16.14 2 7.86 7.86 2" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
    )
  };

  // Colors for each level
  const colors = {
    'Trace': '#888888',
    'Debug': '#6b7280',
    'Information': '#00bcd4',
    'Warning': '#f59e0b',
    'Error': '#dc2626',
    'Critical': '#ef4444'
  };

  const isCritical = level === 'Critical';

  return (
    <span 
      className={`level-icon ${isCritical ? 'level-icon-pulse' : ''}`}
      style={{ 
        color: colors[level] || '#00bcd4',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center'
      }}
      title={level}
    >
      {icons[level] || icons['Information']}
    </span>
  );
};

export default LevelIcon;