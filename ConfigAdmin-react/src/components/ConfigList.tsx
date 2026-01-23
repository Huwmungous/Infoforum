import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { configApi, ConfigApiError } from '../services/configApi';
import type { ConfigEntry } from '../types/config';
import { useToast } from './Toast';
import { ConfirmModal } from './ConfirmModal';
import './ConfigList.css';

export function ConfigList() {
  const [entries, setEntries] = useState<ConfigEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ConfigEntry | null>(null);
  const [filter, setFilter] = useState('');
  const { showToast } = useToast();

  const loadEntries = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await configApi.getBatch(0, 100, true);
      setEntries(response.entries);
      setTotal(response.total);
    } catch (err) {
      const message = err instanceof ConfigApiError ? err.message : 'Failed to load configurations';
      setError(message);
      showToast(message, 'error');
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    loadEntries();
  }, [loadEntries]);

  const handleToggleEnabled = async (entry: ConfigEntry) => {
    try {
      await configApi.setEnabled(entry.idx, !entry.enabled);
      setEntries(prev => 
        prev.map(e => e.idx === entry.idx ? { ...e, enabled: !e.enabled } : e)
      );
      showToast(`Entry ${entry.enabled ? 'disabled' : 'enabled'} successfully`, 'success');
    } catch (err) {
      const message = err instanceof ConfigApiError ? err.message : 'Failed to update entry';
      showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    
    try {
      await configApi.delete(deleteTarget.idx);
      setEntries(prev => prev.filter(e => e.idx !== deleteTarget.idx));
      setTotal(prev => prev - 1);
      showToast('Entry deleted successfully', 'success');
    } catch (err) {
      const message = err instanceof ConfigApiError ? err.message : 'Failed to delete entry';
      showToast(message, 'error');
    } finally {
      setDeleteTarget(null);
    }
  };

  const filteredEntries = entries.filter(entry => {
    if (!filter) return true;
    const search = filter.toLowerCase();
    return (
      entry.realm.toLowerCase().includes(search) ||
      entry.client.toLowerCase().includes(search)
    );
  });

  if (loading) {
    return (
      <div className="loading">
        <div className="if-spinner" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="if-card">
        <div className="if-card-body">
          <div className="empty-state">
            <div className="empty-state-icon">⚠️</div>
            <h3>Error Loading Configurations</h3>
            <p className="if-text-muted">{error}</p>
            <button className="if-btn if-btn-primary" onClick={loadEntries} style={{ marginTop: '1rem' }}>
              Retry
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="config-list">
      <div className="config-list-header">
        <div>
          <h1>Configurations</h1>
          <p className="if-text-muted">{total} entries total</p>
        </div>
        <div className="config-list-actions">
          <input
            type="text"
            className="if-form-input"
            placeholder="Filter by realm or client..."
            value={filter}
            onChange={e => setFilter(e.target.value)}
          />
          <Link to="/new" className="if-btn if-btn-primary">
            + New Entry
          </Link>
        </div>
      </div>

      {filteredEntries.length === 0 ? (
        <div className="if-card">
          <div className="if-card-body">
            <div className="empty-state">
              <div className="empty-state-icon">📋</div>
              <h3>No configurations found</h3>
              <p className="if-text-muted">
                {filter ? 'No entries match your filter.' : 'Create your first configuration entry.'}
              </p>
              {!filter && (
                <Link to="/new" className="if-btn if-btn-primary" style={{ marginTop: '1rem' }}>
                  Create Entry
                </Link>
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="if-card table-container">
          <table className="if-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Realm</th>
                <th>Client</th>
                <th>Configs</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredEntries.map(entry => (
                <tr key={entry.idx} className={!entry.enabled ? 'row-disabled' : ''}>
                  <td>
                    <span className={`if-badge ${entry.enabled ? 'if-badge-enabled' : 'if-badge-disabled'}`}>
                      {entry.enabled ? '● Enabled' : '○ Disabled'}
                    </span>
                  </td>
                  <td>
                    <code className="if-text-mono">{entry.realm}</code>
                  </td>
                  <td>
                    <code className="if-text-mono">{entry.client}</code>
                  </td>
                  <td>
                    <div className="config-badges">
                      {entry.bootstrapConfig && <span className="config-badge">Bootstrap</span>}
                      {entry.userConfig && <span className="config-badge">User</span>}
                      {entry.serviceConfig && <span className="config-badge">Service</span>}
                    </div>
                  </td>
                  <td>
                    <div className="action-buttons">
                      <Link to={`/edit/${entry.idx}`} className="if-btn if-btn-ghost if-btn-sm">
                        Edit
                      </Link>
                      <button 
                        className={`if-btn if-btn-sm ${entry.enabled ? 'if-btn-secondary' : 'if-btn-success'}`}
                        onClick={() => handleToggleEnabled(entry)}
                      >
                        {entry.enabled ? 'Disable' : 'Enable'}
                      </button>
                      <button 
                        className="if-btn if-btn-danger if-btn-sm"
                        onClick={() => setDeleteTarget(entry)}
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {deleteTarget && (
        <ConfirmModal
          title="Delete Configuration"
          message={`Are you sure you want to delete the configuration for "${deleteTarget.realm} / ${deleteTarget.client}"? This action cannot be undone.`}
          confirmLabel="Delete"
          confirmVariant="danger"
          onConfirm={handleDelete}
          onCancel={() => setDeleteTarget(null)}
        />
      )}
    </div>
  );
}
