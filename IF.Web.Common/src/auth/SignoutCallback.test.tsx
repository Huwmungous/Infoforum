import React from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { SignoutCallback } from './SignoutCallback';
import { authService } from './AuthService';

// Mock authService
vi.mock('./AuthService', () => ({
  authService: {
    completeSignout: vi.fn().mockResolvedValue(undefined),
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

// Mock window.location
const mockLocation = {
  href: 'http://localhost:3000/signout/callback',
  replace: vi.fn(),
};

Object.defineProperty(window, 'location', {
  value: mockLocation,
  writable: true,
});

describe('SignoutCallback', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLocation.href = 'http://localhost:3000/signout/callback';
  });

  describe('loading state', () => {
    it('should show default loading UI', () => {
      (authService.completeSignout as any).mockImplementation(() => new Promise(() => {}));

      render(<SignoutCallback />);

      expect(screen.getByText('Completing sign out...')).toBeInTheDocument();
    });
  });

  describe('successful signout', () => {
    it('should call completeSignout', async () => {
      render(<SignoutCallback />);

      await waitFor(() => {
        expect(authService.completeSignout).toHaveBeenCalled();
      });
    });

    it('should redirect to default "/" after successful signout', async () => {
      render(<SignoutCallback />);

      await waitFor(() => {
        expect(mockLocation.replace).toHaveBeenCalledWith('/');
      });
    });

    it('should redirect to custom URL when redirectUrl is provided', async () => {
      render(<SignoutCallback redirectUrl="/home" />);

      await waitFor(() => {
        expect(mockLocation.replace).toHaveBeenCalledWith('/home');
      });
    });
  });

  describe('error handling', () => {
    it('should still redirect on error', async () => {
      (authService.completeSignout as any).mockRejectedValue(new Error('Signout failed'));

      render(<SignoutCallback />);

      await waitFor(() => {
        expect(mockLocation.replace).toHaveBeenCalledWith('/');
      });
    });

    it('should redirect to custom URL on error when redirectUrl is provided', async () => {
      (authService.completeSignout as any).mockRejectedValue(new Error('Signout failed'));

      render(<SignoutCallback redirectUrl="/login" />);

      await waitFor(() => {
        expect(mockLocation.replace).toHaveBeenCalledWith('/login');
      });
    });
  });

  describe('StrictMode protection', () => {
    it('should only process callback once', async () => {
      const { rerender } = render(<SignoutCallback />);

      await waitFor(() => {
        expect(authService.completeSignout).toHaveBeenCalledTimes(1);
      });

      // Simulate StrictMode double-invoke
      rerender(<SignoutCallback />);

      // Should still only be called once
      expect(authService.completeSignout).toHaveBeenCalledTimes(1);
    });
  });
});
