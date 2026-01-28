import React, { ReactNode, useEffect, useState } from 'react';
import { ConfigService, AppType } from '@if/web-common';
import { EnvironmentConfig } from '@if/web-common';
import { setupFetchInterceptor } from '@if/web-common';
import { authService } from '@if/web-common';
import { LoggerService } from '@if/web-common';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/ProtectedRoute';
import { SigninCallback } from './components/SigninCallback';
import { SignoutCallback } from './components/SignoutCallback';
import { SilentCallback } from './components/SilentCallback';
import { AppContextProvider } from './contexts/AppContext';
import { getCurrentRoutePath, buildAppUrl, getAppBasePath, setDynamicBasePath } from "@if/web-common";

export interface StaticConfigOverride {
  clientId: string;
  openIdConfig: string;
  loggerService?: string | null;
  logLevel?: string;
}

/**
 * Optional auth config overrides for debug/testing scenarios.
 * When provided, these values take precedence over ConfigService values.
 * Use with dynamic config to override just auth settings while keeping other config dynamic.
 */
export interface StaticAuthConfigOverride {
  clientId?: string;
  authority?: string;
}

/**
 * Dynamic configuration from URL parameters.
 * When provided, appDomain comes from URL instead of environment variables.
 * This enables multi-tenant applications where the tenant is determined by URL.
 */
export interface DynamicConfigOverride {
  /** URL to the config service (e.g., "https://example.com/config" or "/config") */
  configServiceUrl: string;
  /** Application domain extracted from URL (e.g., 'Infoforum', 'BreakTackle') */
  appDomain: string;
  /** Full redirect URI for signin callback (optional - will be built from base path if not provided) */
  redirectUri?: string;
  /** Full redirect URI for post-logout (optional - will be built from base path if not provided) */
  postLogoutRedirectUri?: string;
  /** Full redirect URI for silent token renewal (optional - will be built from base path if not provided) */
  silentRedirectUri?: string;
  /** Base path for the application (e.g., "/infoforum/tokens") - used for routing */
  basePath?: string;
}

export interface AppInitializerProps {
  children: ReactNode;
  appType?: AppType;
  staticConfig?: StaticConfigOverride;
  /**
   * Dynamic configuration from URL parameters.
   * When provided, appDomain comes from URL instead of environment variables.
   * This takes precedence over environment variables for appDomain/configServiceUrl.
   */
  dynamicConfig?: DynamicConfigOverride;
  /**
   * Optional auth config overrides. When using dynamic config, these values
   * override the auth settings from ConfigService. Useful for debug/testing.
   */
  staticAuthConfig?: StaticAuthConfigOverride;
  loadingComponent?: ReactNode;
  errorComponent?: (error: string) => ReactNode;
  redirectingComponent?: ReactNode;
}

/**
 * AppInitializer is the unified entry point for If applications.
 * It handles config, auth, and route protection internally.
 *
 * Usage (environment-based config):
 * <AppInitializer appType="user">
 *   <App />
 * </AppInitializer>
 * 
 * Usage (URL-based dynamic config):
 * <AppInitializer 
 *   appType="user"
 *   dynamicConfig={{
 *     configServiceUrl: '/config',
 *     appDomain: 'Infoforum',
 *   }}
 * >
 *   <App />
 * </AppInitializer>
 */
