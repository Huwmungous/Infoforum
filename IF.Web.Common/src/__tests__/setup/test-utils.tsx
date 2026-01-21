import React, { ReactElement } from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { vi } from 'vitest';
import { AuthContextValue } from '../../auth/AuthContext';
type LoggerContextValue = {
  getLogger: (category: string) => any;
};

// Mock bootstrap config response
export const mockBootstrapConfig = {
  clientId: 'test-client-id',
  openIdConfig: 'https://auth.example.com/realms',
  loggerService: 'https://logger.example.com/api/log',
  logLevel: 'Information',
};

// Mock environment variables
export const mockEnvVars: Record<string, string> = {
  VITE_SFD_CONFIG_SERVICE: 'https://config.example.com',
  VITE_SFD_REALM: 'test-realm',
  VITE_SFD_APP_NAME: 'TestApp',
  VITE_SFD_ENVIRONMENT: 'test',
  VITE_SFD_CLIENT: 'test-client',
  VITE_LOG_LEVEL: 'Information',
};

// Create mock fetch response
export function createMockFetchResponse<T>(data: T, ok = true, status = 200) {
  return vi.fn().mockResolvedValue({
    ok,
    status,
    statusText: ok ? 'OK' : 'Error',
    json: vi.fn().mockResolvedValue(data),
    text: vi.fn().mockResolvedValue(JSON.stringify(data)),
    headers: new Headers({ 'Content-Type': 'application/json' }),
  });
}

// Create a mock AuthContext value
export function createMockAuthContext(overrides?: Partial<AuthContextValue>): AuthContextValue {
  return {
    user: null,
    loading: false,
    error: null,
    initialized: true,
    isAuthenticated: false,
    signin: vi.fn().mockResolvedValue(undefined),
    signout: vi.fn().mockResolvedValue(undefined),
    renewToken: vi.fn().mockResolvedValue({
      access_token: 'new-token',
      profile: { sub: 'user-123' },
    }),
    getAccessToken: vi.fn().mockResolvedValue('mock-token'),
    setUser: vi.fn(),
    ...overrides,
  };
}

// Create a mock LoggerContext value
export function createMockLoggerContext(): LoggerContextValue {
  const mockLogger = {
    trace: vi.fn(),
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    critical: vi.fn(),
  };

  return {
    getLogger: vi.fn().mockReturnValue(mockLogger),
  };
}

// Create mock logger
export function createMockLogger() {
  return {
    trace: vi.fn(),
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    critical: vi.fn(),
    isEnabled: vi.fn().mockReturnValue(true),
    log: vi.fn(),
  };
}

// Custom render function with providers
interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  authContextValue?: Partial<AuthContextValue>;
  loggerContextValue?: LoggerContextValue;
}

// Re-export everything from testing-library
export * from '@testing-library/react';
export { vi } from 'vitest';
