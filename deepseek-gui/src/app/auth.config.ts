// import { AuthConfig } from 'angular-oauth2-oidc';

// export const authConfig: AuthConfig = { 
//   issuer: 'https://longmanrd.net/auth/realms/LongmanRd', 
//   redirectUri: window.location.origin,  
//   clientId: 'Intelligence', 
//   responseType: 'code',
//   scope: 'openid profile email',
//   showDebugInformation: true,
//   disableAtHashCheck: true 
// };

// import { AuthConfig } from 'angular-auth-oidc-client';

export const authConfig = {
  authority: 'https://longmanrd.net/auth/realms/LongmanRd',
  redirectUrl: window.location.origin,
  postLogoutRedirectUri: window.location.origin,
  clientId: 'Intelligence',
  scope: 'openid profile email',
  responseType: 'code',
  silentRenew: true,
  useRefreshToken: true,
  logLevel:4, // 0: None, 1: Error, 2: Warn, 3: Info, 4: Debug
};
