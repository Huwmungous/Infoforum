import React, { useState } from 'react';
import { useAppContext } from '@sfd/web-common';
import { patientAuthService } from './PatientAuthService';

/**
 * PatientTokenDisplay component
 * Displays authenticated patient information and tokens
 */
const PatientTokenDisplay = ({ user, onLogout }) => {
  const { config } = useAppContext();
  const [copiedField, setCopiedField] = useState(null);
  const [renewing, setRenewing] = useState(false);
  const [currentUser, setCurrentUser] = useState(user);

  const copyToClipboard = async (text, fieldName) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(fieldName);
      setTimeout(() => setCopiedField(null), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  const handleRenewToken = async () => {
    try {
      setRenewing(true);
      const renewedUser = await patientAuthService.renewToken(config.openIdConfig);
      setCurrentUser(renewedUser);
      alert('Token renewed successfully!');
    } catch (err) {
      alert(`Failed to renew token: ${err.message}`);
    } finally {
      setRenewing(false);
    }
  };

  const handleLogout = () => {
    patientAuthService.logout();
    onLogout();
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
    if (!exp) return 'Unknown';
    const date = new Date(exp * 1000);
    const now = new Date();
    const diff = date - now;
    const minutes = Math.floor(diff / 60000);
    const seconds = Math.floor((diff % 60000) / 1000);
    
    if (diff < 0) {
      return 'Expired';
    }
    
    return `${date.toLocaleString()} (in ${minutes}m ${seconds}s)`;
  };

  const CopyIcon = ({ copied }) => (
    <svg
      className={`w-5 h-5 ${copied ? 'text-green-600' : 'text-gray-600 hover:text-gray-900'}`}
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

  const accessTokenDecoded = currentUser?.access_token ? decodeJWT(currentUser.access_token) : null;
  const idTokenDecoded = currentUser?.id_token ? decodeJWT(currentUser.id_token) : null;

  return (
    <div className="min-h-screen bg-gray-100 p-8">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="bg-white rounded-lg shadow-md p-6 mb-6">
          <div className="flex justify-between items-center mb-4">
            <div>
              <h1 className="text-2xl font-bold">Patient Authentication Successful</h1>
              <p className="text-gray-600 text-sm mt-1">Client: dev-login-pps</p>
            </div>
            <div className="flex gap-2">
              <button
                onClick={handleRenewToken}
                disabled={renewing}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:bg-blue-300"
              >
                {renewing ? 'Renewing...' : 'Renew Token'}
              </button>
              <button
                onClick={handleLogout}
                className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
              >
                Logout
              </button>
            </div>
          </div>

          {/* User Info */}
          {currentUser?.profile && (
            <div className="mb-4 p-4 bg-blue-50 rounded">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm">
                <div>
                  <span className="text-gray-600">Username:</span>{' '}
                  <span className="font-medium text-gray-900">
                    {currentUser.profile.preferred_username || currentUser.profile.name || 'N/A'}
                  </span>
                </div>
                <div>
                  <span className="text-gray-600">Email:</span>{' '}
                  <span className="font-medium text-gray-900">{currentUser.profile.email || 'N/A'}</span>
                </div>
                {currentUser.profile.given_name && (
                  <div>
                    <span className="text-gray-600">First Name:</span>{' '}
                    <span className="font-medium text-gray-900">{currentUser.profile.given_name}</span>
                  </div>
                )}
                {currentUser.profile.family_name && (
                  <div>
                    <span className="text-gray-600">Last Name:</span>{' '}
                    <span className="font-medium text-gray-900">{currentUser.profile.family_name}</span>
                  </div>
                )}
                {accessTokenDecoded?.exp && (
                  <div className="md:col-span-2">
                    <span className="text-gray-600">Token Expires:</span>{' '}
                    <span className="font-medium text-gray-900">
                      {formatExpiration(accessTokenDecoded.exp)}
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Token Status */}
          <div className="p-4 bg-green-50 rounded">
            <div className="flex items-center text-sm text-green-800">
              <svg className="w-5 h-5 mr-2" fill="currentColor" viewBox="0 0 20 20">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
                  clipRule="evenodd"
                />
              </svg>
              <span>
                Patient authenticated successfully using Resource Owner Password Credentials (ROPC) flow.
              </span>
            </div>
          </div>
        </div>

        {/* Access Token */}
        {currentUser?.access_token && (
          <div className="bg-white rounded-lg shadow-md p-6 mb-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-bold">Access Token</h2>
              <button
                onClick={() => copyToClipboard(currentUser.access_token, 'access_token')}
                className="flex items-center gap-2 px-3 py-2 bg-gray-100 hover:bg-gray-200 rounded transition-colors"
                title="Copy access token"
              >
                <CopyIcon copied={copiedField === 'access_token'} />
                <span className="text-sm">
                  {copiedField === 'access_token' ? 'Copied!' : 'Copy'}
                </span>
              </button>
            </div>
            <div className="bg-gray-50 p-4 rounded mb-4 overflow-x-auto">
              <code className="text-sm break-all">{currentUser.access_token}</code>
            </div>
            {accessTokenDecoded && (
              <div>
                <h3 className="font-semibold mb-2">Decoded Payload:</h3>
                <pre className="bg-gray-50 p-4 rounded overflow-x-auto text-sm">
                  {JSON.stringify(accessTokenDecoded, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}

        {/* ID Token */}
        {currentUser?.id_token && (
          <div className="bg-white rounded-lg shadow-md p-6 mb-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-bold">ID Token</h2>
              <button
                onClick={() => copyToClipboard(currentUser.id_token, 'id_token')}
                className="flex items-center gap-2 px-3 py-2 bg-gray-100 hover:bg-gray-200 rounded transition-colors"
                title="Copy ID token"
              >
                <CopyIcon copied={copiedField === 'id_token'} />
                <span className="text-sm">{copiedField === 'id_token' ? 'Copied!' : 'Copy'}</span>
              </button>
            </div>
            <div className="bg-gray-50 p-4 rounded mb-4 overflow-x-auto">
              <code className="text-sm break-all">{currentUser.id_token}</code>
            </div>
            {idTokenDecoded && (
              <div>
                <h3 className="font-semibold mb-2">Decoded Payload:</h3>
                <pre className="bg-gray-50 p-4 rounded overflow-x-auto text-sm">
                  {JSON.stringify(idTokenDecoded, null, 2)}
                </pre>
              </div>
            )}
          </div>
        )}

        {/* Refresh Token */}
        {currentUser?.refresh_token && (
          <div className="bg-white rounded-lg shadow-md p-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-xl font-bold">Refresh Token</h2>
              <button
                onClick={() => copyToClipboard(currentUser.refresh_token, 'refresh_token')}
                className="flex items-center gap-2 px-3 py-2 bg-gray-100 hover:bg-gray-200 rounded transition-colors"
                title="Copy refresh token"
              >
                <CopyIcon copied={copiedField === 'refresh_token'} />
                <span className="text-sm">
                  {copiedField === 'refresh_token' ? 'Copied!' : 'Copy'}
                </span>
              </button>
            </div>
            <div className="bg-gray-50 p-4 rounded overflow-x-auto">
              <code className="text-sm break-all">{currentUser.refresh_token}</code>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default PatientTokenDisplay;