export function AppInitializer({
  children,
  appType = 'user',
  staticConfig,
  dynamicConfig,
  staticAuthConfig,
  loadingComponent,
  errorComponent,
  redirectingComponent
}: AppInitializerProps) {
  const [state, setState] = useState<{
    ready: boolean;
    error: string | null;
  }>({
    ready: false,
    error: null
  });

  // Set dynamic base path if using URL-based config
  // This must happen before any routing decisions
  if (dynamicConfig?.basePath) {
    setDynamicBasePath(dynamicConfig.basePath);
  }

  // Detect if we're on OAuth callback routes (supports both standard and hash routing)
  const routePath = getCurrentRoutePath();
  const isSigninCallbackRoute = routePath === '/signin/callback';
  const isSignoutCallbackRoute = routePath === '/signout/callback';
  const isSilentCallbackRoute = routePath === '/silent-callback';

  // Short-circuit for silent renewal - avoid full app initialization
  // This runs in a hidden iframe, so skip all heavy initialization
  const isInIframe = typeof window !== 'undefined' && window.self !== window.top;
  if (isInIframe && isSilentCallbackRoute) {
    return <SilentCallback />;
  }

  useEffect(() => {
    const initializeConfig = async () => {
      try {
        // Already initialized (e.g., React StrictMode double-invoke)
        if (ConfigService.isInitialized) {
          console.log('AppInitializer: ConfigService already initialized, skipping');
          setState({ ready: true, error: null });
          return;
        }

        // Determine if using dynamic URL-based config
        const useDynamicUrlConfig = !!dynamicConfig;

        // Validate environment variables using EnvironmentConfig
        // Pass useDynamicUrlConfig to skip appDomain/configServiceUrl validation
        const validationErrors = EnvironmentConfig.validate(!!staticConfig, useDynamicUrlConfig);
        if (validationErrors.length > 0) {
          EnvironmentConfig.logValidationErrors(validationErrors);
          setState({ ready: false, error: 'Configuration validation failed. Check console for details.' });
          return;
        }

        // Log all configuration values after successful validation
        EnvironmentConfig.logConfiguration(!!staticConfig, useDynamicUrlConfig);

        // Setup global fetch interceptor with auth token supplier
        setupFetchInterceptor({
          getAccessToken: () => authService.getAccessToken()
        });

        if (staticConfig) {
          // Use static config - bypasses ConfigWebService
          console.log('AppInitializer: Using static config (bypassing ConfigWebService)');
          ConfigService.initializeWithStaticConfig({
            ...staticConfig,
            appDomain: dynamicConfig?.appDomain ?? EnvironmentConfig.get('IF_APP_DOMAIN'),
            appName: EnvironmentConfig.get('IF_APP_NAME'),
            environment: EnvironmentConfig.get('IF_ENVIRONMENT'),
            appType
          });
        } else if (dynamicConfig) {
          // Use dynamic URL-based config
          console.log(`AppInitializer: Using dynamic URL config - appDomain: ${dynamicConfig.appDomain}`);
          await ConfigService.initialize({
            configServiceUrl: dynamicConfig.configServiceUrl,
            appDomain: dynamicConfig.appDomain,
            appType,
            appName: EnvironmentConfig.get('IF_APP_NAME'),
            environment: EnvironmentConfig.get('IF_ENVIRONMENT')
          });
        } else {
          // Fetch from ConfigWebService using environment variables
          console.log(`AppInitializer: Initializing ConfigService with appType: ${appType}`);
          await ConfigService.initialize({
            configServiceUrl: EnvironmentConfig.get('IF_CONFIG_SERVICE_URL'),
            appDomain: EnvironmentConfig.get('IF_APP_DOMAIN'),
            appType,
            appName: EnvironmentConfig.get('IF_APP_NAME'),
            environment: EnvironmentConfig.get('IF_ENVIRONMENT')
          });
        }

        console.log('AppInitializer: ConfigService initialized successfully');

        // CRITICAL: Configure LoggerService AFTER ConfigService is ready
        await LoggerService.configureFromConfigService();
        console.log('AppInitializer: LoggerService configured from ConfigService');

        // Log app launching event
        const appLogger = LoggerService.create('AppInitializer');
        appLogger.info(`Application launching: ${ConfigService.AppName} (${ConfigService.Environment})`);

        setState({ ready: true, error: null });
      } catch (err: any) {
        console.error('AppInitializer: Failed to initialize ConfigService:', err);
        
        // Try to log the failure if LoggerService is available
        try {
          const appLogger = LoggerService.create('AppInitializer');
          appLogger.error(`Application failed to initialize: ${err.message}`, err);
        } catch {
          // LoggerService not available yet, console.error already logged
        }
        
        setState({ ready: false, error: err.message || 'Failed to initialize configuration' });
      }
    };

    initializeConfig();
  }, [appType, staticConfig, dynamicConfig]);

  // Log warnings for missing optional UI components (after state is set)
  useEffect(() => {
    if (state.ready) {
      if (!loadingComponent) {
        console.warn('[AppInitializer] No loadingComponent provided, using default');
      }
      if (!errorComponent) {
        console.warn('[AppInitializer] No errorComponent provided, using default');
      }
      if (!redirectingComponent) {
        console.warn('[AppInitializer] No redirectingComponent provided, using default');
      }
    }
  }, [state.ready, loadingComponent, errorComponent, redirectingComponent]);

  if (state.error) {
    if (errorComponent) {
      return <>{errorComponent(state.error)}</>;
    }

    return (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
        <div className="bg-red-50 border border-red-200 p-6 rounded-lg shadow-md max-w-2xl">
          <h2 className="text-xl font-bold text-red-800 mb-2">Configuration Error</h2>
          <p className="text-red-700 mb-4">{state.error}</p>
          <button
            onClick={() => window.location.reload()}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!state.ready) {
    if (loadingComponent) {
      return <>{loadingComponent}</>;
    }

    return (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center">
        <div className="bg-white p-8 rounded-lg shadow-md">
          <div className="flex items-center space-x-3">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            <div className="text-lg">Initializing application...</div>
          </div>
        </div>
      </div>
    );
  }

  // Build auth config from ConfigService, with optional static overrides
  // When using dynamicConfig, use provided redirect URIs or build from base path
  const authConfig = {
    redirectUri: dynamicConfig?.redirectUri ?? buildAppUrl("signin/callback"),
    postLogoutRedirectUri: dynamicConfig?.postLogoutRedirectUri ?? buildAppUrl("signout/callback"),
    silentRedirectUri: dynamicConfig?.silentRedirectUri ?? buildAppUrl("silent-callback"),
    clientId: staticAuthConfig?.clientId ?? ConfigService.ClientId,
    authority: staticAuthConfig?.authority ?? ConfigService.OpenIdConfig,
  };

  if (dynamicConfig) {
    console.log('AppInitializer: Using dynamic URL config with redirect URIs', {
      redirectUri: authConfig.redirectUri,
      postLogoutRedirectUri: authConfig.postLogoutRedirectUri,
      silentRedirectUri: authConfig.silentRedirectUri,
    });
  }

  if (staticAuthConfig) {
    console.log('AppInitializer: Using staticAuthConfig overrides', {
      clientId: staticAuthConfig.clientId ? '(overridden)' : '(from ConfigService)',
      authority: staticAuthConfig.authority ? '(overridden)' : '(from ConfigService)',
    });
  }

  // If on signin callback route, render SigninCallback component
  if (isSigninCallbackRoute) {
    return (
      <AuthProvider config={authConfig}>
        <SigninCallback
          redirectUrl={getAppBasePath()}
          loadingComponent={loadingComponent}
          errorComponent={errorComponent}
        />
      </AuthProvider>
    );
  }

  // If on signout callback route, render SignoutCallback component
  if (isSignoutCallbackRoute) {
    return (
      <AuthProvider config={authConfig}>
        <SignoutCallback redirectUrl={getAppBasePath()} />
      </AuthProvider>
    );
  }

  // Render children wrapped in AuthProvider, ProtectedRoute, and AppContextProvider
  return (
    <AuthProvider config={authConfig}>
      <AppContextProvider>
        <ProtectedRoute
          loadingComponent={loadingComponent}
          redirectingComponent={redirectingComponent}
        >
          {children}
        </ProtectedRoute>
      </AppContextProvider>
    </AuthProvider>
  );
}
