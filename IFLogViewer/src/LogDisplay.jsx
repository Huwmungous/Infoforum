import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import * as signalR from '@microsoft/signalr';
import { FixedSizeList as List } from 'react-window';
import { useAppContext, useAuth } from '@if/web-common-react';
import { useTheme } from './context/ThemeContext.jsx';
import LevelIcon from './LevelIcon';

// Logo import for base path compatibility
const logo = new URL('/IF-Logo.png', import.meta.url).href;

// Row heights for virtualization
const HERCULES_ROW_HEIGHT = 22;
const TABLE_ROW_HEIGHT = 44;

// Buffer management constants
const DEFAULT_BUFFER_SIZE = 500;
const FETCH_BATCH_SIZE = 50;
const SCROLL_THRESHOLD = 5; // items from edge to trigger fetch

const LogDisplay = ({ loggerServiceUrl }) => {
  const { auth } = useAppContext();
  const { getAccessToken, isAuthenticated, initialized } = auth;
  const { theme, setTheme } = useTheme();
  const { signout } = useAuth();
  
  // Core state
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
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
  const [bufferSize, setBufferSize] = useState(DEFAULT_BUFFER_SIZE);
  const connectionRef = useRef(null);
  
  // Buffer tracking for infinite scroll
  const [hasMoreBefore, setHasMoreBefore] = useState(true);
  const [isAtLiveEdge, setIsAtLiveEdge] = useState(true);
  const oldestIdxRef = useRef(null);
  const newestIdxRef = useRef(null);
  
  // Refs for virtualized lists
  const herculesListRef = useRef(null);
  const tableListRef = useRef(null);
  const [autoScroll, setAutoScroll] = useState(true);
  const scrollToBottomRef = useRef(true);
  
  // Container size for virtualized list
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });
  const containerRef = useRef(null);

  // Use Vite proxy in dev mode
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

  // Build search request with current filters
  const buildSearchRequest = useCallback((limit, fromIdx = null, back = 1) => {
    const request = {
      limit,
      filters: [],
      filterLogic: 'And',
    };
    
    if (levelFilter) {
      request.filters.push({ field: 'level', operator: 'Equals', value: levelFilter });
    }
    if (messageFilter) {
      request.filters.push({ field: 'message', operator: 'Contains', value: messageFilter });
    }
    
    if (fromIdx !== null) {
      request.from_idx = fromIdx;
      request.back = back;
    }
    
    return request;
  }, [levelFilter, messageFilter]);

  // Update buffer tracking refs when logs change
  useEffect(() => {
    if (logs.length > 0) {
      oldestIdxRef.current = Math.min(...logs.map(l => l.idx));
      newestIdxRef.current = Math.max(...logs.map(l => l.idx));
    } else {
      oldestIdxRef.current = null;
      newestIdxRef.current = null;
    }
  }, [logs]);

  // Load initial logs (most recent)
  const loadInitialLogs = useCallback(async () => {
    if (!apiBase) return;

    setLoading(true);
    setError(null);
    try {
      const request = buildSearchRequest(maxLogs);
      const data = await apiFetch(`${apiBase}/logs/search`, {
        method: 'POST',
        body: JSON.stringify(request),
      });
      
      setLogs(data || []);
      setFocusedLog(null);
      setHasMoreBefore(data?.length >= maxLogs);
      setIsAtLiveEdge(true);
      scrollToBottomRef.current = true;
    } catch (err) {
      console.error('Failed to load logs:', err);
      setError(`Failed to load logs: ${err.message}`);
    } finally {
      setLoading(false);
    }
  }, [apiBase, maxLogs, buildSearchRequest]);

  // Fetch older logs (backwards)
  const fetchOlderLogs = useCallback(async () => {
    if (!apiBase || !hasMoreBefore || loadingMore || oldestIdxRef.current === null) return;

    setLoadingMore(true);
    try {
      const request = buildSearchRequest(FETCH_BATCH_SIZE, oldestIdxRef.current, 1);
      const data = await apiFetch(`${apiBase}/logs/search`, {
        method: 'POST',
        body: JSON.stringify(request),
      });
      
      if (data && data.length > 0) {
        setLogs(prevLogs => {
          const newLogs = [...data, ...prevLogs];
          // Prune from end if buffer exceeds max
          if (newLogs.length > bufferSize) {
            const pruned = newLogs.slice(0, bufferSize);
            setIsAtLiveEdge(false);
            return pruned;
          }
          return newLogs;
        });
        setHasMoreBefore(data.length >= FETCH_BATCH_SIZE);
      } else {
        setHasMoreBefore(false);
      }
    } catch (err) {
      console.error('Failed to fetch older logs:', err);
    } finally {
      setLoadingMore(false);
    }
  }, [apiBase, hasMoreBefore, loadingMore, bufferSize, buildSearchRequest]);

  // Fetch newer logs (forwards)
  const fetchNewerLogs = useCallback(async () => {
    if (!apiBase || isAtLiveEdge || loadingMore || newestIdxRef.current === null) return;

    setLoadingMore(true);
    try {
      const request = buildSearchRequest(FETCH_BATCH_SIZE, newestIdxRef.current, 0);
      const data = await apiFetch(`${apiBase}/logs/search`, {
        method: 'POST',
        body: JSON.stringify(request),
      });
      
      if (data && data.length > 0) {
        setLogs(prevLogs => {
          const newLogs = [...prevLogs, ...data];
          // Prune from start if buffer exceeds max
          if (newLogs.length > bufferSize) {
            const pruned = newLogs.slice(-bufferSize);
            setHasMoreBefore(true);
            return pruned;
          }
          return newLogs;
        });
        // Check if we've reached the live edge
        if (data.length < FETCH_BATCH_SIZE) {
          setIsAtLiveEdge(true);
        }
      } else {
        setIsAtLiveEdge(true);
      }
    } catch (err) {
      console.error('Failed to fetch newer logs:', err);
    } finally {
      setLoadingMore(false);
    }
  }, [apiBase, isAtLiveEdge, loadingMore, bufferSize, buildSearchRequest]);

  // Go to live edge
  const goToLiveEdge = useCallback(async () => {
    await loadInitialLogs();
    setAutoScroll(true);
  }, [loadInitialLogs]);

  // Initial load when authenticated
  useEffect(() => {
    if (isAuthenticated && initialized && apiBase && !isRealTime) {
      loadInitialLogs();
    }
  }, [isAuthenticated, initialized, apiBase, isRealTime]);

  // Log level filtering
  const levelPriority = useMemo(() => ({
    'Trace': 0, 'Debug': 1, 'Information': 2, 'Warning': 3, 'Error': 4, 'Critical': 5
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
    const fields = ['message', 'text', 'category', 'environment', 'application', 'pathname', 'machineName', 'host'];
    return fields.some(f => (log?.logData?.[f]?.toLowerCase() || '').includes(searchLower));
  }, [levelFilter, searchString, levelPriority]);

  const filteredLogs = useMemo(() => logs.filter(showRow), [logs, showRow]);

  // Measure container size
  useEffect(() => {
    const updateSize = () => {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect();
        setContainerSize({ width: rect.width, height: rect.height });
      }
    };
    
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

  // Scroll to bottom effect
  useEffect(() => {
    if (filteredLogs.length === 0 || containerSize.height === 0) return;
    
    const shouldScroll = scrollToBottomRef.current || (isRealTime && autoScroll && isAtLiveEdge);
    if (!shouldScroll) return;
    
    const timer = setTimeout(() => {
      if (isRealTime && herculesListRef.current) {
        herculesListRef.current.scrollToItem(filteredLogs.length - 1, 'end');
      } else if (!isRealTime && tableListRef.current) {
        tableListRef.current.scrollToItem(filteredLogs.length - 1, 'end');
      }
      scrollToBottomRef.current = false;
    }, 50);
    
    return () => clearTimeout(timer);
  }, [filteredLogs.length, isRealTime, autoScroll, containerSize.height, isAtLiveEdge]);

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

    if (!isAuthenticated || !initialized || !apiBase) return;

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
        .withUrl(`${apiBase}/loghub`, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      connectionRef.current = connection;

      connection.on('NewLogEntry', (log) => {
        // Only append if we're at the live edge
        if (!isAtLiveEdge) return;
        
        log._isNew = true;
        setLogs(prevLogs => {
          const newLogs = [...prevLogs, log];
          // Prune from start if buffer exceeds max
          if (newLogs.length > bufferSize) {
            setHasMoreBefore(true);
            return newLogs.slice(-bufferSize);
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
  }, [isRealTime, getAccessToken, isAuthenticated, initialized, apiBase, levelFilter, isAtLiveEdge, bufferSize]);

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
    await loadInitialLogs();
  };

  const handleRealTimeToggle = async (enabled) => {
    setIsRealTime(enabled);
    setFocusedLog(null);
    setAutoScroll(true);
    setIsAtLiveEdge(true);
    scrollToBottomRef.current = true;
    await loadInitialLogs();
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

  // Handle scroll for infinite loading
  const handleListScroll = useCallback(({ scrollOffset, scrollDirection }) => {
    const listRef = isRealTime ? herculesListRef : tableListRef;
    const rowHeight = isRealTime ? HERCULES_ROW_HEIGHT : TABLE_ROW_HEIGHT;
    
    if (!listRef.current) return;
    
    const visibleStartIndex = Math.floor(scrollOffset / rowHeight);
    const visibleEndIndex = visibleStartIndex + Math.ceil(containerSize.height / rowHeight);
    
    // Fetch older logs when near the top
    if (visibleStartIndex < SCROLL_THRESHOLD && hasMoreBefore && !loadingMore) {
      fetchOlderLogs();
    }
    
    // Fetch newer logs when near the bottom (only if not at live edge)
    if (!isAtLiveEdge && visibleEndIndex > filteredLogs.length - SCROLL_THRESHOLD && !loadingMore) {
      fetchNewerLogs();
    }
    
    // Update autoScroll based on position
    const contentHeight = filteredLogs.length * rowHeight;
    const listHeight = containerSize.height - (isRealTime ? 70 : 40);
    const maxScroll = contentHeight - listHeight;
    const isNearBottom = scrollOffset >= maxScroll - 50;
    
    if (isRealTime) {
      setAutoScroll(isNearBottom && isAtLiveEdge);
    }
  }, [isRealTime, containerSize.height, hasMoreBefore, isAtLiveEdge, loadingMore, filteredLogs.length, fetchOlderLogs, fetchNewerLogs]);

  const renderHerculesTerminal = () => {
    const listHeight = Math.max(containerSize.height - 70, 200);
    
    return (
      <div className="hercules-container">
        <div className="hercules-header">
          <span>═══ LIVE LOG STREAM ═══</span>
          <span className="hercules-status">{isConnected ? '● CONNECTED' : '○ DISCONNECTED'}</span>
          <span className="hercules-count">{filteredLogs.length} entries</span>
          {loadingMore && <span className="hercules-loading">Loading...</span>}
          {!isAtLiveEdge && (
            <button className="hercules-scroll-btn hercules-live-btn" onClick={goToLiveEdge}>
              ⚡ GO LIVE
            </button>
          )}
          {isAtLiveEdge && !autoScroll && (
            <button className="hercules-scroll-btn" onClick={() => {
              setAutoScroll(true);
              herculesListRef.current?.scrollToItem(filteredLogs.length - 1, 'end');
            }}>↓ SCROLL TO BOTTOM</button>
          )}
        </div>
        <div className="hercules-terminal">
          {hasMoreBefore && (
            <div className="hercules-more-indicator">↑ Scroll up for older logs</div>
          )}
          {containerSize.height > 0 && (
            <List
              ref={herculesListRef}
              height={listHeight}
              width={containerSize.width || 800}
              itemCount={filteredLogs.length}
              itemSize={HERCULES_ROW_HEIGHT}
              onScroll={handleListScroll}
              overscanCount={20}
            >
              {HerculesRow}
            </List>
          )}
          {filteredLogs.length === 0 && (
            <div className="hercules-waiting">{isConnected ? 'Waiting for log entries...' : 'Connecting...'}</div>
          )}
          {/* Blinking cursor at bottom when at live edge */}
          {isAtLiveEdge && filteredLogs.length > 0 && (
            <div className="hercules-cursor-line">
              <span className="hercules-cursor">▌</span>
            </div>
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
        
        {loadingMore && <div className="loading-more-indicator">Loading more...</div>}
        
        {containerSize.height > 0 && (
          <List
            ref={tableListRef}
            height={listHeight}
            width={containerSize.width || 800}
            itemCount={filteredLogs.length}
            itemSize={TABLE_ROW_HEIGHT}
            onScroll={handleListScroll}
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
          <span className="version-label">v1.1</span>
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
          <button onClick={loadInitialLogs} disabled={isRealTime} className="if-btn if-btn-secondary if-btn-sm">Clear</button>
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
            {isRealTime && !isAtLiveEdge && <span className="status-not-live">⏸ Paused</span>}
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
