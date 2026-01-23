import React from 'react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppInitializer } from './AppInitializer';
import { ConfigService } from '@if/web-common';
import { EnvironmentConfig } from '@if/web-common';
import { setupFetchInterceptor } from '@if/web-common';
import { LoggerService } from '@if/web-common';

// Mock EnvironmentConfig
vi.mock('@if/web-common', () => ({
  EnvironmentConfig: {
    get: vi.fn((key: string) => {
      const envVars: Record<string, string> = {
        IF_APP_NAME: 'TestApp',
        IF_ENVIRONMENT: 'DEV',
        IF_REALM: 'test-realm',
        IF_CLIENT: 'test-client',
        IF_CONFIG_SERVICE_URL: 'https://config.example.com',
      };
      return envVars[key] || '';
    }),
    validate: vi.fn(() => []),
    logValidationErrors: vi.fn(),
    logConfiguration: vi.fn(),
  },
}));

// Mock ConfigService
vi.mock('@if/web-common', () => ({
  ConfigService: {
    isInitialized: false,
    initialize: vi.fn().mockResolvedValue(undefined),
    initializeWithStaticConfig: vi.fn(),
    ClientId: 'test-client-id',
    OpenIdConfig: 'https://auth.example.com/realm',
  },
}));

// Mock fetchInterceptor
vi.mock('./fetchInterceptor', () => ({
  setupFetchInterceptor: vi.fn(),
}));

// Mock LoggerService
vi.mock('@if/web-common', () => ({
  LoggerService: {
    configureFromConfigService: vi.fn().mockResolvedValue(undefined),
  },
}));

