import { useState, useEffect } from 'react';
import { apiService } from '../services/apiService';
import './ToolSelector.scss';

interface ToolSelectorProps {
  enabledTools: string[];
  onToolsChange: (tools: string[]) => void;
  onClose: () => void;
}

interface ServerInfo {
  name: string;
  url: string;
  toolCount: number;
  enabled: boolean;
}

export function ToolSelector({ enabledTools, onToolsChange, onClose }: ToolSelectorProps) {
  const [servers, setServers] = useState<ServerInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadServers = async () => {
      try {
        setLoading(true);
        const [serversData, toolsData] = await Promise.all([
          apiService.getServers(),
          apiService.getTools(),
        ]);

        // Count tools per server
        const toolCounts = new Map<string, number>();
        toolsData.tools.forEach((tool) => {
          const count = toolCounts.get(tool.serverName) || 0;
          toolCounts.set(tool.serverName, count + 1);
        });

        // Build server info list
        const serverList: ServerInfo[] = Object.entries(serversData.servers).map(([name, url]) => ({
          name,
          url: url as string,
          toolCount: toolCounts.get(name) || 0,
          enabled: enabledTools.includes(name),
        }));

        setServers(serverList);
        setError(null);
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to load tools';
        setError(message);
      } finally {
        setLoading(false);
      }
    };

    loadServers();
  }, [enabledTools]);

  const toggleServer = (serverName: string) => {
    const newTools = enabledTools.includes(serverName)
      ? enabledTools.filter((t) => t !== serverName)
      : [...enabledTools, serverName];
    onToolsChange(newTools);
  };

  const selectAll = () => {
    onToolsChange(servers.map((s) => s.name));
  };

  const selectNone = () => {
    onToolsChange([]);
  };

  return (
    <div className="if-modal-overlay" onClick={onClose}>
      <div className="if-modal tool-selector" onClick={(e) => e.stopPropagation()}>
        <div className="if-modal-header">
          <h3>MCP Tool Servers</h3>
          <button className="if-btn if-btn-ghost if-btn-sm" onClick={onClose}>
            ✕
          </button>
        </div>

        <div className="tool-selector-actions">
          <button className="if-btn if-btn-secondary if-btn-sm" onClick={selectAll}>
            Select All
          </button>
          <button className="if-btn if-btn-secondary if-btn-sm" onClick={selectNone}>
            Select None
          </button>
        </div>

        {loading && (
          <div className="tool-selector-loading">
            <div className="if-spinner"></div>
            <span>Loading tools...</span>
          </div>
        )}

        {error && <div className="tool-selector-error">{error}</div>}

        {!loading && !error && (
          <div className="if-modal-body server-list">
            {servers.map((server) => (
              <label key={server.name} className="server-item">
                <input
                  type="checkbox"
                  checked={enabledTools.includes(server.name)}
                  onChange={() => toggleServer(server.name)}
                />
                <div className="server-info">
                  <span className="server-name">{server.name}</span>
                  <span className="server-tools">
                    {server.toolCount} tool{server.toolCount !== 1 ? 's' : ''}
                  </span>
                </div>
              </label>
            ))}
          </div>
        )}

        <div className="if-modal-footer">
          <span className="if-text-muted">{enabledTools.length} server(s) enabled</span>
          <button className="if-btn if-btn-primary" onClick={onClose}>
            Done
          </button>
        </div>
      </div>
    </div>
  );
}
