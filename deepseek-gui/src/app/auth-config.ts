import { AuthConfig } from 'angular-oauth2-oidc';

export const authConfig: AuthConfig = {
  issuer: 'https://your-identity-provider.com',
  redirectUri: window.location.origin,
  clientId: 'your-client-id',
  responseType: 'code',
  scope: 'openid profile email',
  showDebugInformation: true,
};