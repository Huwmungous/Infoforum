/**
 * @if/web-common/core - Authentication
 * 
 * Framework-agnostic authentication service using oidc-client-ts.
 */

export { AuthService, authService } from './AuthService';
export type { AuthConfig, UserChangeCallback } from './AuthService';

// Re-export User type from oidc-client-ts for convenience
export type { User } from 'oidc-client-ts';
