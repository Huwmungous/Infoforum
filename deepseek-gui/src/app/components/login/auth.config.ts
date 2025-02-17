import { AuthConfig } from 'angular-oauth2-oidc';

export const authConfig: AuthConfig = {
  issuer: 'https://longmanrd.net/realms/master',
  redirectUri: window.location.origin,
  clientId: 'LongmanRd-realm',
  responseType: 'code',
  scope: 'openid profile email',
  showDebugInformation: true,
};