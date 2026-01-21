import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ConfigService } from './ConfigService';

// Mock LoggerService to avoid circular dependency issues
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

// Mock EnvironmentConfig - LOG_LEVEL comes from here as fallback
vi.mock('../environmentConfig', () => ({
  EnvironmentConfig: {
    get: vi.fn((key: string) => {
      if (key === 'LOG_LEVEL') return 'Information';
      return '';
    }),
  },
}));

describe('ConfigService', () => {
  const mockBootstrapConfig = {
    clientId: 'test-client-id',
    openIdConfig: 'https://auth.example.com/realms',
    loggerService: 'https://logger.example.com/api/log',
    logLevel: 'Debug',
  };

  const defaultInitParams = {
    configServiceUrl: 'https://config.example.com',
    realm: 'test-realm',
    client: 'test-client',
    appType: 'user' as const,
    appName: 'TestApp',
    environment: 'test',
  };

  beforeEach(() => {
    ConfigService.reset();
    vi.clearAllMocks();
  });

  afterEach(() => {
    ConfigService.reset();
  });

  describe('initial state', () => {
    it('should not be initialized by default', () => {
      expect(ConfigService.isInitialized).toBe(false);
    });

    it('should have default appType of "user"', () => {
      expect(ConfigService.appType).toBe('user');
    });

    it('should fall back to EnvironmentConfig for LogLevel when not set', () => {
      // EnvironmentConfig.get('LOG_LEVEL') returns 'Information' in the mock
      expect(ConfigService.LogLevel).toBe('Information');
    });

    it('should throw when accessing ClientId before initialization', () => {
      expect(() => ConfigService.ClientId).toThrow('ConfigService not initialized');
    });

    it('should throw when accessing OpenIdConfig before initialization', () => {
      expect(() => ConfigService.OpenIdConfig).toThrow('ConfigService not initialized');
    });

    it('should return null for LoggerService before initialization', () => {
      expect(ConfigService.LoggerService).toBe(null);
    });

    it('should return null for Realm before initialization', () => {
      expect(ConfigService.Realm).toBe(null);
    });

    it('should return null for AppName before initialization', () => {
      expect(ConfigService.AppName).toBe(null);
    });

    it('should return null for Environment before initialization', () => {
      expect(ConfigService.Environment).toBe(null);
    });
  });

  describe('initialize (dynamic)', () => {
    it('should fetch and store bootstrap config successfully', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      await ConfigService.initialize(defaultInitParams);

      expect(ConfigService.isInitialized).toBe(true);
      expect(ConfigService.ClientId).toBe('test-client-id');
      expect(ConfigService.OpenIdConfig).toBe('https://auth.example.com/realms/test-realm');
      expect(ConfigService.LoggerService).toBe('https://logger.example.com/api/log');
      expect(ConfigService.LogLevel).toBe('Debug');
      expect(ConfigService.Realm).toBe('test-realm');
      expect(ConfigService.AppName).toBe('TestApp');
      expect(ConfigService.Environment).toBe('test');
      expect(ConfigService.appType).toBe('user');
    });

    it('should throw on HTTP error', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      });

      await expect(ConfigService.initialize(defaultInitParams)).rejects.toThrow(
        'Failed to fetch bootstrap configuration: 500 Internal Server Error'
      );
      expect(ConfigService.isInitialized).toBe(false);
    });

    it('should throw on invalid bootstrap config (missing clientId)', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue({
          openIdConfig: 'https://auth.example.com',
          // Missing clientId
        }),
      });

      await expect(ConfigService.initialize(defaultInitParams)).rejects.toThrow(
        'Invalid bootstrap configuration: missing clientId or openIdConfig'
      );
    });

    it('should throw on invalid bootstrap config (missing openIdConfig)', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue({
          clientId: 'test-client-id',
          // Missing openIdConfig
        }),
      });

      await expect(ConfigService.initialize(defaultInitParams)).rejects.toThrow(
        'Invalid bootstrap configuration: missing clientId or openIdConfig'
      );
    });

    it('should not reinitialize if already initialized', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      await ConfigService.initialize(defaultInitParams);
      await ConfigService.initialize(defaultInitParams); // Second call should be no-op

      expect(global.fetch).toHaveBeenCalledTimes(1);
    });

    it('should handle concurrent initialization calls', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      // Call initialize concurrently
      const promise1 = ConfigService.initialize(defaultInitParams);
      const promise2 = ConfigService.initialize(defaultInitParams);
      const promise3 = ConfigService.initialize(defaultInitParams);

      await Promise.all([promise1, promise2, promise3]);

      // Should only fetch once
      expect(global.fetch).toHaveBeenCalledTimes(1);
      expect(ConfigService.isInitialized).toBe(true);
    });

    it('should use correct URL with appType and realm', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      await ConfigService.initialize({
        ...defaultInitParams,
        appType: 'service',
        realm: 'my-realm',
        client: 'my-client',
      });

      expect(global.fetch).toHaveBeenCalledWith(
        'https://config.example.com/Config?cfg=bootstrap&type=service&realm=my-realm&client=my-client'
      );
    });

    it('should set appType from params', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      await ConfigService.initialize({
        ...defaultInitParams,
        appType: 'patient',
      });

      expect(ConfigService.appType).toBe('patient');
    });
  });

  describe('initializeWithStaticConfig', () => {
    it('should initialize with provided static config', () => {
      ConfigService.initializeWithStaticConfig({
        clientId: 'static-client-id',
        openIdConfig: 'https://static-auth.example.com',
        loggerService: 'https://static-logger.example.com',
        logLevel: 'Warning',
        realm: 'static-realm',
        appName: 'StaticApp',
        environment: 'production',
        appType: 'service',
      });

      expect(ConfigService.isInitialized).toBe(true);
      expect(ConfigService.ClientId).toBe('static-client-id');
      expect(ConfigService.OpenIdConfig).toBe('https://static-auth.example.com');
      expect(ConfigService.LoggerService).toBe('https://static-logger.example.com');
      expect(ConfigService.LogLevel).toBe('Warning');
      expect(ConfigService.Realm).toBe('static-realm');
      expect(ConfigService.AppName).toBe('StaticApp');
      expect(ConfigService.Environment).toBe('production');
      expect(ConfigService.appType).toBe('service');
    });

    it('should fall back to EnvironmentConfig for logLevel if not provided', () => {
      ConfigService.initializeWithStaticConfig({
        clientId: 'static-client-id',
        openIdConfig: 'https://static-auth.example.com',
      });

      // Falls back to EnvironmentConfig.get('LOG_LEVEL') which returns 'Information' in the mock
      expect(ConfigService.LogLevel).toBe('Information');
    });

    it('should handle null loggerService', () => {
      ConfigService.initializeWithStaticConfig({
        clientId: 'static-client-id',
        openIdConfig: 'https://static-auth.example.com',
        loggerService: null,
      });

      expect(ConfigService.LoggerService).toBe(null);
    });

    it('should not reinitialize if already initialized', () => {
      ConfigService.initializeWithStaticConfig({
        clientId: 'first-client',
        openIdConfig: 'https://first-auth.example.com',
      });

      ConfigService.initializeWithStaticConfig({
        clientId: 'second-client',
        openIdConfig: 'https://second-auth.example.com',
      });

      // Should keep the first configuration
      expect(ConfigService.ClientId).toBe('first-client');
    });

    it('should default to user appType if not provided', () => {
      ConfigService.initializeWithStaticConfig({
        clientId: 'static-client-id',
        openIdConfig: 'https://static-auth.example.com',
      });

      expect(ConfigService.appType).toBe('user');
    });
  });

  describe('reset', () => {
    it('should reset all state to defaults', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: vi.fn().mockResolvedValue(mockBootstrapConfig),
      });

      await ConfigService.initialize({
        ...defaultInitParams,
        appType: 'service',
      });

      expect(ConfigService.isInitialized).toBe(true);
      expect(ConfigService.appType).toBe('service');

      ConfigService.reset();

      expect(ConfigService.isInitialized).toBe(false);
      expect(ConfigService.appType).toBe('user');
      // After reset, LogLevel falls back to EnvironmentConfig
      expect(ConfigService.LogLevel).toBe('Information');
      expect(ConfigService.LoggerService).toBe(null);
      expect(ConfigService.Realm).toBe(null);
      expect(ConfigService.AppName).toBe(null);
      expect(ConfigService.Environment).toBe(null);
    });
  });
});
