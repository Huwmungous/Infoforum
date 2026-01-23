/**
 * @if/web-common
 * 
 * Framework-agnostic library for authentication, configuration, and logging.
 * Works with any TypeScript framework: Angular, React, Vue, etc.
 * 
 * Usage:
 *   import { AuthService, ConfigService, LoggerService } from '@if/web-common';
 */

// Authentication
export { AuthService, authService } from './auth';
export type { AuthConfig, UserChangeCallback } from './auth';
export type { User } from 'oidc-client-ts';

// Configuration
export { ConfigService, EnvironmentConfig } from './config';
export type { AppType, BootstrapConfig, ConfigServiceInitParams } from './config';

// Logging
export { LoggerService, IfLogger, IfLoggerProvider, IfLoggerExtensions } from './logger';
export type { LogLevel, IfLoggerConfiguration, IfLogEntry } from './logger';

// HTTP
export { api, setupFetchInterceptor } from './http';

// Routing utilities
export { normalizeBase, getAppBasePath, getCurrentRoutePath, buildAppUrl } from './routing';
