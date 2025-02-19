export const authConfig = {
  authority: 'https://longmanrd.net/auth/realms/LongmanRd',
  redirectUrl: window.location.origin + '/auth-callback',  // ✅ Correct key
  postLogoutRedirectUri: window.location.origin,
  clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7',
  scope: 'openid profile email offline_access',
  responseType: 'code',
  silentRenew: true,
  silentRenewUrl: window.location.origin + '/silent-renew.html',
  useRefreshToken: true, // ✅ Ensures session is maintained
  storage: localStorage, // ✅ Ensures state persistence
  logLevel: 3, // ✅ Enables debugging logs
  postLoginRoute: '/', // ✅ Ensure it's correctly applied after login
  disablePKCE: false, // ✅ Ensures PKCE is enabled
};


