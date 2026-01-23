import { useAppContext } from '@if/web-common';

const LogTests = () => {
  const { createLogger } = useAppContext();

  // Read additional config from env vars
  const loggerServiceUrl = import.meta.env.VITE_LOG_SERVICE_URL;
  const realm = import.meta.env.VITE_IF_REALM;
  const client = import.meta.env.VITE_IF_CLIENT;
  const environment = import.meta.env.MODE.toUpperCase();

  // Frontend log using createLogger from AppContext
  const sendLog = (level, message) => {
    const logger = createLogger('LogViewerTests');
    switch (level) {
      case 'trace': logger.trace(message); break;
      case 'debug': logger.debug(message); break;
      case 'info': logger.info(message); break;
      case 'warn': logger.warn(message); break;
      case 'error': logger.error(message, new Error('Test error')); break;
      case 'critical': logger.critical(message, new Error('Test critical')); break;
    }
  };

  // Backend-style log (simulates .NET service logs)
  const sendBackendLog = async (level, message) => {
    if (!loggerServiceUrl) {
      console.error('VITE_LOG_SERVICE_URL not configured');
      return;
    }

    const request = {
      realm: realm,
      client: client,
      logData: {
        timestamp: new Date().toISOString(),
        level: level,
        category: 'LoggerWebService',
        message: message,
        exception: level === 'Error' || level === 'Critical' ? 'System.Exception: Test exception\n   at TestMethod()' : null,
        clientId: client,
        application: 'TestBackendService',
        environment: environment,
        machineName: 'sofia-d'
      },
      environment: environment,
      application: 'TestBackendService',
      logLevel: level
    };

    try {
      const response = await fetch(loggerServiceUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      });
      console.log(`Backend ${level} log sent:`, response.ok ? 'OK' : response.status);
    } catch (err) {
      console.error('Failed to send backend log:', err);
    }
  };

  const buttonStyle = (color) => ({
    padding: '16px 32px',
    fontSize: '18px',
    fontWeight: 'bold',
    color: 'white',
    backgroundColor: color,
    border: 'none',
    borderRadius: '8px',
    cursor: 'pointer',
    minWidth: '150px',
  });

  const sectionStyle = {
    marginBottom: '48px',
    padding: '24px',
    backgroundColor: '#f9fafb',
    borderRadius: '12px',
  };

  return (
    <div style={{
      padding: '40px',
      fontFamily: 'system-ui, sans-serif',
      maxWidth: '900px',
      margin: '0 auto'
    }}>
      <h1 style={{ marginBottom: '8px' }}>Log Viewer Tests</h1>
      <p style={{ color: '#666', marginBottom: '32px' }}>Click buttons to send log entries to the logging service</p>

      {/* Frontend Logs Section */}
      <div style={sectionStyle}>
        <h2 style={{ marginTop: 0, marginBottom: '16px', color: '#1f2937' }}>Log Viewer (Browser)</h2>
        <p style={{ color: '#6b7280', marginBottom: '20px', fontSize: '14px' }}>Simulates logs from React/browser applications</p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(3, 1fr)',
          gap: '12px',
          marginBottom: '16px'
        }}>
          <button style={buttonStyle('#9CA3AF')} onClick={() => sendLog('trace', 'Trace level message')}>
            Trace
          </button>
          <button style={buttonStyle('#6B7280')} onClick={() => sendLog('debug', 'Debug level message')}>
            Debug
          </button>
          <button style={buttonStyle('#3B82F6')} onClick={() => sendLog('info', 'Information level message')}>
            Information
          </button>
          <button style={buttonStyle('#F59E0B')} onClick={() => sendLog('warn', 'Warning level message')}>
            Warning
          </button>
          <button style={buttonStyle('#EF4444')} onClick={() => sendLog('error', 'Error level message')}>
            Error
          </button>
          <button style={buttonStyle('#7C3AED')} onClick={() => sendLog('critical', 'Critical level message')}>
            Critical
          </button>
        </div>

        <button
          style={{ ...buttonStyle('#10B981'), width: '100%', padding: '16px' }}
          onClick={() => {
            sendLog('trace', 'Trace level message');
            sendLog('debug', 'Debug level message');
            sendLog('info', 'Information level message');
            sendLog('warn', 'Warning level message');
            sendLog('error', 'Error level message');
            sendLog('critical', 'Critical level message');
          }}
        >
          Send All Frontend Levels
        </button>
      </div>

      {/* Backend Logs Section */}
      <div style={sectionStyle}>
        <h2 style={{ marginTop: 0, marginBottom: '16px', color: '#1f2937' }}>Logger Web Service (Server)</h2>
        <p style={{ color: '#6b7280', marginBottom: '20px', fontSize: '14px' }}>Simulates logs from .NET backend services</p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(3, 1fr)',
          gap: '12px',
          marginBottom: '16px'
        }}>
          <button style={buttonStyle('#9CA3AF')} onClick={() => sendBackendLog('Trace', 'Backend trace message')}>
            Trace
          </button>
          <button style={buttonStyle('#6B7280')} onClick={() => sendBackendLog('Debug', 'Backend debug message')}>
            Debug
          </button>
          <button style={buttonStyle('#3B82F6')} onClick={() => sendBackendLog('Information', 'Backend info message')}>
            Information
          </button>
          <button style={buttonStyle('#F59E0B')} onClick={() => sendBackendLog('Warning', 'Backend warning message')}>
            Warning
          </button>
          <button style={buttonStyle('#EF4444')} onClick={() => sendBackendLog('Error', 'Backend error message')}>
            Error
          </button>
          <button style={buttonStyle('#7C3AED')} onClick={() => sendBackendLog('Critical', 'Backend critical message')}>
            Critical
          </button>
        </div>

        <button
          style={{ ...buttonStyle('#0EA5E9'), width: '100%', padding: '16px' }}
          onClick={() => {
            sendBackendLog('Trace', 'Backend trace message');
            sendBackendLog('Debug', 'Backend debug message');
            sendBackendLog('Information', 'Backend info message');
            sendBackendLog('Warning', 'Backend warning message');
            sendBackendLog('Error', 'Backend error message');
            sendBackendLog('Critical', 'Backend critical message');
          }}
        >
          Send All Backend Levels
        </button>
      </div>
    </div>
  );
};

export default LogTests;