// Mock auth components
vi.mock('./auth/AuthContext', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('./auth/ProtectedRoute', () => ({
  ProtectedRoute: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('./auth/SigninCallback', () => ({
  SigninCallback: () => <div>SigninCallback</div>,
}));

vi.mock('./auth/SignoutCallback', () => ({
  SignoutCallback: () => <div>SignoutCallback</div>,
}));

vi.mock('./appContext', () => ({
  AppContextProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('AppInitializer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (ConfigService as any).isInitialized = false;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('successful initialization', () => {
    it('should render children after initialization', async () => {
      render(
        <AppInitializer>
          <div>App Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('App Content')).toBeInTheDocument();
      });
    });

    it('should show loading state initially', () => {
      (ConfigService.initialize as any).mockImplementation(() => new Promise(() => {}));

      render(
        <AppInitializer>
          <div>App Content</div>
        </AppInitializer>
      );

      expect(screen.getByText('Initializing application...')).toBeInTheDocument();
    });

    it('should call setupFetchInterceptor with token supplier', async () => {
      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(setupFetchInterceptor).toHaveBeenCalledWith(
          expect.objectContaining({
            getAccessToken: expect.any(Function),
          })
        );
      });
    });

    it('should call ConfigService.initialize with params for dynamic config', async () => {
      render(
        <AppInitializer appType="service">
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(ConfigService.initialize).toHaveBeenCalledWith({
          configServiceUrl: 'https://config.example.com',
          realm: 'test-realm',
          client: 'test-client',
          appType: 'service',
          appName: 'TestApp',
          environment: 'DEV',
        });
      });
    });

    it('should call LoggerService.configureFromConfigService after init', async () => {
      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(LoggerService.configureFromConfigService).toHaveBeenCalled();
      });
    });

    it('should skip initialization if ConfigService already initialized', async () => {
      (ConfigService as any).isInitialized = true;

      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Content')).toBeInTheDocument();
      });

      expect(ConfigService.initialize).not.toHaveBeenCalled();
    });
  });

  describe('static config', () => {
    it('should use initializeWithStaticConfig when staticConfig provided', async () => {
      const staticConfig = {
        clientId: 'static-client',
        openIdConfig: 'https://auth.example.com',
        loggerService: 'https://logger.example.com',
        logLevel: 'Debug',
      };

      render(
        <AppInitializer staticConfig={staticConfig}>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(ConfigService.initializeWithStaticConfig).toHaveBeenCalledWith({
          ...staticConfig,
          realm: 'test-realm',
          appName: 'TestApp',
          environment: 'DEV',
          appType: 'user',
        });
      });

      expect(ConfigService.initialize).not.toHaveBeenCalled();
    });

    it('should show error when AUTH_CLIENT_ID env var is missing (static config)', async () => {
      (EnvironmentConfig.validate as any).mockReturnValue(['Missing: AUTH_CLIENT_ID']);

      render(
        <AppInitializer staticConfig={{ clientId: 'test', openIdConfig: 'https://auth.example.com' }}>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Configuration Error')).toBeInTheDocument();
        expect(screen.getByText('Configuration validation failed. Check console for details.')).toBeInTheDocument();
      });

      expect(EnvironmentConfig.logValidationErrors).toHaveBeenCalledWith(['Missing: AUTH_CLIENT_ID']);
    });

    it('should show error when AUTH_AUTHORITY env var is missing (static config)', async () => {
      (EnvironmentConfig.validate as any).mockReturnValue(['Missing: AUTH_AUTHORITY']);

      render(
        <AppInitializer staticConfig={{ clientId: 'test', openIdConfig: 'https://auth.example.com' }}>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Configuration Error')).toBeInTheDocument();
      });

      expect(EnvironmentConfig.logValidationErrors).toHaveBeenCalledWith(['Missing: AUTH_AUTHORITY']);
    });
  });

  describe('custom loading component', () => {
    it('should render custom loading component', () => {
      (ConfigService.initialize as any).mockImplementation(() => new Promise(() => {}));

      render(
        <AppInitializer loadingComponent={<div>Custom Loading</div>}>
          <div>Content</div>
        </AppInitializer>
      );

      expect(screen.getByText('Custom Loading')).toBeInTheDocument();
    });
  });

  describe('error handling', () => {
    it('should show error when initialization fails', async () => {
      (ConfigService.initialize as any).mockRejectedValue(new Error('Init failed'));

      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Configuration Error')).toBeInTheDocument();
        expect(screen.getByText('Init failed')).toBeInTheDocument();
      });
    });

    it('should show retry button on error', async () => {
      (ConfigService.initialize as any).mockRejectedValue(new Error('Init failed'));

      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Retry')).toBeInTheDocument();
      });
    });

    it('should render custom error component', async () => {
      (ConfigService.initialize as any).mockRejectedValue(new Error('Init failed'));

      render(
        <AppInitializer errorComponent={(error) => <div>Custom Error: {error}</div>}>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Custom Error: Init failed')).toBeInTheDocument();
      });
    });
  });

  describe('validation errors', () => {
    it('should show error when validation fails', async () => {
      (EnvironmentConfig.validate as any).mockReturnValue(['Missing: IF_APP_NAME']);

      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(screen.getByText('Configuration Error')).toBeInTheDocument();
        expect(screen.getByText('Configuration validation failed. Check console for details.')).toBeInTheDocument();
      });

      expect(EnvironmentConfig.logValidationErrors).toHaveBeenCalledWith(['Missing: IF_APP_NAME']);

      consoleErrorSpy.mockRestore();
    });
  });

  describe('appType prop', () => {
    it('should default to "user" appType', async () => {
      render(
        <AppInitializer>
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(ConfigService.initialize).toHaveBeenCalledWith(
          expect.objectContaining({ appType: 'user' })
        );
      });
    });

    it('should accept "service" appType', async () => {
      render(
        <AppInitializer appType="service">
          <div>Content</div>
        </AppInitializer>
      );

      await waitFor(() => {
        expect(ConfigService.initialize).toHaveBeenCalledWith(
          expect.objectContaining({ appType: 'service' })
        );
      });
    });

  });

});
