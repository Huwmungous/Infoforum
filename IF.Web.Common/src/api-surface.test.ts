/**
 * API Surface Guard Test
 *
 * This test ensures that only the intended public API is exported from the root module.
 * If this test fails, it means internal modules are leaking through the public API.
 *
 * Public Contract:
 * - AppInitializer (component)
 * - useAppContext (hook)
 * - useAuth (hook)
 * - Types: AppContextValue, Logger, AuthUser, AppInitializerProps, StaticConfigOverride
 */

import { describe, it, expect } from 'vitest';
import * as WebCommon from './index';

describe('API Surface Guard', () => {
  // The exact set of allowed exports from the root module
  const ALLOWED_EXPORTS = new Set([
    'AppInitializer',
    'useAppContext',
    'useAuth',
    // Note: Types are not runtime exports, they're only in .d.ts files
  ]);

  // Known internal modules that should NOT be exported
  const FORBIDDEN_EXPORTS = [
    // Internal services
    'ConfigService',
    'EnvironmentConfig',
    'LoggerService',
    'AuthService',
    'authService',

    // Internal logger classes
    'SfdLogger',
    'SfdLoggerProvider',
    'SfdLoggerExtensions',

    // Internal React components/hooks
    'LoggerProvider',
    'LoggerContext',
    'useLogger',

    // Internal auth components
    'AuthProvider',
    'AuthContext',
    'ProtectedRoute',
    'SigninCallback',
    'SignoutCallback',
    'useAuthInternal',

    // Internal utilities
    'setupFetchInterceptor',
  ];

  it('should only export the public contract', () => {
    const actualExports = Object.keys(WebCommon);

    // Check that all actual exports are in the allowed list
    for (const exportName of actualExports) {
      expect(
        ALLOWED_EXPORTS.has(exportName),
        `Unexpected export "${exportName}" found in root module. ` +
        `This may be an internal leak. Allowed exports: ${[...ALLOWED_EXPORTS].join(', ')}`
      ).toBe(true);
    }
  });

  it('should export all required public API members', () => {
    const actualExports = new Set(Object.keys(WebCommon));

    for (const requiredExport of ALLOWED_EXPORTS) {
      expect(
        actualExports.has(requiredExport),
        `Required export "${requiredExport}" is missing from root module`
      ).toBe(true);
    }
  });

  it('should NOT export internal modules', () => {
    const actualExports = new Set(Object.keys(WebCommon));

    for (const forbidden of FORBIDDEN_EXPORTS) {
      expect(
        actualExports.has(forbidden),
        `Internal module "${forbidden}" is leaking through the public API`
      ).toBe(false);
    }
  });

  it('should export AppInitializer as a function', () => {
    expect(typeof WebCommon.AppInitializer).toBe('function');
  });

  it('should export useAppContext as a function', () => {
    expect(typeof WebCommon.useAppContext).toBe('function');
  });

  it('should export useAuth as a function', () => {
    expect(typeof WebCommon.useAuth).toBe('function');
  });

  it('should have exactly the expected number of exports', () => {
    const actualExports = Object.keys(WebCommon);
    expect(
      actualExports.length,
      `Expected ${ALLOWED_EXPORTS.size} exports, but found ${actualExports.length}: ${actualExports.join(', ')}`
    ).toBe(ALLOWED_EXPORTS.size);
  });
});
