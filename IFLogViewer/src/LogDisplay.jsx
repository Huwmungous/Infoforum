import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import * as signalR from '@microsoft/signalr';
import { FixedSizeList as List } from 'react-window';
import { useAppContext, useAuth } from '@if/web-common-react';
import { useTheme } from './context/ThemeContext.jsx';
import LevelIcon from './LevelIcon';

// Logo import for base path compatibility
const logo = new URL('/IF-Logo.png', import.meta.url).href;

// Constants for live log trimming
const MAX_LIVE_LOGS = 300;
const TRIM_COUNT = 100;

// Row heights for virtualization
const HERCULES_ROW_HEIGHT = 22;
const TABLE_ROW_HEIGHT = 44;

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
  
  // Refs for virtualized lists
  const herculesListRef = useRef(null);
  const tableListRef = useRef(null);
  const [autoScroll, setAutoScroll] = useState(true);
  
  // Container size for virtualized list
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });
  const containerRef = useRef(null);

  // Use Vite proxy in dev mode to avoid CORS issues
  const apiBase = import.meta.env.DEV ? '/logger' : loggerServiceUrl;

  // Simple fetch helper
  const apiFetch = async (url, options = {}) => {
    const response = await fetch(url, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return response.json();
  };

  // Load all logs
  const loadAllLogs = useCallback(async (preserveLogs = false) => {
    if (!apiBase) return;

    if (!preserveLogs) {
      setLoading(true);
    }
    setError(null);
    try {
      const data = await apiFetch(`${apiBase}/logs?limit=${maxLogs}&offset=0`);
      setLogs(data || []);
      setFocusedLog(null);
    } catch (err) {
      console.error('Failed to load logs:', err);
      setError(`Failed to load logs: ${err.message}`);
    } finally {
      setLoading(false);
    }
  }, [apiBase, maxLogs]);

  // Initial load when authenticated
  useEffect(() => {
    if (isAuthenticated && initialized && apiBase && !isRealTime) {
      loadAllLogs();
    }
  }, [isAuthenticated, initialized, apiBase, loadAllLogs, isRealTime]);

  // Log level filtering
  const levelPriority = useMemo(() => ({
    'Trace': 0,
    'Debug': 1,
    'Information': 2,
    'Warning': 3,
    'Error': 4,
    'Critical': 5
  }), []);

  const showRow = useCallback((log) => {
    if (levelFilter) {
      const logLevel = log?.logData?.level;
      const minPriority = levelPriority[levelFilter] ?? 0;
      const logPriority = levelPriority[logLevel] ?? 0;
      if (logPriority < minPriority) return false;
    }
    
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
  }, [levelFilter, searchString, levelPriority]);

  const filteredLogs = useMemo(() => logs.filter(showRow), [logs, showRow]);

  // Measure container size for virtualized list
  useEffect(() => {
    const updateSize = () => {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect();
        setContainerSize({ width: rect.width, height: rect.height });
      }
    };
    
    // Initial measurement with small delay to ensure DOM is ready
    const timer = setTimeout(updateSize, 50);
    window.addEventListener('resize', updateSize);
    
    const resizeObserver = new ResizeObserver(updateSize);
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }
    
    return () => {
      clearTimeout(timer);
      window.removeEventListener('resize', updateSize);
      resizeObserver.disconnect();
    };
  }, []);

  // Auto-scroll to bottom when new logs arrive in live mode
  useEffect(() => {
    if (isRealTime && autoScroll && herculesListRef.current && filteredLogs.length > 0) {
      herculesListRef.current.scrollToItem(filteredLogs.length - 1, 'end');
    }
  }, [filteredLogs.length, isRealTime, autoScroll]);

  // Real-time connection management
  useEffect(() => {
    if (!isRealTime) {
      if (connectionRef.current) {
        connectionRef.current.stop();
        connectionRef.current = null;
        setIsConnected(false);
      }
      return;
    }

    if (!isAuthenticated || !initialized || !apiBase) {
      return;
    }

    const createConnection = async () => {
      let token = null;

      try {
        token = await getAccessToken();
      } catch (e) {
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
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      connectionRef.current = connection;

      connection.on('NewLogEntry', (log) => {
        log._isNew = true;
        setLogs(prevLogs => {
          const newLogs = [...prevLogs, log];
          if (newLogs.length > MAX_LIVE_LOGS) {
            return newLogs.slice(TRIM_COUNT);
          }
          return newLogs;
        });
      });

      connection.onreconnecting(() => setIsConnected(false));
      connection.onreconnected(() => setIsConnected(true));
      connection.onclose(() => setIsConnected(false));

      try {
        await connection.start();
        setIsConnected(true);
        if (levelFilter) {
          await connection.invoke('SetMinimumLogLevel', levelFilter);
        }
      } catch (err) {
        setError(`Failed to connect: ${err.message || err}`);
      }
    };

    createConnection();

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
  }, [isRealTime, getAccessToken, isAuthenticated, initialized, apiBase, levelFilter]);

  // Update server-side log level filter
  useEffect(() => {
    const updateServerLogLevel = async () => {
      if (isRealTime && connectionRef.current?.state === signalR.HubConnectionState.Connected) {
        try {
          await connectionRef.current.invoke('SetMinimumLogLevel', levelFilter || 'Trace');
        } catch (err) {
          console.error('Failed to update log level filter:', err);
        }
      }
    };
    updateServerLogLevel();
  }, [levelFilter, isRealTime]);

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
          limit: maxLogs,
          offset: 0,
          filters: filters,
          filterLogic: 'And',
        }),
      });
      setLogs(data || []);
      setFocusedLog(null);
    } catch (err) {
      setError(`Search failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleRealTimeToggle = (enabled) => {
    setIsRealTime(enabled);
    if (enabled) {
      setLogs([]);
      setFocusedLog(null);
      setAutoScroll(true);
    } else {
      loadAllLogs(true);
      setAutoScroll(true);
    }
  };

  const focusedLogData = useMemo(() => {
    if (focusedLog === null) return null;
    return filteredLogs.find(log => log.idx === focusedLog);
  }, [focusedLog, filteredLogs]);

  const getEnvironmentBadgeClass = (env) => {
    const envLower = env?.toLowerCase();
    switch (envLower) {
      case 'production': return 'badge-production';
      case 'uat': return 'badge-uat';
      case 'sit': return 'badge-sit';
      default: return 'badge-default';
    }
  };

  const formatHerculesTime = (dateStr) => {
    const d = new Date(dateStr);
    return d.toLocaleTimeString('en-GB', { hour12: false }) + '.' + 
           d.getMilliseconds().toString().padStart(3, '0');
  };

  const getLevelChar = (level) => {
    const chars = { 'Trace': '·', 'Debug': '○', 'Information': '●', 'Warning': '▲', 'Error': '✗', 'Critical': '◆' };
    return chars[level] || '•';
  };

  const getLevelColor = (level) => {
    const colors = { 'Trace': '#336633', 'Debug': '#669966', 'Information': '#33ff33', 'Warning': '#ffcc00', 'Error': '#ff6633', 'Critical': '#ff3333' };
    return colors[level] || '#33ff33';
  };

  const shouldBlink = (level) => level === 'Critical';

  const getShortCategory = (category) => {
    if (!category) return 'General';
    const parts = category.split('.');
    return parts[parts.length - 1];
  };

  // Hercules row renderer
  const HerculesRow = useCallback(({ index, style }) => {
    const log = filteredLogs[index];
    if (!log) return null;
    
    return (
      <div style={style} className={`hercules-line ${log._isNew ? 'hercules-new' : ''}`}>
        <span className="hercules-time">{formatHerculesTime(log.createdAt)}</span>
        <span 
          className={`hercules-level ${shouldBlink(log.logData?.level) ? 'hercules-blink' : ''}`}
          style={{ color: getLevelColor(log.logData?.level) }}
        >
          {getLevelChar(log.logData?.level)}
        </span>
        <span className="hercules-source">
          [{log.logData?.application || 'System'}.{getShortCategory(log.logData?.category)}]
        </span>
        <span className="hercules-message">{log.logData?.message || ''}</span>
      </div>
    );
  }, [filteredLogs]);

  // Table row renderer
  const TableRow = useCallback(({ index, style }) => {
    const log = filteredLogs[index];
    if (!log) return null;
    
    return (
      <div 
        style={style} 
        className={`table-row-virtual ${focusedLog === log.idx ? 'table-row-focused' : ''}`}
        onClick={() => setFocusedLog(idx => idx === log.idx ? null : log.idx)}
      >
        <div className="table-cell-icon">{focusedLog === log.idx ? '▼' : '▶'}</div>
        <div className="table-cell table-cell-id">{log.idx}</div>
        <div className="table-cell table-cell-level"><LevelIcon level={log.logData?.level} /></div>
        <div className="table-cell table-cell-badges">
          <div className="badge-container">
            {log.logData?.environment && (
              <span className={`badge ${getEnvironmentBadgeClass(log.logData.environment)}`}>{log.logData.environment}</span>
            )}
            {log.logData?.application && (
              <span className="badge badge-application">{log.logData.application}</span>
            )}
            {log.logData?.category && (
              <span className="badge badge-category">{getShortCategory(log.logData.category)}</span>
            )}
          </div>
        </div>
        <div className="table-cell table-cell-message">{log.logData?.message || ''}</div>
        <div className="table-cell table-cell-date">
          {new Date(log.createdAt).toLocaleDateString()} {new Date(log.createdAt).toLocaleTimeString()}
        </div>
      </div>
    );
  }, [filteredLogs, focusedLog]);

  const handleHerculesScroll = useCallback(({ scrollOffset }) => {
    if (herculesListRef.current) {
      const listHeight = containerSize.height - 40;
      const contentHeight = filteredLogs.length * HERCULES_ROW_HEIGHT;
      const maxScroll = contentHeight - listHeight;
      setAutoScroll(scrollOffset >= maxScroll - 50);
    }
  }, [containerSize.height, filteredLogs.length]);

  const renderHerculesTerminal = () => {
    // Account for: hercules-container margins (8px each side), header (~40px), padding
    const listHeight = Math.max(containerSize.height - 70, 200);
    
    return (
      <div className="hercules-container">
        <div className="hercules-header">
          <span>═══ LIVE LOG STREAM ═══</span>
          <span className="hercules-status">{isConnected ? '● CONNECTED' : '○ DISCONNECTED'}</span>
          <span className="hercules-count">{filteredLogs.length} entries</span>
          {!autoScroll && (
            <button className="hercules-scroll-btn" onClick={() => {
              setAutoScroll(true);
              herculesListRef.current?.scrollToItem(filteredLogs.length - 1, 'end');
            }}>↓ SCROLL TO BOTTOM</button>
          )}
        </div>
        <div className="hercules-terminal">
          {containerSize.height > 0 && (
            <List
              ref={herculesListRef}
              height={listHeight}
              width={containerSize.width || 800}
              itemCount={filteredLogs.length}
              itemSize={HERCULES_ROW_HEIGHT}
              onScroll={handleHerculesScroll}
              overscanCount={20}
            >
              {HerculesRow}
            </List>
          )}
          {filteredLogs.length === 0 && (
            <div className="hercules-waiting">{isConnected ? 'Waiting for log entries...' : 'Connecting...'}</div>
          )}
        </div>
      </div>
    );
  };

  const renderDetailPanel = () => {
    if (!focusedLogData) return null;
    
    return (
      <div className="detail-panel">
        <div className="detail-header">
          <span>Log Details - ID: {focusedLogData.idx}</span>
          <button onClick={() => {
            navigator.clipboard.writeText(JSON.stringify(focusedLogData.logData, null, 2));
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
          }} className={`btn-copy ${copied ? 'btn-copy-success' : ''}`}>{copied ? 'Copied!' : 'Copy'}</button>
          <button onClick={() => setFocusedLog(null)} className="btn-close">✕</button>
        </div>
        <div className="detail-grid">
          {Object.entries(focusedLogData.logData || {}).map(([key, value]) => (
            <div key={key} className="detail-row">
              <span className="detail-key">{key}:</span>
              <span className="detail-value">
                {value === null ? <span className="detail-null">null</span> : typeof value === 'object' ? JSON.stringify(value, null, 2) : String(value)}
              </span>
            </div>
          ))}
        </div>
      </div>
    );
  };

  const renderTableView = () => {
    // Account for: table header (~40px), and optionally detail panel (~220px)
    const headerHeight = 40;
    const detailHeight = focusedLogData ? 220 : 0;
    const listHeight = Math.max(containerSize.height - headerHeight - detailHeight, 200);
    
    return (
      <div className="table-view-container">
        <div className="table-header-virtual">
          <div className="table-header-cell table-header-icon"></div>
          <div className="table-header-cell table-header-id">ID</div>
          <div className="table-header-cell table-header-level">Level</div>
          <div className="table-header-cell table-header-badges">Attributes</div>
          <div className="table-header-cell table-header-message">Message</div>
          <div className="table-header-cell table-header-date">Created</div>
        </div>
        
        {containerSize.height > 0 && (
          <List
            ref={tableListRef}
            height={listHeight}
            width={containerSize.width || 800}
            itemCount={filteredLogs.length}
            itemSize={TABLE_ROW_HEIGHT}
            overscanCount={10}
          >
            {TableRow}
          </List>
        )}
        
        {renderDetailPanel()}
      </div>
    );
  };

  return (
    <div className="app-container">
      <header className="app-header">
        <div className="header-left">
          <img src={logo} alt="IF" className="if-logo" />
          <h1 className="app-title">IF Log Viewer</h1>
          <span className="version-label">v1.0</span>
        </div>

        <div className="header-center">
          <select value={levelFilter} onChange={e => setLevelFilter(e.target.value)} className="if-header-select">
            <option value="">All Levels</option>
            <option value="Trace">Trace+</option>
            <option value="Debug">Debug+</option>
            <option value="Information">Info+</option>
            <option value="Warning">Warning+</option>
            <option value="Error">Error+</option>
            <option value="Critical">Critical</option>
          </select>

          <input type="text" value={messageFilter} onChange={(e) => setMessageFilter(e.target.value)}
            placeholder="Message..." disabled={isRealTime} className="if-header-input" />

          <button onClick={handleSearch} disabled={isRealTime} className="if-btn if-btn-primary if-btn-sm">Search</button>
          <button onClick={loadAllLogs} disabled={isRealTime} className="if-btn if-btn-secondary if-btn-sm">Clear</button>
        </div>

        <div className="header-right">
          <select value={maxLogs} onChange={(e) => setMaxLogs(parseInt(e.target.value))} disabled={isRealTime} className="if-header-select">
            {[25, 50, 75, 100, 150, 200, 300, 500].map(n => <option key={n} value={n}>{n}</option>)}
          </select>

          <select value={theme} onChange={(e) => setTheme(e.target.value)} className="if-header-select">
            <option value="light">Light</option>
            <option value="dark">Dark</option>
          </select>

          <button onClick={signout} className="if-btn if-btn-warning if-btn-sm">Sign Out</button>
        </div>
      </header>

      <div className="main-content">
        <div className="filter-row">
          <div className="filter-row-left">
            <button onClick={() => handleRealTimeToggle(!isRealTime)} className={`toggle-switch ${isRealTime ? 'toggle-switch-active' : ''}`}>
              <span className="toggle-switch-knob"></span>
            </button>
            <span>Live</span>
            {isRealTime && <span className={isConnected ? 'status-connected' : 'status-disconnected'}>{isConnected ? '●' : '○'}</span>}
          </div>

          <div className="filter-row-center">
            <input type="text" value={searchString} onChange={(e) => setSearchString(e.target.value)} placeholder="Filter Results..." className="search-input" />
          </div>

          <div className="filter-row-right">
            <span className="results-count">{filteredLogs.length} log{filteredLogs.length === 1 ? '' : 's'}</span>
          </div>
        </div>

        <div className="results-panel" ref={containerRef}>
          {error && <div className="error-message">{error}</div>}
          {loading && <div className="loading-message">Loading...</div>}
          {isRealTime && !loading && renderHerculesTerminal()}
          {!isRealTime && !loading && logs.length === 0 && <div className="empty-message">No logs found</div>}
          {!isRealTime && !loading && logs.length > 0 && renderTableView()}
        </div>
      </div>
    </div>
  );
};

export default LogDisplay;
