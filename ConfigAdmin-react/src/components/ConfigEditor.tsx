import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { configApi, ConfigApiError } from '../services/configApi';
import type { ConfigEntry, ConfigEntryDto } from '../types/config';
import { useToast } from './Toast';
import { JsonEditor } from './JsonEditor';
import './ConfigEditor.css';

export function ConfigEditor() {
  const { idx } = useParams<{ idx: string }>();
  const navigate = useNavigate();
  const { showToast } = useToast();
  const isEditing = Boolean(idx);

  const [loading, setLoading] = useState(isEditing);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [realm, setRealm] = useState('');
  const [client, setClient] = useState('');
  const [enabled, setEnabled] = useState(true);
  const [userConfig, setUserConfig] = useState('');
  const [serviceConfig, setServiceConfig] = useState('');
  const [bootstrapConfig, setBootstrapConfig] = useState('');

  const [activeTab, setActiveTab] = useState<'bootstrap' | 'user' | 'service'>('bootstrap');

  useEffect(() => {
    if (isEditing && idx) {
      loadEntry(parseInt(idx, 10));
    }
  }, [idx, isEditing]);

  const loadEntry = async (entryIdx: number) => {
    try {
      setLoading(true);
      setError(null);
      const entry = await configApi.getByIdx(entryIdx);
      populateForm(entry);
    } catch (err) {
      const message = err instanceof ConfigApiError ? err.message : 'Failed to load configuration';
      setError(message);
      showToast(message, 'error');
    } finally {
      setLoading(false);
    }
  };

  const populateForm = (entry: ConfigEntry) => {
    setRealm(entry.realm);
    setClient(entry.client);
    setEnabled(entry.enabled);
    setUserConfig(entry.userConfig ? JSON.stringify(entry.userConfig, null, 2) : '');
    setServiceConfig(entry.serviceConfig ? JSON.stringify(entry.serviceConfig, null, 2) : '');
    setBootstrapConfig(entry.bootstrapConfig ? JSON.stringify(entry.bootstrapConfig, null, 2) : '');
  };

  const validateJson = (json: string, fieldName: string): boolean => {
    if (!json.trim()) return true; // Empty is valid
    try {
      JSON.parse(json);
      return true;
    } catch {
      showToast(`Invalid JSON in ${fieldName}`, 'error');
      return false;
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!realm.trim()) {
      showToast('Realm is required', 'error');
      return;
    }

    if (!client.trim()) {
      showToast('Client is required', 'error');
      return;
    }

    if (!validateJson(userConfig, 'User Config')) return;
    if (!validateJson(serviceConfig, 'Service Config')) return;
    if (!validateJson(bootstrapConfig, 'Bootstrap Config')) return;

    const dto: ConfigEntryDto = {
      realm: realm.trim(),
      client: client.trim(),
      enabled,
      userConfig: userConfig.trim(),
      serviceConfig: serviceConfig.trim(),
      bootstrapConfig: bootstrapConfig.trim()
    };

    try {
      setSaving(true);
      if (isEditing && idx) {
        await configApi.update(parseInt(idx, 10), dto);
        showToast('Configuration updated successfully', 'success');
      } else {
        await configApi.create(dto);
        showToast('Configuration created successfully', 'success');
      }
      navigate('/');
    } catch (err) {
      const message = err instanceof ConfigApiError ? err.message : 'Failed to save configuration';
      showToast(message, 'error');
    } finally {
      setSaving(false);
    }
  };

  const formatJson = (setter: React.Dispatch<React.SetStateAction<string>>, value: string) => {
    if (!value.trim()) return;
    try {
      const parsed = JSON.parse(value);
      setter(JSON.stringify(parsed, null, 2));
    } catch {
      showToast('Cannot format invalid JSON', 'error');
    }
  };

  if (loading) {
    return (
      <div className="loading">
        <div className="if-spinner" />
      </div>
    );
  }

  if (error && isEditing) {
    return (
      <div className="if-card">
        <div className="if-card-body">
          <div className="empty-state">
            <div className="empty-state-icon">⚠️</div>
            <h3>Error Loading Configuration</h3>
            <p className="if-text-muted">{error}</p>
            <Link to="/" className="if-btn if-btn-primary" style={{ marginTop: '1rem' }}>
              Back to List
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="config-editor">
      <div className="config-editor-header">
        <Link to="/" className="if-btn if-btn-ghost">
          ← Back
        </Link>
        <h1>{isEditing ? 'Edit Configuration' : 'New Configuration'}</h1>
      </div>

      <form onSubmit={handleSubmit}>
        <div className="if-card">
          <div className="if-card-header">
            <h3>Basic Information</h3>
          </div>
          <div className="if-card-body">
            <div className="form-grid">
              <div className="if-form-group">
                <label className="if-form-label" htmlFor="realm">Realm *</label>
                <input
                  id="realm"
                  type="text"
                  className="if-form-input"
                  value={realm}
                  onChange={e => setRealm(e.target.value)}
                  placeholder="e.g., longmanrd_dev"
                  required
                />
              </div>

              <div className="if-form-group">
                <label className="if-form-label" htmlFor="client">Client *</label>
                <input
                  id="client"
                  type="text"
                  className="if-form-input"
                  value={client}
                  onChange={e => setClient(e.target.value)}
                  placeholder="e.g., dev-login"
                  required
                />
              </div>
            </div>

            <div className="if-form-group" style={{ marginTop: 'var(--if-space-md)' }}>
              <label className="toggle-label">
                <input
                  type="checkbox"
                  checked={enabled}
                  onChange={e => setEnabled(e.target.checked)}
                />
                <span className="toggle-switch" />
                <span>Enabled</span>
              </label>
            </div>
          </div>
        </div>

        <div className="if-card">
          <div className="if-card-header">
            <div className="if-tabs">
              <button
                type="button"
                className={`if-tab ${activeTab === 'bootstrap' ? 'active' : ''}`}
                onClick={() => setActiveTab('bootstrap')}
              >
                Bootstrap Config
              </button>
              <button
                type="button"
                className={`if-tab ${activeTab === 'user' ? 'active' : ''}`}
                onClick={() => setActiveTab('user')}
              >
                User Config
              </button>
              <button
                type="button"
                className={`if-tab ${activeTab === 'service' ? 'active' : ''}`}
                onClick={() => setActiveTab('service')}
              >
                Service Config
              </button>
            </div>
          </div>
          <div className="if-card-body">
            {activeTab === 'bootstrap' && (
              <JsonEditor
                value={bootstrapConfig}
                onChange={setBootstrapConfig}
                onFormat={() => formatJson(setBootstrapConfig, bootstrapConfig)}
                placeholder="Bootstrap configuration JSON..."
                hint="Returned when cfg=bootstrap. Contains OIDC settings, logger service URL, etc."
              />
            )}
            {activeTab === 'user' && (
              <JsonEditor
                value={userConfig}
                onChange={setUserConfig}
                onFormat={() => formatJson(setUserConfig, userConfig)}
                placeholder="User configuration JSON..."
                hint="Configuration values for user-facing clients (type=user)."
              />
            )}
            {activeTab === 'service' && (
              <JsonEditor
                value={serviceConfig}
                onChange={setServiceConfig}
                onFormat={() => formatJson(setServiceConfig, serviceConfig)}
                placeholder="Service configuration JSON..."
                hint="Configuration values for backend services (type=service). May contain secrets."
              />
            )}
          </div>
        </div>

        <div className="form-actions">
          <Link to="/" className="if-btn if-btn-secondary">
            Cancel
          </Link>
          <button type="submit" className="if-btn if-btn-primary if-btn-lg" disabled={saving}>
            {saving ? 'Saving...' : (isEditing ? 'Update Configuration' : 'Create Configuration')}
          </button>
        </div>
      </form>
    </div>
  );
}
