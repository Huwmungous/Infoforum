import '@testing-library/jest-dom';
import { vi, beforeEach, afterEach } from 'vitest';

// Mock import.meta.env
const mockEnv: Record<string, string> = {};

vi.stubGlobal('import', {
  meta: {
    env: mockEnv,
  },
});

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
    get length() {
      return Object.keys(store).length;
    },
    key: vi.fn((index: number) => Object.keys(store)[index] ?? null),
  };
})();

vi.stubGlobal('localStorage', localStorageMock);

// Mock window.location
const locationMock = {
  href: 'http://localhost:3000',
  origin: 'http://localhost:3000',
  pathname: '/',
  search: '',
  hash: '',
  host: 'localhost:3000',
  hostname: 'localhost',
  port: '3000',
  protocol: 'http:',
  assign: vi.fn(),
  reload: vi.fn(),
  replace: vi.fn(),
};

vi.stubGlobal('location', locationMock);

// Mock window.__IF_CONFIG__
(window as any).__IF_CONFIG__ = undefined;

// Mock fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// Mock console methods to avoid noise in tests
const originalConsole = { ...console };
vi.stubGlobal('console', {
  ...console,
  log: vi.fn(),
  debug: vi.fn(),
  info: vi.fn(),
  warn: vi.fn(),
  error: vi.fn(),
});

// Helper to set mock environment variables
export function setMockEnv(env: Record<string, string>) {
  Object.assign(mockEnv, env);
}

// Helper to clear mock environment variables
export function clearMockEnv() {
  Object.keys(mockEnv).forEach((key) => delete mockEnv[key]);
}

// Helper to set window runtime config
export function setRuntimeConfig(config: Record<string, string> | undefined) {
  (window as any).__IF_CONFIG__ = config;
}

// Helper to get mock fetch
export function getMockFetch() {
  return mockFetch;
}

// Helper to restore console
export function restoreConsole() {
  vi.stubGlobal('console', originalConsole);
}

// Reset all mocks between tests
beforeEach(() => {
  vi.clearAllMocks();
  localStorageMock.clear();
  clearMockEnv();
  setRuntimeConfig(undefined);
  mockFetch.mockReset();
});

afterEach(() => {
  vi.restoreAllMocks();
});
