import { useState } from 'react';
import { useAuth } from '@if/web-common-react';
import { apiService } from '../services/apiService';
import { GitCredentialType } from '../types';
import './AddRepoModal.scss';

interface AddRepoModalProps {
  onClose: () => void;
  onRepoAdded: () => void;
}

export function AddRepoModal({ onClose, onRepoAdded }: AddRepoModalProps) {
  const auth = useAuth();
  const userId = auth.user?.profile?.sub;

  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [credentialType, setCredentialType] = useState<GitCredentialType>(GitCredentialType.PersonalAccessToken);
  const [credential, setCredential] = useState('');
  const [cloning, setCloning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [progress, setProgress] = useState<string | null>(null);

  // Auto-derive name from URL
  const handleUrlChange = (newUrl: string) => {
    setUrl(newUrl);
    if (!name || name === deriveName(url)) {
      setName(deriveName(newUrl));
    }
  };

  const deriveName = (repoUrl: string): string => {
    const match = repoUrl.match(/\/([^/]+?)(?:\.git)?$/);
    return match ? match[1] : '';
  };

  const handleSubmit = async () => {
    if (!userId || !name.trim() || !url.trim()) return;

    setCloning(true);
    setError(null);
    setProgress('Cloning repository...');

    try {
      await apiService.cloneRepository(userId, name.trim(), url.trim(), credentialType, credential);
      setProgress('Clone complete!');
      onRepoAdded();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Clone failed';
      setError(message);
      setProgress(null);
    } finally {
      setCloning(false);
    }
  };

  return (
    <div className="if-modal-overlay" onClick={onClose}>
      <div className="if-modal add-repo-modal" onClick={e => e.stopPropagation()}>
        <div className="if-modal-header">
          <h3>Add Git Repository</h3>
          <button className="if-btn if-btn-ghost if-btn-sm" onClick={onClose}>
            ✕
          </button>
        </div>

        <div className="if-modal-body add-repo-form">
          <div className="form-group">
            <label htmlFor="repo-url">Repository URL</label>
            <input
              id="repo-url"
              className="if-form-input"
              type="text"
              value={url}
              onChange={e => handleUrlChange(e.target.value)}
              placeholder="https://github.com/user/repo.git"
              disabled={cloning}
            />
          </div>

          <div className="form-group">
            <label htmlFor="repo-name">Display Name</label>
            <input
              id="repo-name"
              className="if-form-input"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="My Project"
              disabled={cloning}
            />
          </div>

          <div className="form-group">
            <label htmlFor="cred-type">Authentication</label>
            <select
              id="cred-type"
              className="if-form-input"
              value={credentialType}
              onChange={e => setCredentialType(Number(e.target.value) as GitCredentialType)}
              disabled={cloning}
            >
              <option value={GitCredentialType.PersonalAccessToken}>Personal Access Token</option>
              <option value={GitCredentialType.SshKey}>SSH Key Path</option>
            </select>
          </div>

          <div className="form-group">
            <label htmlFor="credential">
              {credentialType === GitCredentialType.PersonalAccessToken
                ? 'Personal Access Token'
                : 'SSH Key File Path'}
            </label>
            <input
              id="credential"
              className="if-form-input"
              type={credentialType === GitCredentialType.PersonalAccessToken ? 'password' : 'text'}
              value={credential}
              onChange={e => setCredential(e.target.value)}
              placeholder={
                credentialType === GitCredentialType.PersonalAccessToken
                  ? 'ghp_xxxxxxxxxxxx'
                  : '/home/user/.ssh/id_rsa'
              }
              disabled={cloning}
            />
            <span className="form-hint">
              {credentialType === GitCredentialType.PersonalAccessToken
                ? 'A PAT with repo read/write access.'
                : 'Absolute path to the SSH private key on the server.'}
            </span>
          </div>

          {error && <div className="add-repo-error">{error}</div>}
          {progress && <div className="add-repo-progress">{progress}</div>}
        </div>

        <div className="if-modal-footer">
          <button className="if-btn if-btn-secondary" onClick={onClose} disabled={cloning}>
            Cancel
          </button>
          <button
            className="if-btn if-btn-primary"
            onClick={handleSubmit}
            disabled={cloning || !name.trim() || !url.trim()}
          >
            {cloning ? 'Cloning...' : 'Clone Repository'}
          </button>
        </div>
      </div>
    </div>
  );
}
