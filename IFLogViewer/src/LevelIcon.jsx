const LevelIcon = ({ level }) => {
  // UTF-8 characters appropriate for each log level
  // Designed to work well in monospace fonts like Courier New
  const icons = {
    'Trace': '·',      // Middle dot - subtle
    'Debug': '○',      // White circle - diagnostic
    'Information': '●', // Black circle - standard info
    'Warning': '▲',    // Triangle - attention
    'Error': '✗',      // Ballot X - problem
    'Critical': '◆'    // Black diamond - severe
  };

  // Colors for Hercules-style display (greens and ambers)
  const colors = {
    'Trace': '#336633',     // Dark green - subtle
    'Debug': '#669966',     // Medium green
    'Information': '#33ff33', // Bright green
    'Warning': '#ffcc00',   // Amber/yellow
    'Error': '#ff6633',     // Orange-red
    'Critical': '#ff3333'   // Bright red
  };

  return (
    <span 
      className="level-icon" 
      style={{ color: colors[level] || '#33ff33' }}
      title={level}
    >
      {icons[level] || '•'}
    </span>
  );
};

export default LevelIcon;
