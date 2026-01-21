import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthProvider } from 'react-oidc-context';
import { authConfig } from './auth/authConfig';
import App from './App';
import './App.scss';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider {...authConfig}>
      <App />
    </AuthProvider>
  </StrictMode>
);
