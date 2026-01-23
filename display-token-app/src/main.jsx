// import { StrictMode } from 'react'; // Temporarily disabled - causes double OIDC callback in dev
import { createRoot } from 'react-dom/client';
import { AppInitializer } from '@if/web-common-react';
import App from './App.jsx';
import './index.css';

const basePath = import.meta.env.BASE_URL.replace(/\/$/, '');
const pathname = window.location.pathname;

createRoot(document.getElementById('root')).render(
  // <StrictMode> // Temporarily disabled - causes double OIDC callback in dev
    <AppInitializer appType="user"
      staticAuthConfig={{
        clientId: import.meta.env.VITE_AUTH_CLIENT_ID,
        authority: import.meta.env.VITE_AUTH_AUTHORITY,
      }}>
      <App />
    </AppInitializer>
  // </StrictMode>
);
