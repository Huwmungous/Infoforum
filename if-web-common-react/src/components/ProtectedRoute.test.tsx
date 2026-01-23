import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { ProtectedRoute } from './ProtectedRoute';
import { AuthProvider, AuthContextValue } from '../contexts/AuthContext';

// Mock the useAuthInternal hook
const mockAuthContext: AuthContextValue = {
  user: null,
  loading: false,
  error: null,
  initialized: true,
  isAuthenticated: false,
  signin: vi.fn().mockResolvedValue(undefined),
  signout: vi.fn().mockResolvedValue(undefined),
  renewToken: vi.fn().mockResolvedValue({}),
  getAccessToken: vi.fn().mockResolvedValue('token'),
  setUser: vi.fn(),
};

vi.mock('./AuthContext', async () => {
  const actual = await vi.importActual('./AuthContext');
  return {
    ...actual,
    useAuthInternal: () => mockAuthContext,
  };
});

describe('ProtectedRoute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset to default state
    mockAuthContext.user = null;
    mockAuthContext.loading = false;
    mockAuthContext.initialized = true;
    mockAuthContext.isAuthenticated = false;
    mockAuthContext.error = null;
  });

  describe('when loading', () => {
    it('should show default loading UI', () => {
      mockAuthContext.loading = true;
      mockAuthContext.initialized = false;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Loading...')).toBeInTheDocument();
      expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
    });

    it('should show custom loading component when provided', () => {
      mockAuthContext.loading = true;
      mockAuthContext.initialized = false;

      render(
        <ProtectedRoute loadingComponent={<div>Custom Loading...</div>}>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Custom Loading...')).toBeInTheDocument();
    });
  });

  describe('when not initialized', () => {
    it('should show loading UI when not initialized', () => {
      mockAuthContext.initialized = false;
      mockAuthContext.loading = false;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });
  });

  describe('when not authenticated', () => {
    it('should trigger login redirect', async () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = false;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      await waitFor(() => {
        expect(mockAuthContext.signin).toHaveBeenCalled();
      });
    });

    it('should show default redirecting UI', () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = false;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Redirecting to login...')).toBeInTheDocument();
    });

    it('should show custom redirecting component when provided', () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = false;

      render(
        <ProtectedRoute redirectingComponent={<div>Custom Redirect...</div>}>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Custom Redirect...')).toBeInTheDocument();
    });

    it('should only call login once (guard against multiple attempts)', async () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = false;

      const { rerender } = render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      await waitFor(() => {
        expect(mockAuthContext.signin).toHaveBeenCalledTimes(1);
      });

      // Rerender shouldn't trigger another login
      rerender(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(mockAuthContext.signin).toHaveBeenCalledTimes(1);
    });
  });

  describe('when authenticated', () => {
    it('should render children', () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = true;
      mockAuthContext.user = {
        access_token: 'token',
        profile: { sub: 'user-123' },
        expired: false,
      } as any;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Protected Content')).toBeInTheDocument();
    });

    it('should not trigger login', () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = true;

      render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(mockAuthContext.signin).not.toHaveBeenCalled();
    });

    it('should reset login guard when user becomes authenticated', async () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = false;

      const { rerender } = render(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      await waitFor(() => {
        expect(mockAuthContext.signin).toHaveBeenCalledTimes(1);
      });

      // User becomes authenticated
      mockAuthContext.isAuthenticated = true;

      rerender(
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      );

      expect(screen.getByText('Protected Content')).toBeInTheDocument();
    });
  });

  describe('with no children', () => {
    it('should render empty when authenticated', () => {
      mockAuthContext.initialized = true;
      mockAuthContext.isAuthenticated = true;

      const { container } = render(<ProtectedRoute />);

      expect(container.innerHTML).toBe('');
    });
  });
});
