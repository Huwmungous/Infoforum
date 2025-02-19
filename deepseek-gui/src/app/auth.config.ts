// export const authConfig = {
//   authority: 'https://longmanrd.net/auth/realms/LongmanRd',
//   redirectUrl: window.location.origin + '/auth-callback',
//   postLogoutRedirectUri: window.location.origin,
//   clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7',
//   scope: 'openid profile email offline_access',
//   responseType: 'code',
//   silentRenew: true,
//   useRefreshToken: true,
//   logLevel: 2 // 0: None, 1: Error, 2: Warn, 3: Info, 4: Debug
// };


export const authConfig = {
  authority: 'https://longmanrd.net/auth/realms/LongmanRd',
  redirectUrl: window.location.origin + '/auth-callback',  // Ensure this is correct!
  postLogoutRedirectUri: window.location.origin,
  clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7',
  scope: 'openid profile email offline_access',
  responseType: 'code',
  silentRenew: true,
  useRefreshToken: true,
  postLoginRoute: '/',
};
