import React, { useState, useMemo, useEffect } from 'react';
import { useAppContext, useAuth } from '@if/web-common-react';

/**
 * TokenDisplay component
 * Displays authenticated user information and tokens
 */

// At the top of Tokendisplay.jsx
const logo = new URL('/IF-Logo.png', import.meta.url).href;
 
const TokenDisplay = () => {
  const { createLogger } = useAppContext();
  const { user, signout, renewToken } = useAuth();
  const [copiedField, setCopiedField] = useState(null);
  const [renewing, setRenewing] = useState(false);

  // Create logger for this component
  const logger = useMemo(() => createLogger('TokenDisplay'), []);

  // Force re-render every second for live countdown
  const [, setTick] = useState(0);
  useEffect(() => {
    const interval = setInterval(() => setTick(t => t + 1), 1000);
    return () => clearInterval(interval);
  }, []);

  const copyToClipboard = async (text, fieldName) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(fieldName);
      setTimeout(() => setCopiedField(null), 2000);

      // Log successful copy
      logger.info(`Token copied to clipboard: ${JSON.stringify({
        fieldName,
        userId: user?.profile?.sub,
        username: user?.profile?.preferred_username || user?.profile?.email,
        tokenLength: text?.length || 0
      })}`);

    } catch (err) {
      console.error('Failed to copy:', err);
      logger.error(`Failed to copy token to clipboard: ${fieldName}`, err);
    }
  };

  const handleRenewToken = async () => {
    try {
      setRenewing(true);
      await renewToken();
      alert('Token renewed successfully!');
    } catch (err) {
      alert(`Failed to renew token: ${err.message}`);
    } finally {
      setRenewing(false);
    }
  };

  const decodeJWT = (token) => {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch (err) {
      return null;
    }
  };

  const formatExpiration = (exp) => {
    if (!exp) return { text: 'Unknown', status: 'unknown' };
    const date = new Date(exp * 1000);
    const now = new Date();
    const diff = date - now;

    if (diff < 0) {
      const expiredAgo = Math.abs(diff);
      const mins = Math.floor(expiredAgo / 60000);
      const secs = Math.floor((expiredAgo % 60000) / 1000);
      return {
        text: `Expired ${mins}m ${secs}s ago`,
        status: 'expired',
        expiresAt: date.toLocaleString()
      };
    }

    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    const seconds = Math.floor((diff % 60000) / 1000);

    const countdown = hours > 0
      ? `${hours}h ${minutes}m ${seconds}s`
      : `${minutes}m ${seconds}s`;

    const status = diff < 60000 ? 'critical' : diff < 300000 ? 'warning' : 'ok';

    return {
      text: countdown,
      status,
      expiresAt: date.toLocaleString()
    };
  };

  const CopyIcon = ({ copied }) => (
    <svg
      className={`w-5 h-5 ${copied ? 'text-green-600' : 'text-if-medium hover:text-if-dark'}`}
      fill="none"
      stroke="currentColor"
      viewBox="0 0 24 24"
    >
      {copied ? (
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
      ) : (
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"
        />
      )}
    </svg>
  );

  const accessTokenDecoded = user?.access_token ? decodeJWT(user.access_token) : null;
  const idTokenDecoded = user?.id_token ? decodeJWT(user.id_token) : null;

  return (
    <div className="min-h-screen bg-if-window">
      {/* Header */}
      <header className="bg-[#333] text-if-window px-6 py-3 flex justify-between items-center">
        <div className="flex items-center gap-3">
          <img src={logo} alt="IF" className="w-12 h-12" />
          <h1 className="text-xl font-medium">Token Display</h1>
          <span className="text-sm opacity-70">v1.0</span>
        </div>
        <button
          onClick={handleRenewToken}
          disabled={renewing}
          className="px-4 py-2 bg-if-hl-medium text-white rounded shadow-if-lg border border-if-dark hover:bg-if-hl-dark disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {renewing ? 'Renewing...' : 'Renew Token'}
        </button>
      </header>

      <div className="max-w-7xl mx-auto p-6">
        {/* Authentication Status Card */}
        <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 mb-6">
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-xl font-medium text-if-dark">Authentication Successful</h2>
          </div>

          {/* User Identity */}
          {user?.profile && (
            <div className="mb-4 p-4 bg-if-light/10 border border-if-light/30 rounded-lg">
              <h3 className="font-medium text-if-dark mb-2">User Identity</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                <div>
                  <code className="text-if-medium">preferred_username:</code>{' '}
                  <span className="font-medium text-if-dark">
                    {user.profile.preferred_username || 'N/A'}
                  </span>
                </div>
                <div>
                  <code className="text-if-medium">email:</code>{' '}
                  <span className="font-medium text-if-dark">{user.profile.email || 'N/A'}</span>
                </div>
                <div>
                  <code className="text-if-medium">sub:</code>{' '}
                  <span className="font-medium text-if-dark text-xs">{user.profile.sub || 'N/A'}</span>
                </div>
                {user.scopes && user.scopes.length > 0 && (
                  <div>
                    <code className="text-if-medium">scope:</code>{' '}
                    <span className="font-medium text-if-dark">{user.scopes.join(' ')}</span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* SSO Session */}
          {user && (
            <div className="mb-4 p-4 bg-if-medium/10 border border-if-medium/30 rounded-lg">
              <h3 className="font-medium text-if-dark mb-2">SSO Session</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                {(idTokenDecoded?.auth_time || accessTokenDecoded?.auth_time) && (
                  <div>
                    <code className="text-if-medium">auth_time:</code>{' '}
                    <span className="font-medium text-if-dark">
                      {new Date((idTokenDecoded?.auth_time || accessTokenDecoded?.auth_time) * 1000).toLocaleString()}
                    </span>
                  </div>
                )}
                {(idTokenDecoded?.auth_time || accessTokenDecoded?.auth_time) && (() => {
                  const authTime = (idTokenDecoded?.auth_time || accessTokenDecoded?.auth_time) * 1000;
                  const elapsed = Date.now() - authTime;
                  const hours = Math.floor(elapsed / 3600000);
                  const minutes = Math.floor((elapsed % 3600000) / 60000);
                  const seconds = Math.floor((elapsed % 60000) / 1000);
                  return (
                    <div>
                      <span className="text-if-medium">Session Duration:</span>{' '}
                      <span className="font-mono font-medium text-if-dark">
                        {hours > 0 ? `${hours}h ${minutes}m ${seconds}s` : `${minutes}m ${seconds}s`}
                      </span>
                    </div>
                  );
                })()}
                {(idTokenDecoded?.session_state || accessTokenDecoded?.session_state) && (
                  <div className="col-span-2">
                    <code className="text-if-medium">session_state:</code>{' '}
                    <span className="font-mono text-xs text-if-dark">
                      {idTokenDecoded?.session_state || accessTokenDecoded?.session_state}
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Current Tokens */}
          {user && (
            <div className="mb-4 p-4 bg-if-hl-light/10 border border-if-hl-light/30 rounded-lg">
              <h3 className="font-medium text-if-dark mb-2">Current Tokens</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                <div>
                  <code className="text-if-medium">expired:</code>{' '}
                  <span className={`font-medium ${user.expired ? 'text-if-hl-dark' : 'text-green-600'}`}>
                    {user.expired ? 'true' : 'false'}
                  </span>
                </div>
                {user.token_type && (
                  <div>
                    <code className="text-if-medium">token_type:</code>{' '}
                    <span className="font-medium text-if-dark">{user.token_type}</span>
                  </div>
                )}
                {(idTokenDecoded?.iat || accessTokenDecoded?.iat) && (
                  <div>
                    <code className="text-if-medium">iat:</code>{' '}
                    <span className="font-medium text-if-dark">
                      {new Date((accessTokenDecoded?.iat || idTokenDecoded?.iat) * 1000).toLocaleString()}
                    </span>
                  </div>
                )}
                {(idTokenDecoded?.iat || accessTokenDecoded?.iat) && (() => {
                  const iatTime = (accessTokenDecoded?.iat || idTokenDecoded?.iat) * 1000;
                  const elapsed = Date.now() - iatTime;
                  const hours = Math.floor(elapsed / 3600000);
                  const minutes = Math.floor((elapsed % 3600000) / 60000);
                  const seconds = Math.floor((elapsed % 60000) / 1000);
                  return (
                    <div>
                      <span className="text-if-medium">Token Age:</span>{' '}
                      <span className="font-mono font-medium text-if-dark">
                        {hours > 0 ? `${hours}h ${minutes}m ${seconds}s` : `${minutes}m ${seconds}s`}
                      </span>
                    </div>
                  );
                })()}
                {accessTokenDecoded?.exp && (() => {
                  const expInfo = formatExpiration(accessTokenDecoded.exp);
                  const statusColors = {
                    ok: 'text-green-600',
                    warning: 'text-if-hl-medium',
                    critical: 'text-if-hl-dark animate-pulse',
                    expired: 'text-if-hl-dark',
                    unknown: 'text-if-dark'
                  };
                  return (
                    <>
                      <div>
                        <code className="text-if-medium">access_token.exp:</code>{' '}
                        <span className="font-medium text-if-dark">{expInfo.expiresAt}</span>
                      </div>
                      <div>
                        <span className="text-if-medium">Access Token Countdown:</span>{' '}
                        <span className={`font-mono font-bold ${statusColors[expInfo.status]}`}>
                          {expInfo.text}
                        </span>
                      </div>
                    </>
                  );
                })()}
                {idTokenDecoded?.exp && (() => {
                  const expInfo = formatExpiration(idTokenDecoded.exp);
                  const statusColors = {
                    ok: 'text-green-600',
                    warning: 'text-if-hl-medium',
                    critical: 'text-if-hl-dark animate-pulse',
                    expired: 'text-if-hl-dark',
                    unknown: 'text-if-dark'
                  };
                  return (
                    <>
                      <div>
                        <code className="text-if-medium">id_token.exp:</code>{' '}
                        <span className="font-medium text-if-dark">{expInfo.expiresAt}</span>
                      </div>
                      <div>
                        <span className="text-if-medium">ID Token Countdown:</span>{' '}
                        <span className={`font-mono font-bold ${statusColors[expInfo.status]}`}>
                          {expInfo.text}
                        </span>
                      </div>
                    </>
                  );
                })()}
              </div>
            </div>
          )}

          {/* Token Status */}
          <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
            <div className="flex items-center text-sm text-green-800">
              <svg className="w-5 h-5 mr-2" fill="currentColor" viewBox="0 0 20 20">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
              <span>
                Automatic token renewal is enabled. Tokens will be refreshed automatically before
                expiration.
              </span>
            </div>
          </div>
        </div>

        {/* Access Token */}
        {user?.access_token && (
          <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 mb-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-medium text-if-dark">Access Token</h2>
              <button
                onClick={() => copyToClipboard(user.access_token, 'access_token')}
                className="flex items-center gap-2 px-3 py-2 bg-if-window hover:bg-if-light/10 border border-if-medium/30 rounded transition-colors"
                title="Copy access token"
              >
                <CopyIcon copied={copiedField === 'access_token'} />
                <span className="text-sm text-if-dark">
                  {copiedField === 'access_token' ? 'Copied!' : 'Copy'}
                </span>
              </button>
            </div>
            <div className="bg-if-window p-4 rounded-lg mb-4 overflow-x-auto border border-if-medium/20">
              <code className="text-sm break-all text-if-dark">{user.access_token}</code>
            </div>
            {accessTokenDecoded && (
              <div>
                <h3 className="font-medium text-if-dark mb-2">Decoded Payload:</h3>
                <pre className="bg-if-window p-4 rounded-lg overflow-x-auto text-sm border border-if-medium/20 text-if-dark">
                  {JSON.stringify(accessTokenDecoded, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}

        {/* ID Token */}
        {user?.id_token && (
          <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 mb-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-medium text-if-dark">ID Token</h2>
              <button
                onClick={() => copyToClipboard(user.id_token, 'id_token')}
                className="flex items-center gap-2 px-3 py-2 bg-if-window hover:bg-if-light/10 border border-if-medium/30 rounded transition-colors"
                title="Copy ID token"
              >
                <CopyIcon copied={copiedField === 'id_token'} />
                <span className="text-sm text-if-dark">{copiedField === 'id_token' ? 'Copied!' : 'Copy'}</span>
              </button>
            </div>
            <div className="bg-if-window p-4 rounded-lg mb-4 overflow-x-auto border border-if-medium/20">
              <code className="text-sm break-all text-if-dark">{user.id_token}</code>
            </div>
            {idTokenDecoded && (
              <div>
                <h3 className="font-medium text-if-dark mb-2">Decoded Payload:</h3>
                <pre className="bg-if-window p-4 rounded-lg overflow-x-auto text-sm border border-if-medium/20 text-if-dark">
                  {JSON.stringify(idTokenDecoded, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}

        {/* Refresh Token */}
        {user?.refresh_token && (
          <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 mb-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-medium text-if-dark">Refresh Token</h2>
              <button
                onClick={() => copyToClipboard(user.refresh_token, 'refresh_token')}
                className="flex items-center gap-2 px-3 py-2 bg-if-window hover:bg-if-light/10 border border-if-medium/30 rounded transition-colors"
                title="Copy refresh token"
              >
                <CopyIcon copied={copiedField === 'refresh_token'} />
                <span className="text-sm text-if-dark">
                  {copiedField === 'refresh_token' ? 'Copied!' : 'Copy'}
                </span>
              </button>
            </div>
            <div className="bg-if-window p-4 rounded-lg overflow-x-auto border border-if-medium/20">
              <code className="text-sm break-all text-if-dark">{user.refresh_token}</code>
            </div>
          </div>
        )}
      </div>

      {/* Footer */}
      <footer className="bg-[#333] text-if-window text-center py-2 text-sm">
        Token Display Tool
      </footer>
    </div>
  );
};

export default TokenDisplay;