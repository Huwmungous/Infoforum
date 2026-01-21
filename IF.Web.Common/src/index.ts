/**
 * @sfd/web-common Public API
 *
 * This module exposes only the public contract:
 * - AppInitializer: Main entry point component
 * - useAppContext: Hook for accessing app context (config, auth, createLogger)
 * - useAuth: Convenience hook for auth-only access
 * - Types: AppContextValue, Logger, AuthUser, AppInitializerProps, StaticConfigOverride
 */

// Main entry point component
export { AppInitializer } from './AppInitializer';
export { api } from './fetchInterceptor';
export { LoggerService } from './logger';
export type { AppInitializerProps, StaticConfigOverride } from './AppInitializer';

// Hooks
export { useAppContext, useAuth } from './appContext';

// Types
export type { AppContextValue, Logger } from './appContext';

