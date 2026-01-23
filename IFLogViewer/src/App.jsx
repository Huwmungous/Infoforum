import { useEffect } from 'react';
import { useAppContext } from '@if/web-common';
import { ThemeProvider } from './context/ThemeContext.jsx';
import LogDisplay from './LogDisplay.jsx';
import { initDebug } from './debug.js';

function App() {
  const appContext = useAppContext();
  const { config } = appContext;

  // Initialize debug utilities with context
  useEffect(() => {
    initDebug(appContext);
  }, [appContext]);

  return (
    <ThemeProvider>
      <LogDisplay loggerServiceUrl={config.loggerService} />
    </ThemeProvider>
  );
}

export default App;
