/**
 * @if/web-common-react
 * 
 * React bindings for @if/web-common.
 * Provides components, hooks, and context providers for authentication and configuration.
 * 
 * Usage:
 *   import { AppInitializer, useAuth, useAppContext } from '@if/web-common-react';
 */

// Main entry component
export { AppInitializer } from './AppInitializer';
export type { AppInitializerProps, StaticConfigOverride, StaticAuthConfigOverride } from './AppInitializer';

// Contexts and hooks
export { AppContextProvider, useAppContext, useAuth } from './contexts';
export type { AppContextValue, Logger } from './contexts';

export { AuthProvider, useAuthInternal } from './contexts';
export type { AuthContextValue } from './contexts';

// Components
export { ProtectedRoute, SigninCallback, SignoutCallback, SilentCallback } from './components';
export type { ProtectedRouteProps, SigninCallbackProps, SignoutCallbackProps } from './components';
