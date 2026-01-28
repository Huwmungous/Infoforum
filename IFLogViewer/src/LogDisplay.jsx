import React, { useState, useEffect, useLayoutEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAppContext, useAuth } from '@if/web-common-react';
import { useTheme } from './context/ThemeContext.jsx';
import LevelIcon from './components/LevelIcon';

// Logo import for base path compatibility
const logo = new URL('/IF-Logo.png', import.meta.url).href;

// Constants for live log trimming
const MAX_LIVE_LOGS = 300;
const TRIM_COUNT = 100;

const LogDisplay = ({ loggerServiceUrl }) => {
  const { auth } = useAppContext();
  const { getAccessToken, isAuthenticated, initialized } = auth;
  const { theme, setTheme } = useTheme();
  const { signout } = useAuth();
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [levelFilter, setLevelFilter] = useState('');
  const [messageFilter, setMessageFilter] = useState('');
  const [searchString, setSearchString] = useState('');
  const [focusedLog, setFocusedLog] = useState(null);
  const [copied, setCopied] = useState(false);

  // Real-time features
  const [isRealTime, setIsRealTime] = useState(false);
  const [isConnected, setIsConnected] = useState(false);
  const [maxLogs, setMaxLogs] = useState(100);
  const connectionRef = useRef(null);
  
  // Ref for auto-scrolling - points to the cursor at the bottom
  const cursorRef = useRef(null);
  const [autoScroll, setAutoScroll] = useState(true);

  // Use Vite proxy in dev mode to avoid CORS issues
  const apiBase = import.meta.env.DEV ? '/logger' : loggerServiceUrl;

  // Simple fetch helper - auth handled by AppInitializer interceptor
  const apiFetch = async (url, options = {}) => {
    const response = await fetch(url, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.detail || `HTTP ${response.status}: ${response.statusText}`);
    }

    return response.json();
  };

  const scrollToBottom = useCallback(() => {
    if (cursorRef.current) {
      cursorRef.current.scrollIntoView({ behavior: 'auto', block: 'end' });
    }
  }, []);

  const loadAllLogs = useCallback(async (shouldScrollToBottom = false) => {
    if (!apiBase) {
      console.error('loggerServiceUrl prop is required');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      console.log('Loading logs from:', `${apiBase}/logs?limit=${maxLogs}`);
      const data = await apiFetch(`${apiBase}/logs?limit=${maxLogs}`);
      setLogs(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
      // Scroll to bottom AFTER loading is complete (DOM will update on next tick)
      if (shouldScrollToBottom) {
        setTimeout(scrollToBottom, 100);
      }
    }
  }, [apiBase, maxLogs, scrollToBottom]);

  // Load logs when authenticated
  useEffect(() => {
    if (isAuthenticated && initialized && apiBase) {
      console.log('Auth ready, loading logs from:', apiBase);
      loadAllLogs();
    }
  }, [isAuthenticated, initialized, apiBase, loadAllLogs]);

  // Auto-scroll to cursor when new logs arrive in live mode
  // useLayoutEffect runs synchronously after DOM mutations
  useLayoutEffect(() => {
    if (isRealTime && autoScroll && !loading && cursorRef.current) {
      cursorRef.current.scrollIntoView({ behavior: 'auto', block: 'end' });
    }
  }, [logs, isRealTime, autoScroll, loading]);

  // Handle scroll to detect if user scrolled up (disable auto-scroll)
  const handleTerminalScroll = (e) => {
    const el = e.currentTarget;
    const { scrollTop, scrollHeight, clientHeight } = el;
    // If user is within 50px of bottom, re-enable auto-scroll
    const isAtBottom = scrollHeight - scrollTop - clientHeight < 50;
    setAutoScroll(isAtBottom);
  };

  // Real-time connection management
  useEffect(() => {
    if (!isRealTime) {
      // Disconnect if real-time is disabled
      if (connectionRef.current) {
        connectionRef.current.stop();
        connectionRef.current = null;
        setIsConnected(false);
      }
      return;
    }

    // Don't connect until authenticated and we have the URL
    if (!isAuthenticated || !initialized || !apiBase) {
      return;
    }

    // Create SignalR connection with auth token
    const createConnection = async () => {
      let token = null;

      try {
        token = await getAccessToken();
      } catch (e) {
        console.warn('getAccessToken failed for SignalR:', e);
        setError('No token available for real-time connection');
        return;
      }

      if (!token) {
        setError('No token available for real-time connection');
        return;
      }

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${apiBase}/loghub`, {
          accessTokenFactory: () => token
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

      connectionRef.current = connection;

      // Handle incoming logs with trimming logic
      connection.on('NewLogEntry', (log) => {
        console.log('Log hub message received:', log);
        log._isNew = true;
        setLogs(prevLogs => {
          const newLogs = [...prevLogs, log];
          // Trim oldest 100 entries when count exceeds 300
          if (newLogs.length > MAX_LIVE_LOGS) {
            return newLogs.slice(TRIM_COUNT);
          }
          return newLogs;
        });
      });

      // Handle connection state changes
      connection.onreconnecting(() => {
        console.log('Reconnecting...');
        setIsConnected(false);
      });

      connection.onreconnected(() => {
        console.log('Reconnected');
        setIsConnected(true);
      });

      connection.onclose(() => {
        console.log('Connection closed');
        setIsConnected(false);
      });

      // Start connection
      try {
        await connection.start();
        console.log('Connected to log stream');
        setIsConnected(true);
      } catch (err) {
        console.error('SignalR connection error:', err);
        setError(`Failed to connect to real-time log stream: ${err.message || err}`);
      }
    };

    createConnection();

    // Cleanup
    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
  }, [isRealTime, maxLogs, getAccessToken, isAuthenticated, initialized, apiBase]);

  const handleSearch = async () => {
    if (!apiBase) return;

    setLoading(true);
    setError(null);
    try {
      const filters = [];
      if (levelFilter) {
        filters.push({ field: 'level', operator: 'Equals', value: levelFilter });
      }
      if (messageFilter) {
        filters.push({ field: 'message', operator: 'Contains', value: messageFilter });
      }

      const data = await apiFetch(`${apiBase}/logs/search`, {
        method: 'POST',
        body: JSON.stringify({
          filters: filters,
          filterLogic: 'And',
          limit: maxLogs
        })
      });
      setLogs(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleRealTimeToggle = (enabled) => {
    setIsRealTime(enabled);
    if (enabled) {
      // When enabling real-time, load recent logs and scroll to bottom
      loadAllLogs(true);
      setAutoScroll(true);
    }
  };

  // Enhanced search to include all key fields
  const showRow = log => {
    if (!searchString) return true;
    const searchLower = searchString.toLowerCase();
    const message = log?.logData?.message?.toLowerCase() || '';
    const text = log?.logData?.text?.toLowerCase() || '';
    const category = log?.logData?.category?.toLowerCase() || '';
    const environment = log?.logData?.environment?.toLowerCase() || '';
    const application = log?.logData?.application?.toLowerCase() || '';
    const pathname = log?.logData?.pathname?.toLowerCase() || '';
    const machineName = log?.logData?.machineName?.toLowerCase() || '';
    const host = log?.logData?.host?.toLowerCase() || '';
    return message.includes(searchLower) ||
           text.includes(searchLower) ||
           category.includes(searchLower) ||
           environment.includes(searchLower) ||
           application.includes(searchLower) ||
           pathname.includes(searchLower) ||
           machineName.includes(searchLower) ||
           host.includes(searchLower);
  };

  // Badge color configs using CSS classes
  const getEnvironmentBadgeClass = (env) => {
    const envLower = env?.toLowerCase();
    switch (envLower) {
      case 'production': return 'badge-production';
      case 'uat': return 'badge-uat';
      case 'sit': return 'badge-sit';
      default: return 'badge-default';
    }
  };

  // Format timestamp for Hercules display
  const formatHerculesTime = (dateStr) => {
    const d = new Date(dateStr);
    return d.toLocaleTimeString('en-GB', { hour12: false }) + '.' + 
           d.getMilliseconds().toString().padStart(3, '0');
  };

  // Get level indicator character
  const getLevelChar = (level) => {
    const chars = {
      'Trace': '·',
      'Debug': '●',      // Solid circle (grey)
      'Information': '●',
      'Warning': '▲',
      'Error': '✗',
      'Critical': '◆'
    };
    return chars[level] || '•';
  };

  // Get level color for Hercules display
  const getLevelColor = (level) => {
    const colors = {
      'Trace': '#336633',
      'Debug': '#888888',   // Light grey
      'Information': '#33ff33',
      'Warning': '#ffcc00',
      'Error': '#ff6633',
      'Critical': '#ff3333'
    };
    return colors[level] || '#33ff33';
  };

  // Check if level should blink
  const shouldBlink = (level) => level === 'Critical';

  // Render Hercules-style terminal for live logs
  // Get just the class name from a fully-qualified category
  const getShortCategory = (category) => {
    if (!category) return 'System';
    // Extract the last part after the final dot
    const parts = category.split('.');
    return parts[parts.length - 1];
  };

  const renderHerculesTerminal = () => {
    const filteredLogs = logs.filter(showRow);
    
    return (
      <div className="hercules-container">
        <div className="hercules-header">
          <span>═══ LIVE LOG STREAM ═══</span>
          <span className="hercules-status">
            {isConnected ? '● CONNECTED' : '○ DISCONNECTED'}
          </span>
          <span className="hercules-count">{filteredLogs.length} entries</span>
          {!autoScroll && (
            <button 
              className="hercules-scroll-btn"
              onClick={() => {
                setAutoScroll(true);
                if (cursorRef.current) {
                  cursorRef.current.scrollIntoView({ behavior: 'auto', block: 'end' });
                }
              }}
            >
              ↓ SCROLL TO BOTTOM
            </button>
          )}
        </div>
        <div 
          className="hercules-terminal"
          onScroll={handleTerminalScroll}
        >
          {filteredLogs.map((log, index) => (
            <div 
              key={log.idx || index} 
              className={`hercules-line ${log._isNew ? 'hercules-new' : ''}`}
            >
              <span className="hercules-time">
                {formatHerculesTime(log.createdAt)}
              </span>
              <span 
                className={`hercules-level ${shouldBlink(log.logData?.level) ? 'hercules-blink' : ''}`}
                style={{ color: getLevelColor(log.logData?.level) }}
              >
                {getLevelChar(log.logData?.level)}
              </span>
              <span className="hercules-source">
                [{log.logData?.application || log.logData?.serviceName || 'System'}.{getShortCategory(log.logData?.category)}]
              </span>
              <span className="hercules-message">
                {log.logData?.message || ''}
              </span>
            </div>
          ))}
          {/* Blinking cursor line - always at bottom */}
          <div className="hercules-cursor-line" ref={cursorRef}>
            <span className="hercules-cursor">▌</span>
            {filteredLogs.length === 0 && (
              <span className="hercules-waiting-text">
                {isConnected ? ' Waiting for log entries...' : ' Connecting...'}
              </span>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="app-container">
      {/* Header */}
      <header className="app-header">
        {/* Left: Logo and Title */}
        <div className="header-left">
          <img src={logo} alt="IF" className="if-logo" />
          <h1 className="app-title">IF Log Viewer</h1>
          <span className="version-label">v1.0</span>
        </div>

        {/* Center: Search controls */}
        <div className="header-center">
          <select
            onChange={e => setLevelFilter(e.target.value)}
            disabled={isRealTime}
            className="filter-select"
          >
            <option value="">All Levels</option>
            <option value="Trace">Trace</option>
            <option value="Debug">Debug</option>
            <option value="Information">Information</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Critical">Critical</option>
          </select>

          <input
            type="text"
            value={messageFilter}
            onChange={(e) => setMessageFilter(e.target.value)}
            placeholder="Message..."
            disabled={isRealTime}
            className="filter-input"
          />

          <button
            onClick={handleSearch}
            disabled={isRealTime}
            className="btn-primary"
          >
            Search
          </button>
          <button
            onClick={loadAllLogs}
            disabled={isRealTime}
            className="btn-secondary"
          >
            Clear
          </button>
        </div>

        {/* Right: Settings (Max logs + Theme) */}
        <div className="header-right">
          <select
            value={maxLogs}
            onChange={(e) => setMaxLogs(parseInt(e.target.value))}
            disabled={isRealTime}
            className="theme-select"
          >
            {[25, 50, 75, 100, 125, 150, 175, 200, 225, 250, 275, 300, 325, 350, 375, 400, 425, 450, 475, 500].map(n => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>

          <select
            value={theme}
            onChange={(e) => setTheme(e.target.value)}
            className="theme-select"
          >
            <option value="light">Light</option>
            <option value="dark">Dark</option>
          </select>

          <button
            onClick={signout}
            className="btn-warning"
          >
            Sign Out
          </button>
        </div>
      </header>

      {/* Main container */}
      <div className="main-content">
        {/* Results section */}
        <div className="results-panel">
          {/* Filter row with toggle, search, and count */}
          <div className="filter-row">
            {/* Left: Realtime toggle */}
            <div className="filter-row-left">
              <button
                onClick={() => handleRealTimeToggle(!isRealTime)}
                className={`toggle-switch ${isRealTime ? 'toggle-switch-active' : ''}`}
              >
                <span className="toggle-switch-knob"></span>
              </button>
              <span>Live</span>
              {isRealTime && (
                <span className={isConnected ? 'status-connected' : 'status-disconnected'}>
                  {isConnected ? '●' : '○'}
                </span>
              )}
            </div>

            {/* Center: Search input */}
            <div className="filter-row-center">
              <input
                type="text"
                value={searchString}
                onChange={(e) => setSearchString(e.target.value)}
                placeholder="Filter Results..."
                className="search-input"
              />
            </div>

            {/* Right: Count */}
            <div className="filter-row-right">
              <span className="results-count">
                {logs.filter(showRow).length} log{logs.filter(showRow).length === 1 ? '' : 's'}
              </span>
            </div>
          </div>

          {/* Error message */}
          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          {/* Loading state */}
          {loading && (
            <div className="loading-message">
              Loading...
            </div>
          )}

          {/* Hercules Terminal for Live Mode */}
          {isRealTime && !loading && renderHerculesTerminal()}

          {/* Standard Table View for non-live mode */}
          {!isRealTime && !loading && logs.length === 0 && (
            <div className="empty-message">
              No logs found
            </div>
          )}

          {/* Log table (only shown when NOT in real-time mode) */}
          {!isRealTime && !loading && logs.length > 0 && (
            <>
              {/* Table */}
              <div className="table-container">
                <table className="log-table">
                  <thead>
                    <tr className="table-header-row">
                      <th className="table-header-cell"></th>
                      <th className="table-header-cell">ID</th>
                      <th className="table-header-cell">Level</th>
                      <th className="table-header-cell">Attributes</th>
                      <th className="table-header-cell">Message</th>
                      <th className="table-header-cell">Created</th>
                    </tr>
                  </thead>
                  <tbody>
                    {logs.map(log => (
                      showRow(log) ? (
                        <React.Fragment key={log.idx}>
                          <tr
                            className={`table-row ${focusedLog === log.idx ? 'table-row-focused' : ''} ${log._isNew ? 'animate-new-log' : ''}`}
                            onClick={() => setFocusedLog(idx => idx === log.idx ? null : log.idx)}
                          >
                            <td className="table-cell-icon">
                              {focusedLog === log.idx ? '▼' : '▶'}
                            </td>
                            <td className="table-cell">{log.idx}</td>
                            <td className="table-cell">
                              <LevelIcon level={log.logData?.level} />
                            </td>
                            <td className="table-cell">
                              <div className="badge-container">
                                {log.logData?.environment && (
                                  <span className={`badge ${getEnvironmentBadgeClass(log.logData.environment)}`}>
                                    {log.logData.environment}
                                  </span>
                                )}
                                {log.logData?.application && (
                                  <span className="badge badge-application">
                                    {log.logData.application}
                                  </span>
                                )}
                                {(log.logData?.machineName || log.logData?.host) && (
                                  <span className="badge badge-machine">
                                    {log.logData.machineName || log.logData.host}
                                  </span>
                                )}
                                {log.logData?.pathname && (
                                  <span className="badge badge-pathname">
                                    {log.logData.pathname}
                                  </span>
                                )}
                                {log.logData?.category && (
                                  <span className="badge badge-category">
                                    {log.logData.category}
                                  </span>
                                )}
                              </div>
                            </td>
                            <td className="table-cell">
                              {log.logData?.message || ''}
                            </td>
                            <td className="table-cell-date">
                              {new Date(log.createdAt).toLocaleDateString()}
                              {' '}
                              {new Date(log.createdAt).toLocaleTimeString()}
                            </td>
                          </tr>

                          {/* Expanded detail row */}
                          {focusedLog === log.idx && (
                            <tr>
                              <td colSpan={6}>
                                <div className="detail-panel">
                                  {/* Copy button */}
                                  <div className="detail-actions">
                                    <button
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        const fullLog = JSON.stringify(log.logData, null, 2);
                                        navigator.clipboard.writeText(fullLog);
                                        setCopied(true);
                                        setTimeout(() => setCopied(false), 2000);
                                      }}
                                      className={`btn-copy ${copied ? 'btn-copy-success' : ''}`}
                                    >
                                      {copied ? 'Copied!' : 'Copy'}
                                    </button>
                                  </div>

                                  {/* Log properties */}
                                  <div className="detail-grid">
                                    {typeof log.logData === 'string' ? (
                                      <div className="detail-value">
                                        {log.logData}
                                      </div>
                                    ) : (
                                      Object.entries(log.logData || {}).map(([key, value]) => (
                                        <div key={key} className="detail-row">
                                          <span className="detail-key">
                                            {key}:
                                          </span>
                                          <span className="detail-value">
                                            {value === null || value === undefined ? (
                                              <span className="detail-null">null</span>
                                            ) : typeof value === 'object' ? (
                                              JSON.stringify(value, null, 2)
                                            ) : (
                                              String(value)
                                            )}
                                          </span>
                                        </div>
                                      ))
                                    )}
                                  </div>
                                </div>
                              </td>
                            </tr>
                          )}
                        </React.Fragment>
                      ) : null
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default LogDisplay;