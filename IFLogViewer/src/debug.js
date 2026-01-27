import { useAppContext } from '@if/web-common-react';
// Debug utilities exposed to browser console for testing
// This is a side-effect module - import it to set up window.IFDebug

let appContext = null;

export const initDebug = (context) => {
  appContext = context;

  const debugLogger = () => context.createLogger('BrowserConsoleTest');

  window.IFDebug = {
    trace: (message = 'Test trace message') => {
      debugLogger().trace(message);
      console.log('Trace sent:', message);
    },
    debug: (message = 'Test debug message') => {
      debugLogger().debug(message);
      console.log('Debug sent:', message);
    },
    info: (message = 'Test info message') => {
      debugLogger().info(message);
      console.log('Info sent:', message);
    },
    warn: (message = 'Test warning message') => {
      debugLogger().warn(message);
      console.log('Warning sent:', message);
    },
    error: (message = 'Test error message') => {
      debugLogger().error(message, new Error('Test exception'));
      console.log('Error sent:', message);
    },
    critical: (message = 'Test critical message') => {
      debugLogger().critical(message, new Error('Critical test exception'));
      console.log('Critical sent:', message);
    },
    testAll: () => {
      console.log('Sending all log levels...');
      window.IFDebug.trace();
      window.IFDebug.debug();
      window.IFDebug.info();
      window.IFDebug.warn();
      window.IFDebug.error();
      window.IFDebug.critical();
      console.log('All log levels sent!');
    },
    getConfig: () => ({
      loggerService: context.config.loggerService,
      logLevel: context.config.logLevel,
      clientId: context.config.clientId,
      isInitialized: context.config.isInitialized,
    }),
    getAuth: () => ({
      isAuthenticated: context.auth.isAuthenticated,
      initialized: context.auth.initialized,
      user: context.auth.user,
    }),
  };

  console.log('IFDebug available: trace, debug, info, warn, error, critical, testAll, getConfig, getAuth');
};
