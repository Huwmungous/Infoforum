export const authConfig = {
  authority: 'https://longmanrd.net/auth/realms/LongmanRd',
  redirectUrl: window.location.origin + '/callback',
  postLogoutRedirectUri: window.location.origin,
  clientId: '46279F81-ED75-4CFA-868C-A36AE8BE22B0',
  scope: 'openid profile email',
  responseType: 'code',
  silentRenew: false,
  useRefreshToken: true,
  logLevel:1, // 0: None, 1: Error, 2: Warn, 3: Info, 4: Debug
};
