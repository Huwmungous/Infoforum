// main.jsx
// Entry point that extracts appDomain from URL and passes to AppInitializer

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AppInitializer } from '@if/web-common-react';
import App from './App.jsx';
import LogTests from './LogTests.jsx';
import { getConfigFromUrl, buildFullUrl, getAppBasePath } from './urlConfig.ts';
import './index.css';

// Extract appDomain from URL
const urlConfig = getConfigFromUrl();

// Get the config service URL from environment
const configServiceUrl = import.meta.env.VITE_IF_CONFIG_SERVICE_URL || '/config';

// Check if we're on the tests route
const basePath = urlConfig ? getAppBasePath(urlConfig) : '';
const pathname = window.location.pathname;
const isTests = pathname === `${basePath}/tests`;

const root = createRoot(document.getElementById('root'));

if (!urlConfig) {
  // No appDomain in URL - show error with instructions
  root.render(
    <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
      <div className="bg-white border border-gray-300 p-8 rounded-lg shadow-lg max-w-2xl">
        <h1 className="text-2xl font-bold text-red-600 mb-4">Configuration Required</h1>
        <p className="text-gray-700 mb-4">
          This application requires an application domain in the URL.
        </p>
        <div className="bg-gray-50 p-4 rounded border border-gray-200 mb-4">
          <p className="text-sm text-gray-600 mb-2">Expected URL format:</p>
          <code className="text-sm text-blue-600 break-all">
            {window.location.origin}/<span className="text-green-600">{'{appDomain}'}</span>/logs/
          </code>
        </div>
        <p className="text-sm text-gray-600 mb-4">Example:</p>
        <code className="text-sm text-blue-600 break-all block bg-gray-50 p-2 rounded">
          {window.location.origin}/infoforum/logs/
        </code>
        <div className="mt-6 text-xs text-gray-500">
          Current path: <code className="bg-gray-100 px-1">{window.location.pathname}</code>
        </div>
      </div>
    </div>
  );
} else {
  // We have URL config - initialise the app
  console.log('URL Config:', urlConfig);
  console.log('Config Service URL:', configServiceUrl);
  
  root.render(
    <StrictMode>
      <AppInitializer 
        appType="user"
        dynamicConfig={{
          configServiceUrl,
          appDomain: urlConfig.appDomain,
          // Build redirect URIs based on URL path
          redirectUri: buildFullUrl(urlConfig, 'signin/callback'),
          postLogoutRedirectUri: buildFullUrl(urlConfig, 'signout/callback'),
          silentRedirectUri: buildFullUrl(urlConfig, 'silent-callback'),
          basePath: getAppBasePath(urlConfig),
        }}
        loadingComponent={<div className="loading-message">Loading...</div>}
        errorComponent={(err) => <div className="error-message">Error: {err}</div>}
        redirectingComponent={<div className="loading-message">Redirecting to login...</div>}
      >
        {isTests ? <LogTests /> : <App />}
      </AppInitializer>
    </StrictMode>
  );
}