import { vi } from 'vitest';

export const mockUser = {
  access_token: 'mock-access-token',
  token_type: 'Bearer',
  profile: {
    sub: 'user-123',
    preferred_username: 'testuser',
    email: 'test@example.com',
    name: 'Test User',
  },
  expires_at: Math.floor(Date.now() / 1000) + 3600, // 1 hour from now
  expired: false,
  scope: 'openid profile email',
};

export const mockExpiredUser = {
  ...mockUser,
  expires_at: Math.floor(Date.now() / 1000) - 3600, // 1 hour ago
  expired: true,
};

export const createMockUserManager = () => ({
  signinRedirect: vi.fn().mockResolvedValue(undefined),
  signinRedirectCallback: vi.fn().mockResolvedValue(mockUser),
  signoutRedirect: vi.fn().mockResolvedValue(undefined),
  signinSilent: vi.fn().mockResolvedValue(mockUser),
  getUser: vi.fn().mockResolvedValue(mockUser),
  removeUser: vi.fn().mockResolvedValue(undefined),
  events: {
    addAccessTokenExpiring: vi.fn(),
    addAccessTokenExpired: vi.fn(),
    addSilentRenewError: vi.fn(),
    addUserSignedOut: vi.fn(),
  },
});

export const mockUserManagerClass = vi.fn().mockImplementation(() => createMockUserManager());

vi.mock('oidc-client-ts', () => ({
  UserManager: mockUserManagerClass,
  WebStorageStateStore: vi.fn().mockImplementation(() => ({})),
  User: vi.fn(),
}));
