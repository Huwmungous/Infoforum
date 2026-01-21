import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// We need to use vi.hoisted to make the mock available before vi.mock hoisting
const { MockUserManager, getMockInstance, resetMockInstance, constructorSpy } = vi.hoisted(() => {
  // Factory function must be inside vi.hoisted
  const createMockUserManager = () => ({
    signinRedirect: vi.fn().mockResolvedValue(undefined),
    signinRedirectCallback: vi.fn().mockResolvedValue({
      access_token: 'mock-token',
      profile: {
        sub: 'user-123',
        preferred_username: 'testuser',
        email: 'test@example.com',
      },
      expires_at: Math.floor(Date.now() / 1000) + 3600,
    }),
    signoutRedirect: vi.fn().mockResolvedValue(undefined),
    signinSilent: vi.fn().mockResolvedValue({
      access_token: 'renewed-token',
      profile: {
        sub: 'user-123',
        preferred_username: 'testuser',
        email: 'test@example.com',
      },
      expires_at: Math.floor(Date.now() / 1000) + 7200,
    }),
    getUser: vi.fn().mockResolvedValue({
      access_token: 'mock-token',
      profile: {
        sub: 'user-123',
        preferred_username: 'testuser',
        email: 'test@example.com',
      },
      expires_at: Math.floor(Date.now() / 1000) + 3600,
    }),
    removeUser: vi.fn().mockResolvedValue(undefined),
    signinSilentCallback: vi.fn().mockResolvedValue(undefined),
    events: {
      addAccessTokenExpiring: vi.fn(),
      addAccessTokenExpired: vi.fn(),
      addSilentRenewError: vi.fn(),
      addUserLoaded: vi.fn(),
      addUserUnloaded: vi.fn(),
      addUserSignedOut: vi.fn(),
    },
  });

  let mockInstance = createMockUserManager();
  const spy = vi.fn();

  // Create a proper class that can be used with `new`
  class MockUserManagerClass {
    signinRedirect: ReturnType<typeof vi.fn>;
    signinRedirectCallback: ReturnType<typeof vi.fn>;
    signoutRedirect: ReturnType<typeof vi.fn>;
    signinSilent: ReturnType<typeof vi.fn>;
    getUser: ReturnType<typeof vi.fn>;
    removeUser: ReturnType<typeof vi.fn>;
    signinSilentCallback: ReturnType<typeof vi.fn>;
    events: {
      addAccessTokenExpiring: ReturnType<typeof vi.fn>;
      addAccessTokenExpired: ReturnType<typeof vi.fn>;
      addSilentRenewError: ReturnType<typeof vi.fn>;
      addUserLoaded: ReturnType<typeof vi.fn>;
      addUserUnloaded: ReturnType<typeof vi.fn>;
      addUserSignedOut: ReturnType<typeof vi.fn>;
    };

    constructor(settings: any) {
      spy(settings);
      Object.assign(this, mockInstance);
    }
  }

  return {
    MockUserManager: MockUserManagerClass,
    constructorSpy: spy,
    getMockInstance: () => mockInstance,
    resetMockInstance: () => {
      mockInstance = createMockUserManager();
    },
  };
});

vi.mock('oidc-client-ts', () => ({
  UserManager: MockUserManager,
  WebStorageStateStore: class MockWebStorageStateStore {
    constructor() {
      // Mock implementation
    }
  },
}));

// Mock ConfigService
vi.mock('../configServiceClient', () => ({
  ConfigService: {
    isInitialized: true,
    ClientId: 'test-client-id',
    OpenIdConfig: 'https://auth.example.com/realms/test',
  },
}));

// Mock LoggerService
vi.mock('../logger', () => ({
  LoggerService: {
    create: vi.fn(() => ({
      trace: vi.fn(),
      debug: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      critical: vi.fn(),
    })),
  },
}));

// Import after mocks are set up
import { AuthService, AuthConfig } from './AuthService';

