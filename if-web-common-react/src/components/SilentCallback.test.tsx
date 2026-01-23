import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { SilentCallback } from './SilentCallback';

// Mock authService
const mockCompleteSilentSignin = vi.fn().mockResolvedValue(undefined);

vi.mock('@if/web-common', () => ({
  authService: {
    completeSilentSignin: () => mockCompleteSilentSignin(),
  },
}));

describe('SilentCallback', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCompleteSilentSignin.mockResolvedValue(undefined);
  });

  it('should render nothing', () => {
    const { container } = render(<SilentCallback />);
    expect(container.firstChild).toBeNull();
  });

  it('should call completeSilentSignin on mount', async () => {
    render(<SilentCallback />);

    // Wait for useEffect to run
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(mockCompleteSilentSignin).toHaveBeenCalled();
  });

  it('should handle callback errors gracefully', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    mockCompleteSilentSignin.mockRejectedValueOnce(new Error('Callback failed'));

    render(<SilentCallback />);

    // Wait for the promise to reject
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(consoleError).toHaveBeenCalledWith('Silent callback error:', expect.any(Error));
    consoleError.mockRestore();
  });
});
