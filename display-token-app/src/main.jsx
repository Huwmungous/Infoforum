// main.jsx
// Entry point that extracts realm/client from URL and passes to AppInitializer

import { createRoot } from 'react-dom/client';
import { AppInitializer } from '@if/web-common-react';
import App from './App.jsx';
import { getConfigFromUrl, buildFullUrl } from './urlConfig.ts';
import './index.css';

// Extract realm and client from URL
const urlConfig = getConfigFromUrl();

// Get the config service URL from environment
const configServiceUrl = import.meta.env.VITE_IF_CONFIG_SERVICE_URL || '/config';

const root = createRoot(document.getElementById('root'));

if (!urlConfig) {
  // No realm/client in URL - show error with instructions
  root.render(
    <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
      <div className="bg-white border border-gray-300 p-8 rounded-lg shadow-lg max-w-2xl">
        <h1 className="text-2xl font-bold text-red-600 mb-4">Configuration Required</h1>
        <p className="text-gray-700 mb-4">
          This application requires realm and client parameters in the URL.
        </p>
        <div className="bg-gray-50 p-4 rounded border border-gray-200 mb-4">
          <p className="text-sm text-gray-600 mb-2">Expected URL format:</p>
          <code className="text-sm text-blue-600 break-all">
            {window.location.origin}/tokens/<span className="text-green-600">{'{realm}'}</span>/<span className="text-green-600">{'{client}'}</span>/
          </code>
        </div>
        <p className="text-sm text-gray-600 mb-4">Example:</p>
        <code className="text-sm text-blue-600 break-all block bg-gray-50 p-2 rounded">
          {window.location.origin}/tokens/IfDevelopment_Dev/dev-login/
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
    <AppInitializer 
      appType="user"
      dynamicConfig={{
        configServiceUrl,
        realm: urlConfig.realm,
        client: urlConfig.client,
        // Build redirect URIs based on URL path
        redirectUri: buildFullUrl(urlConfig, 'signin/callback'),
        postLogoutRedirectUri: buildFullUrl(urlConfig, 'signout/callback'),
        silentRedirectUri: buildFullUrl(urlConfig, 'silent-callback'),
      }}
    >
      <App />
    </AppInitializer>
  );
}
