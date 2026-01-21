import { AuthProviderProps } from 'react-oidc-context';

const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';
const REALM = 'LongmanRd';
const CLIENT_ID = 'ifollama-react';

// Get the app base URL dynamically
const getBaseUrl = (): string => {
  const { protocol, host, pathname } = window.location;
  // Handle paths like /ifollama/ or just /
  const basePath = pathname.endsWith('/') ? pathname : pathname + '/';
  return `${protocol}//${host}${basePath}`;
};

export const authConfig: AuthProviderProps = {
  authority: `${KEYCLOAK_BASE_URL}${REALM}`,
  client_id: CLIENT_ID,
  redirect_uri: `${getBaseUrl()}auth-callback`,
  post_logout_redirect_uri: getBaseUrl(),
  scope: 'openid profile email offline_access',
  response_type: 'code',
  automaticSilentRenew: true,
  loadUserInfo: true,
  onSigninCallback: () => {
    // Remove the code and state from the URL after sign-in
    window.history.replaceState({}, document.title, window.location.pathname);
  },
};

export const getAccessToken = async (): Promise<string | null> => {
  // This will be set by the useAuth hook
  return null;
};

export const isIntelligenceUser = (user: { profile?: { kc_groups?: string[] } }): boolean => {
  const groups = user?.profile?.kc_groups ?? [];
  return groups.includes('IntelligenceUsers');
};
