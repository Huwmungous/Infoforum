import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AppInitializer } from '@if/web-common-react';
import App from './App';
import LogTests from './LogTests.jsx';
import './index.css';

const basePath = import.meta.env.BASE_URL.replace(/\/$/, '');
const pathname = window.location.pathname;
const isTests = pathname === `${basePath}/tests`;

// Extract appDomain from URL path (e.g., /infoforum/logs/ -> infoforum)
const pathParts = pathname.split('/').filter(Boolean);
const appDomain = pathParts[0] || 'infoforum';

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AppInitializer
      appType="user"
      dynamicConfig={{
        configServiceUrl: '/config',
        appDomain: appDomain,
        basePath: basePath
      }}
      loadingComponent={<div className="loading-message">Loading...</div>}
      errorComponent={(err) => <div className="error-message">Error: {err}</div>}
      redirectingComponent={<div className="loading-message">Redirecting to login...</div>}
    >
      {isTests ? <LogTests /> : <App />}
    </AppInitializer>
  </StrictMode>
);