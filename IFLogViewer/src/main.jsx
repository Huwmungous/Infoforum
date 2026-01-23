import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AppInitializer } from '@if/web-common';
import App from './App';
import LogTests from './LogTests.jsx';
import './index.css';

const basePath = import.meta.env.BASE_URL.replace(/\/$/, '');
const pathname = window.location.pathname;
const isTests = pathname === `${basePath}/tests`;

// Static config from env vars
const staticConfig = {
  clientId: import.meta.env.VITE_AUTH_CLIENT_ID,
  openIdConfig: import.meta.env.VITE_AUTH_AUTHORITY,
  loggerService: import.meta.env.VITE_LOG_SERVICE_URL
};

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AppInitializer
      staticConfig={staticConfig}
      loadingComponent={<div className="loading-message">Loading...</div>}
      errorComponent={(err) => <div className="error-message">Error: {err}</div>}
      redirectingComponent={<div className="loading-message">Redirecting to login...</div>}
      staticAuthConfig={{
        clientId: import.meta.env.VITE_AUTH_CLIENT_ID,
        authority: import.meta.env.VITE_AUTH_AUTHORITY,
      }}>
      {isTests ? <LogTests /> : <App />}
    </AppInitializer>
  </StrictMode>
);
