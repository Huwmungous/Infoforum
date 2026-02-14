import { useState, useEffect, useRef, useCallback } from 'react';
import { useAuth } from '@if/web-common-react';
import { apiService } from '../services/apiService';
import { AddRepoModal } from './AddRepoModal';
import type { GitRepository, ConversationRepo } from '../types';
import { ProjectType } from '../types';
import './RepoSelector.scss';

interface RepoSelectorProps {
  conversationId: string | null;
  conversationTitle?: string | null;
}

const projectTypeLabel = (pt: ProjectType): string => {
  switch (pt) {
    case ProjectType.DotNet: return '.NET';
    case ProjectType.ReactTypeScript: return 'React/TS';
    case ProjectType.AngularTypeScript: return 'Angular/TS';
    case ProjectType.Delphi: return 'Delphi';
    default: return 'Unknown';
  }
};

const projectTypeIcon = (pt: ProjectType): string => {
  switch (pt) {
    case ProjectType.DotNet: return '🟣';
    case ProjectType.ReactTypeScript: return '⚛️';
    case ProjectType.AngularTypeScript: return '🅰️';
    case ProjectType.Delphi: return '🔷';
    default: return '📁';
  }
};

export function RepoSelector({ conversationId, conversationTitle }: RepoSelectorProps) {
  const auth = useAuth();
  const userId = auth.user?.profile?.sub;

  const [isOpen, setIsOpen] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);
  const [repos, setRepos] = useState<GitRepository[]>([]);
  const [conversationRepos, setConversationRepos] = useState<ConversationRepo[]>([]);
  const [loading, setLoading] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const enabledCount = conversationRepos.filter(cr => cr.enabled).length;

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Load user repos
  const loadRepos = useCallback(async () => {
    if (!userId) return;
    try {
      setLoading(true);
      const userRepos = await apiService.getRepositories(userId);
      setRepos(userRepos);
    } catch {
      // Silent fail for repo list
    } finally {
      setLoading(false);
    }
  }, [userId]);

  // Load conversation-repo links when conversation changes
  const loadConversationRepos = useCallback(async () => {
    if (!userId || !conversationId) {
      setConversationRepos([]);
      return;
    }
    try {
      const links = await apiService.getConversationRepos(conversationId, userId);
      setConversationRepos(links);
    } catch {
      setConversationRepos([]);
    }
  }, [userId, conversationId]);

  useEffect(() => {
    loadRepos();
  }, [loadRepos]);

  useEffect(() => {
    loadConversationRepos();
  }, [loadConversationRepos]);

  const handleLinkRepo = async (repoId: string) => {
    if (!userId || !conversationId) return;
    setActionInProgress(repoId);
    try {
      await apiService.linkRepoToConversation(
        conversationId, repoId, conversationTitle ?? conversationId, userId
      );
      await loadConversationRepos();
    } catch (err) {
      console.error('Failed to link repo:', err);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleToggleEnabled = async (repoId: string, currentEnabled: boolean) => {
    if (!userId || !conversationId) return;
    setActionInProgress(repoId);
    try {
      await apiService.setRepoEnabled(conversationId, repoId, !currentEnabled, userId);
      setConversationRepos(prev =>
        prev.map(cr => cr.repositoryId === repoId ? { ...cr, enabled: !currentEnabled } : cr)
      );
    } catch (err) {
      console.error('Failed to toggle repo:', err);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleUnlinkRepo = async (repoId: string) => {
    if (!userId || !conversationId) return;
    setActionInProgress(repoId);
    try {
      await apiService.unlinkRepo(conversationId, repoId, userId);
      await loadConversationRepos();
    } catch (err) {
      console.error('Failed to unlink repo:', err);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleDeleteRepo = async (repoId: string) => {
    if (!userId) return;
    if (!confirm('Remove this repository? This will delete the local clone.')) return;
    setActionInProgress(repoId);
    try {
      await apiService.deleteRepository(repoId, userId);
      await loadRepos();
      await loadConversationRepos();
    } catch (err) {
      console.error('Failed to delete repo:', err);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleRepoAdded = () => {
    loadRepos();
    setShowAddModal(false);
  };

  // Build the display: linked repos first, then unlinked repos
  const linkedRepoIds = new Set(conversationRepos.map(cr => cr.repositoryId));
  const unlinkedRepos = repos.filter(r => !linkedRepoIds.has(r.id));

  return (
    <div className="repo-selector" ref={dropdownRef}>
      <button
        className="if-btn if-btn-secondary repo-selector-toggle"
        onClick={() => setIsOpen(!isOpen)}
        title="Manage repository context"
      >
        <svg className="repo-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.4 5.4 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65S8.93 17.38 9 18v4" />
          <path d="M9 18c-4.51 2-5-2-7-2" />
        </svg>
        Repos ({enabledCount})
      </button>

      {isOpen && (
        <div className="repo-dropdown">
          <div className="repo-dropdown-header">
            <h4>Repository Context</h4>
            <button
              className="if-btn if-btn-primary if-btn-sm"
              onClick={() => setShowAddModal(true)}
            >
              + Add Repo
            </button>
          </div>

          {!conversationId && (
            <div className="repo-dropdown-hint">
              Start a conversation to link repositories.
            </div>
          )}

          {loading && (
            <div className="repo-dropdown-loading">
              <div className="if-spinner" />
            </div>
          )}

          {/* Linked repos for this conversation */}
          {conversationRepos.length > 0 && (
            <div className="repo-section">
              <div className="repo-section-label">Linked to this conversation</div>
              {conversationRepos.map(cr => (
                <div key={cr.repositoryId} className="repo-item linked">
                  <label className="repo-item-toggle">
                    <input
                      type="checkbox"
                      checked={cr.enabled}
                      disabled={actionInProgress === cr.repositoryId}
                      onChange={() => handleToggleEnabled(cr.repositoryId, cr.enabled)}
                    />
                    <div className="repo-item-info">
                      <span className="repo-item-name">
                        {projectTypeIcon(cr.detectedProjectType)} {cr.name}
                      </span>
                      <span className="repo-item-meta">
                        {projectTypeLabel(cr.detectedProjectType)} · {cr.branchName}
                      </span>
                    </div>
                  </label>
                  <button
                    className="repo-item-action"
                    onClick={() => handleUnlinkRepo(cr.repositoryId)}
                    disabled={actionInProgress === cr.repositoryId}
                    title="Unlink from conversation"
                  >
                    ✕
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Available repos not yet linked */}
          {conversationId && unlinkedRepos.length > 0 && (
            <div className="repo-section">
              <div className="repo-section-label">Available repositories</div>
              {unlinkedRepos.map(repo => (
                <div key={repo.id} className="repo-item available">
                  <div className="repo-item-info">
                    <span className="repo-item-name">
                      {projectTypeIcon(repo.detectedProjectType)} {repo.name}
                    </span>
                    <span className="repo-item-meta">
                      {projectTypeLabel(repo.detectedProjectType)} · {repo.defaultBranch}
                    </span>
                  </div>
                  <button
                    className="if-btn if-btn-secondary if-btn-sm"
                    onClick={() => handleLinkRepo(repo.id)}
                    disabled={actionInProgress === repo.id}
                  >
                    {actionInProgress === repo.id ? '...' : 'Link'}
                  </button>
                  <button
                    className="repo-item-action danger"
                    onClick={() => handleDeleteRepo(repo.id)}
                    disabled={actionInProgress === repo.id}
                    title="Remove repository"
                  >
                    🗑️
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Show all repos when no conversation is selected */}
          {!conversationId && repos.length > 0 && (
            <div className="repo-section">
              <div className="repo-section-label">Your repositories</div>
              {repos.map(repo => (
                <div key={repo.id} className="repo-item available">
                  <div className="repo-item-info">
                    <span className="repo-item-name">
                      {projectTypeIcon(repo.detectedProjectType)} {repo.name}
                    </span>
                    <span className="repo-item-meta">
                      {projectTypeLabel(repo.detectedProjectType)} · {repo.defaultBranch}
                    </span>
                  </div>
                  <button
                    className="repo-item-action danger"
                    onClick={() => handleDeleteRepo(repo.id)}
                    disabled={actionInProgress === repo.id}
                    title="Remove repository"
                  >
                    🗑️
                  </button>
                </div>
              ))}
            </div>
          )}

          {repos.length === 0 && !loading && (
            <div className="repo-dropdown-empty">
              No repositories added yet. Click &quot;+ Add Repo&quot; to clone one.
            </div>
          )}
        </div>
      )}

      {showAddModal && (
        <AddRepoModal
          onClose={() => setShowAddModal(false)}
          onRepoAdded={handleRepoAdded}
        />
      )}
    </div>
  );
}
