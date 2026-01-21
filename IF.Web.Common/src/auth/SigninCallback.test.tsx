import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { SigninCallback } from './SigninCallback';
import { authService } from './AuthService';
import { AuthContextValue } from './AuthContext';

// Mock authService
vi.mock('./AuthService', () => ({
  authService: {
    completeSignin: vi.fn().mockResolvedValue({
      access_token: 'mock-token',
      profile: {
        sub: 'user-123',
        preferred_username: 'testuser',
        email: 'test@example.com',
      },
      scope: 'openid profile email',
    }),
    getUser: vi.fn().mockResolvedValue(null),
  },
}));

// Mock LoggerService
vi.mock('../logger/LoggerService', () => ({
  LoggerService: {
    create: vi.fn(() => ({
      trace: vi.fn(),
      debug: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      critical: vi.fn(),
    })),
  },
}));

// Mock useAuthInternal hook
const mockAuthContext: Partial<AuthContextValue> = {
  setUser: vi.fn(),
  initialized: true,
};

vi.mock('./AuthContext', () => ({
  useAuthInternal: () => mockAuthContext,
}));

// Mock window.location
const mockLocation = {
  href: 'http://localhost:3000/signin/callback',
  assign: vi.fn(),
  replace: vi.fn(),
};

Object.defineProperty(window, 'location', {
  value: mockLocation,
  writable: true,
});

describe('SigninCallback', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthContext.initialized = true;
    mockLocation.href = 'http://localhost:3000/signin/callback';
  });

  describe('loading state', () => {
    it('should show default loading UI', () => {
      // Make completeSignin slow
      (authService.completeSignin as any).mockImplementation(() => new Promise(() => {}));

      render(<SigninCallback />);

      expect(screen.getByText('Completing authentication...')).toBeInTheDocument();
    });

    it('should show custom loading component when provided', () => {
      (authService.completeSignin as any).mockImplementation(() => new Promise(() => {}));

      render(<SigninCallback loadingComponent={<div>Custom Loading...</div>} />);

      expect(screen.getByText('Custom Loading...')).toBeInTheDocument();
    });
  });

  describe('successful login', () => {
    it('should call completeSignin', async () => {
      render(<SigninCallback />);

      await waitFor(() => {
        expect(authService.completeSignin).toHaveBeenCalled();
      });
    });

    it('should call setUser with the returned user', async () => {
      const mockUser = {
        access_token: 'mock-token',
        profile: { sub: 'user-123', preferred_username: 'testuser', email: 'test@example.com' },
        scope: 'openid profile email',
      };
      (authService.completeSignin as any).mockResolvedValue(mockUser);

      render(<SigninCallback />);

      await waitFor(() => {
        expect(mockAuthContext.setUser).toHaveBeenCalledWith(mockUser);
      });
    });

    it('should complete login flow successfully', async () => {
      const mockUser = {
        access_token: 'mock-token',
        profile: {
          sub: 'user-123',
          preferred_username: 'testuser',
          email: 'test@example.com',
        },
        scope: 'openid profile email',
      };
      (authService.completeSignin as any).mockResolvedValue(mockUser);

      render(<SigninCallback />);

      await waitFor(() => {
        expect(authService.completeSignin).toHaveBeenCalled();
        expect(mockAuthContext.setUser).toHaveBeenCalledWith(mockUser);
      });
    });

    it('should call onSuccess callback when provided', async () => {
      const mockUser = {
        access_token: 'mock-token',
        profile: {
          sub: 'user-123',
          preferred_username: 'testuser',
          email: 'test@example.com',
        },
        scope: 'openid profile email',
      };
      (authService.completeSignin as any).mockResolvedValue(mockUser);
      const onSuccess = vi.fn();

      render(<SigninCallback onSuccess={onSuccess} />);

      await waitFor(() => {
        expect(onSuccess).toHaveBeenCalled();
      });
    });
  });

  describe('error handling', () => {
    it('should show default error UI on failure', async () => {
      (authService.completeSignin as any).mockRejectedValue(new Error('Auth failed'));

      render(<SigninCallback />);

      await waitFor(() => {
        expect(screen.getByText('Authentication Error')).toBeInTheDocument();
        expect(screen.getByText('Auth failed')).toBeInTheDocument();
      });
    });

    it('should show custom error component when provided', async () => {
      (authService.completeSignin as any).mockRejectedValue(new Error('Auth failed'));

      render(<SigninCallback errorComponent={(error) => <div>Custom Error: {error}</div>} />);

      await waitFor(() => {
        expect(screen.getByText('Custom Error: Auth failed')).toBeInTheDocument();
      });
    });

    it('should show Go to Home button on error', async () => {
      (authService.completeSignin as any).mockRejectedValue(new Error('Auth failed'));

      render(<SigninCallback />);

      await waitFor(() => {
        expect(screen.getByText('Go to Home')).toBeInTheDocument();
      });
    });
  });

  describe('duplicate callback handling', () => {
    it('should handle "No matching state found" error gracefully', async () => {
      const mockUser = {
        access_token: 'existing-token',
        profile: { sub: 'user-123' },
      };

      (authService.completeSignin as any).mockRejectedValue(new Error('No matching state found'));
      (authService.getUser as any).mockResolvedValue(mockUser);

      render(<SigninCallback />);

      await waitFor(() => {
        expect(authService.getUser).toHaveBeenCalled();
      });

      await waitFor(() => {
        expect(mockAuthContext.setUser).toHaveBeenCalledWith(mockUser);
      });
    });

    it('should show error if duplicate callback and no user', async () => {
      (authService.completeSignin as any).mockRejectedValue(new Error('No matching state found'));
      (authService.getUser as any).mockResolvedValue(null);

      render(<SigninCallback />);

      await waitFor(() => {
        expect(screen.getByText('Authentication Error')).toBeInTheDocument();
      });
    });
  });

  describe('waiting for auth initialization', () => {
    it('should wait for auth to be initialized', () => {
      mockAuthContext.initialized = false;

      render(<SigninCallback />);

      // Should not call completeSignin yet
      expect(authService.completeSignin).not.toHaveBeenCalled();
    });

    it('should call completeSignin after initialization', async () => {
      mockAuthContext.initialized = true;

      render(<SigninCallback />);

      await waitFor(() => {
        expect(authService.completeSignin).toHaveBeenCalled();
      });
    });
  });

  describe('StrictMode protection', () => {
    it('should only process callback once', async () => {
      const { rerender } = render(<SigninCallback />);

      await waitFor(() => {
        expect(authService.completeSignin).toHaveBeenCalledTimes(1);
      });

      // Simulate StrictMode double-invoke
      rerender(<SigninCallback />);

      // Should still only be called once
      expect(authService.completeSignin).toHaveBeenCalledTimes(1);
    });
  });
});