describe('AuthService', () => {
  const mockConfig: AuthConfig = {
    redirectUri: 'http://localhost:3000/callback',
    postLogoutRedirectUri: 'http://localhost:3000',
  };

  beforeEach(() => {
    resetMockInstance();
    AuthService.reset();
  });

  afterEach(() => {
    AuthService.reset();
  });

  describe('getInstance', () => {
    it('should return singleton instance', () => {
      const instance1 = AuthService.getInstance();
      const instance2 = AuthService.getInstance();

      expect(instance1).toBe(instance2);
    });

    it('should return different instance after reset', () => {
      const instance1 = AuthService.getInstance();
      AuthService.reset();
      const instance2 = AuthService.getInstance();

      expect(instance1).not.toBe(instance2);
    });
  });

  describe('initialize', () => {
    it('should create UserManager with correct settings', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      expect(constructorSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          authority: 'https://auth.example.com/realms/test',
          client_id: 'test-client-id',
          redirect_uri: 'http://localhost:3000/callback',
          response_type: 'code',
          scope: 'openid profile email',
          automaticSilentRenew: true,
        })
      );
    });

    it('should set up event handlers', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const mockInstance = getMockInstance();
      expect(mockInstance.events.addAccessTokenExpiring).toHaveBeenCalled();
      expect(mockInstance.events.addAccessTokenExpired).toHaveBeenCalled();
      expect(mockInstance.events.addSilentRenewError).toHaveBeenCalled();
      expect(mockInstance.events.addUserSignedOut).toHaveBeenCalled();
    });

    it('should throw when ConfigService is not initialized', async () => {
      const { ConfigService } = await import('../configServiceClient');
      (ConfigService as any).isInitialized = false;

      AuthService.reset();
      const authService = AuthService.getInstance();

      await expect(authService.initialize(mockConfig)).rejects.toThrow(
        'ConfigService must be initialized before AuthService'
      );

      // Reset mock
      (ConfigService as any).isInitialized = true;
    });

    it('should throw on failed OIDC config fetch', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        statusText: 'Not Found',
      });

      const authService = AuthService.getInstance();

      await expect(
        authService.initialize({
          ...mockConfig,
          oidcConfigUrl: 'https://auth.example.com/.well-known/openid-configuration',
        })
      ).rejects.toThrow('Failed to fetch OIDC configuration');
    });
  });

  describe('signin', () => {
    it('should call signinRedirect', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      await authService.signin();

      expect(getMockInstance().signinRedirect).toHaveBeenCalled();
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.signin()).rejects.toThrow('AuthService not initialized');
    });
  });

  describe('completeSignin', () => {
    it('should call signinRedirectCallback and return user from callback', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      // Configure mock with a unique value to verify we're returning callback result
      const uniqueToken = `unique-token-${Date.now()}`;
      const uniqueUserId = `user-${Date.now()}`;
      getMockInstance().signinRedirectCallback.mockResolvedValueOnce({
        access_token: uniqueToken,
        profile: {
          sub: uniqueUserId,
          preferred_username: 'dynamicuser',
        },
        expires_at: Math.floor(Date.now() / 1000) + 3600,
      });

      const user = await authService.completeSignin();

      expect(getMockInstance().signinRedirectCallback).toHaveBeenCalled();
      expect(user).toBeDefined();
      // Verify we got the dynamically configured value, not a hardcoded one
      expect(user.access_token).toBe(uniqueToken);
      expect(user.profile.sub).toBe(uniqueUserId);
    });

    it('should propagate errors from signinRedirectCallback', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const callbackError = new Error('Callback processing failed');
      getMockInstance().signinRedirectCallback.mockRejectedValueOnce(callbackError);

      await expect(authService.completeSignin()).rejects.toThrow('Callback processing failed');
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.completeSignin()).rejects.toThrow('AuthService not initialized');
    });
  });

  describe('signout', () => {
    it('should call signoutRedirect', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      await authService.signout();

      expect(getMockInstance().signoutRedirect).toHaveBeenCalled();
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.signout()).rejects.toThrow('AuthService not initialized');
    });
  });

  describe('renewToken', () => {
    it('should call signinSilent and return renewed user from silent renewal', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      // Configure mock with unique values to verify we're returning the silent renewal result
      const uniqueRenewedToken = `renewed-${Date.now()}`;
      const uniqueExpiry = Math.floor(Date.now() / 1000) + 7200;
      getMockInstance().signinSilent.mockResolvedValueOnce({
        access_token: uniqueRenewedToken,
        profile: {
          sub: 'user-123',
          preferred_username: 'testuser',
          email: 'test@example.com',
        },
        expires_at: uniqueExpiry,
      });

      const user = await authService.renewToken();

      expect(getMockInstance().signinSilent).toHaveBeenCalled();
      // Verify we got the dynamically configured value
      expect(user.access_token).toBe(uniqueRenewedToken);
      expect(user.expires_at).toBe(uniqueExpiry);
    });

    it('should propagate errors from signinSilent', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const renewError = new Error('Silent renewal network error');
      getMockInstance().signinSilent.mockRejectedValueOnce(renewError);

      await expect(authService.renewToken()).rejects.toThrow('Silent renewal network error');
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.renewToken()).rejects.toThrow('AuthService not initialized');
    });

    it('should throw when silent renewal returns no user', async () => {
      getMockInstance().signinSilent.mockResolvedValueOnce(null);

      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      await expect(authService.renewToken()).rejects.toThrow(
        'Silent renewal failed: no user returned'
      );
    });
  });

  describe('getUser', () => {
    it('should return current user from UserManager', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      // Configure mock with unique value to verify we're returning the getUser result
      const uniqueUserId = `user-${Date.now()}`;
      getMockInstance().getUser.mockResolvedValueOnce({
        access_token: 'test-token',
        profile: {
          sub: uniqueUserId,
          preferred_username: 'dynamicuser',
          email: 'dynamic@example.com',
        },
        expires_at: Math.floor(Date.now() / 1000) + 3600,
      });

      const user = await authService.getUser();

      expect(getMockInstance().getUser).toHaveBeenCalled();
      expect(user).toBeDefined();
      // Verify we got the dynamically configured value
      expect(user?.profile.sub).toBe(uniqueUserId);
    });

    it('should return null when no user is stored', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      getMockInstance().getUser.mockResolvedValueOnce(null);

      const user = await authService.getUser();

      expect(user).toBeNull();
    });

    it('should propagate errors from UserManager.getUser', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      getMockInstance().getUser.mockRejectedValueOnce(new Error('Storage access error'));

      await expect(authService.getUser()).rejects.toThrow('Storage access error');
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.getUser()).rejects.toThrow('AuthService not initialized');
    });
  });

  describe('getAccessToken', () => {
    it('should return access token from current user', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      // Configure mock with unique token to verify we're extracting from user
      const uniqueToken = `token-${Date.now()}`;
      getMockInstance().getUser.mockResolvedValueOnce({
        access_token: uniqueToken,
        profile: { sub: 'user-123' },
      });

      const token = await authService.getAccessToken();

      // Verify we got the dynamically configured token
      expect(token).toBe(uniqueToken);
    });

    it('should return null when no user exists', async () => {
      getMockInstance().getUser.mockResolvedValueOnce(null);

      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const token = await authService.getAccessToken();

      expect(token).toBeNull();
    });

    it('should return null when user has no access_token', async () => {
      getMockInstance().getUser.mockResolvedValueOnce({
        profile: { sub: 'user-123' },
        // Note: access_token is missing
      });

      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const token = await authService.getAccessToken();

      expect(token).toBeNull();
    });
  });

  describe('removeUser', () => {
    it('should call removeUser on UserManager', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      await authService.removeUser();

      expect(getMockInstance().removeUser).toHaveBeenCalled();
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.removeUser()).rejects.toThrow('AuthService not initialized');
    });
  });

  describe('completeSilentSignin', () => {
    it('should call signinSilentCallback on UserManager', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      await authService.completeSilentSignin();

      expect(getMockInstance().signinSilentCallback).toHaveBeenCalled();
    });

    it('should throw when not initialized', async () => {
      const authService = AuthService.getInstance();

      await expect(authService.completeSilentSignin()).rejects.toThrow('AuthService not initialized');
    });

    it('should propagate errors from signinSilentCallback', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      const callbackError = new Error('Silent callback failed');
      getMockInstance().signinSilentCallback.mockRejectedValueOnce(callbackError);

      await expect(authService.completeSilentSignin()).rejects.toThrow('Silent callback failed');
    });
  });

  describe('reset', () => {
    it('should clear instance and internal state', async () => {
      const authService = AuthService.getInstance();
      await authService.initialize(mockConfig);

      AuthService.reset();

      // After reset, methods should throw not initialized
      const newInstance = AuthService.getInstance();
      await expect(newInstance.signin()).rejects.toThrow('AuthService not initialized');
    });
  });
});
