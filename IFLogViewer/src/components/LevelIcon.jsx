const LevelIcon = ({ level }) => {
  const icons = {
    'Trace': '🔍',
    'Debug': '🐛',
    'Information': 'ℹ️',
    'Warning': '⚠️',
    'Error': '❌',
    'Critical': '🔥'
  };

  return (
    <span className="level-icon">
      {icons[level] || '•'}
    </span>
  );
};

export default LevelIcon;
