import { AuthConfig } from 'angular-oauth2-oidc';

export const authConfig: AuthConfig = { 
  issuer: 'https://longmanrd.net/auth/realms/LongmanRd', 
  redirectUri: window.location.origin,  
  clientId: 'Intelligence', 
  responseType: 'code',
  scope: 'openid profile email',
  showDebugInformation: true,
  disableAtHashCheck: true 
};
