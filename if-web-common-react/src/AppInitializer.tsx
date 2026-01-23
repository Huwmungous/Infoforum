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
import { getCurrentRoutePath, buildAppUrl, getAppBasePath } from "@if/web-common";

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

export interface AppInitializerProps {
  children: ReactNode;
  appType?: AppType;
  staticConfig?: StaticConfigOverride;
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
 * Usage:
 * <AppInitializer appType="user">
 *   <App />
 * </AppInitializer>
 */
export function AppInitializer({
  children,
  appType = 'user',
  staticConfig,
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

        // Validate environment variables using EnvironmentConfig
        const validationErrors = EnvironmentConfig.validate(!!staticConfig);
        if (validationErrors.length > 0) {
          EnvironmentConfig.logValidationErrors(validationErrors);
          setState({ ready: false, error: 'Configuration validation failed. Check console for details.' });
          return;
        }

        // Log all configuration values after successful validation
        EnvironmentConfig.logConfiguration(!!staticConfig);

        // Setup global fetch interceptor with auth token supplier
        setupFetchInterceptor({
          getAccessToken: () => authService.getAccessToken()
        });

        if (staticConfig) {
          // Use static config - bypasses ConfigWebService
          console.log('AppInitializer: Using static config (bypassing ConfigWebService)');
          ConfigService.initializeWithStaticConfig({
            ...staticConfig,
            realm: EnvironmentConfig.get('IF_REALM'),
            appName: EnvironmentConfig.get('IF_APP_NAME'),
            environment: EnvironmentConfig.get('IF_ENVIRONMENT'),
            appType
          });
        } else {
          // Fetch from ConfigWebService
          console.log(`AppInitializer: Initializing ConfigService with appType: ${appType}`);
          await ConfigService.initialize({
            configServiceUrl: EnvironmentConfig.get('IF_CONFIG_SERVICE_URL'),
            realm: EnvironmentConfig.get('IF_REALM'),
            client: EnvironmentConfig.get('IF_CLIENT'),
            appType,
            appName: EnvironmentConfig.get('IF_APP_NAME'),
            environment: EnvironmentConfig.get('IF_ENVIRONMENT')
          });
        }

        console.log('AppInitializer: ConfigService initialized successfully');

        // CRITICAL: Configure LoggerService AFTER ConfigService is ready
        await LoggerService.configureFromConfigService();
        console.log('AppInitializer: LoggerService configured from ConfigService');

        setState({ ready: true, error: null });
      } catch (err: any) {
        console.error('AppInitializer: Failed to initialize ConfigService:', err);
        setState({ ready: false, error: err.message || 'Failed to initialize configuration' });
      }
    };

    initializeConfig();
  }, [appType, staticConfig]);

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
  const authConfig = {
    redirectUri: buildAppUrl("signin/callback"),
    postLogoutRedirectUri: buildAppUrl("signout/callback"),
    silentRedirectUri: buildAppUrl("silent-callback"),
    clientId: staticAuthConfig?.clientId ?? ConfigService.ClientId,
    authority: staticAuthConfig?.authority ?? ConfigService.OpenIdConfig,
  };

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
