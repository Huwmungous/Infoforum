import { describe, it, expect, beforeEach, afterEach, vi, Mock } from 'vitest';
import { setupFetchInterceptor, api } from './fetchInterceptor';

describe('fetchInterceptor', () => {
  let originalFetch: typeof fetch;
  let mockFetch: Mock<typeof fetch>;
  let mockGetAccessToken: Mock<() => Promise<string | null>>;

  beforeEach(() => {
    // Store original fetch
    originalFetch = window.fetch;

    // Create mock fetch
    mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: vi.fn().mockResolvedValue({ data: 'test' }),
      text: vi.fn().mockResolvedValue('test'),
    });

    window.fetch = mockFetch;

    // Create mock token supplier (returns null by default)
    mockGetAccessToken = vi.fn<() => Promise<string | null>>().mockResolvedValue(null);
  });

  afterEach(() => {
    // Restore original fetch
    window.fetch = originalFetch;
  });

  describe('setupFetchInterceptor', () => {
    it('should intercept fetch calls', async () => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data');

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.any(Object)
      );
    });

    it('should call tokenSupplier and add Authorization header when token is non-null', async () => {
      mockGetAccessToken.mockResolvedValue('valid-access-token');

      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data');

      expect(mockGetAccessToken).toHaveBeenCalled();
      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'Bearer valid-access-token',
          }),
        })
      );
    });

    it('should NOT add Authorization header when tokenSupplier returns null', async () => {
      mockGetAccessToken.mockResolvedValue(null);

      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data', { headers: {} });

      expect(mockGetAccessToken).toHaveBeenCalled();
      const callArgs = mockFetch.mock.calls[0];
      const headers = callArgs[1]?.headers as Record<string, string> | undefined;
      expect(headers?.Authorization).toBeUndefined();
    });

    it('should preserve existing headers when adding Authorization', async () => {
      mockGetAccessToken.mockResolvedValue('valid-token');

      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data', {
        headers: {
          'Content-Type': 'application/json',
          'X-Custom-Header': 'custom-value',
        },
      });

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
            'X-Custom-Header': 'custom-value',
            Authorization: 'Bearer valid-token',
          }),
        })
      );
    });

    it('should call tokenSupplier on each fetch request', async () => {
      mockGetAccessToken
        .mockResolvedValueOnce('token-1')
        .mockResolvedValueOnce('token-2');

      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data1');
      await fetch('https://api.example.com/data2');

      expect(mockGetAccessToken).toHaveBeenCalledTimes(2);
    });

    it('should not read localStorage for tokens', async () => {
      const localStorageSpy = vi.spyOn(Storage.prototype, 'getItem');
      const localStorageKeySpy = vi.spyOn(Storage.prototype, 'key');

      mockGetAccessToken.mockResolvedValue('injected-token');

      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });

      await fetch('https://api.example.com/data');

      // Verify localStorage was never accessed for token retrieval
      expect(localStorageSpy).not.toHaveBeenCalled();
      expect(localStorageKeySpy).not.toHaveBeenCalled();

      localStorageSpy.mockRestore();
      localStorageKeySpy.mockRestore();
    });
  });

  describe('api.get', () => {
    beforeEach(() => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });
    });

    it('should make GET request', async () => {
      const result = await api.get<{ data: string }>('https://api.example.com/data');

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({ method: 'GET' })
      );
      expect(result).toEqual({ data: 'test' });
    });

    it('should accept custom options', async () => {
      await api.get('https://api.example.com/data', {
        headers: { 'X-Custom': 'value' },
      });

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({
          method: 'GET',
          headers: expect.objectContaining({
            'X-Custom': 'value',
          }),
        })
      );
    });

    it('should throw on HTTP error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        text: vi.fn().mockResolvedValue('Resource not found'),
      } as unknown as Response);

      await expect(api.get('https://api.example.com/data')).rejects.toThrow(
        'Request failed: 404 Not Found'
      );
    });
  });

  describe('api.post', () => {
    beforeEach(() => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });
    });

    it('should make POST request with JSON body', async () => {
      const body = { name: 'test', value: 123 };

      await api.post('https://api.example.com/data', body);

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({
          method: 'POST',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify(body),
        })
      );
    });

    it('should handle undefined body', async () => {
      await api.post('https://api.example.com/data');

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data',
        expect.objectContaining({
          method: 'POST',
          body: undefined,
        })
      );
    });
  });

  describe('api.put', () => {
    beforeEach(() => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });
    });

    it('should make PUT request with JSON body', async () => {
      const body = { id: 1, name: 'updated' };

      await api.put('https://api.example.com/data/1', body);

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data/1',
        expect.objectContaining({
          method: 'PUT',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify(body),
        })
      );
    });
  });

  describe('api.delete', () => {
    beforeEach(() => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });
    });

    it('should make DELETE request', async () => {
      await api.delete('https://api.example.com/data/1');

      expect(mockFetch).toHaveBeenCalledWith(
        'https://api.example.com/data/1',
        expect.objectContaining({
          method: 'DELETE',
        })
      );
    });
  });

  describe('response handling', () => {
    beforeEach(() => {
      setupFetchInterceptor({ getAccessToken: mockGetAccessToken });
    });

    it('should parse JSON response', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        headers: new Headers({ 'Content-Type': 'application/json' }),
        json: vi.fn().mockResolvedValue({ result: 'success' }),
      } as unknown as Response);

      const result = await api.get('https://api.example.com/data');

      expect(result).toEqual({ result: 'success' });
    });

    it('should return text for non-JSON response', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        headers: new Headers({ 'Content-Type': 'text/plain' }),
        text: vi.fn().mockResolvedValue('plain text response'),
      } as unknown as Response);

      const result = await api.get<string>('https://api.example.com/data');

      expect(result).toBe('plain text response');
    });

    it('should handle empty Content-Type header', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        headers: new Headers({}),
        text: vi.fn().mockResolvedValue('response'),
      } as unknown as Response);

      const result = await api.get<string>('https://api.example.com/data');

      expect(result).toBe('response');
    });

    it('should include response body in error message', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 400,
        statusText: 'Bad Request',
        text: vi.fn().mockResolvedValue('Validation error: field is required'),
      } as unknown as Response);

      await expect(api.get('https://api.example.com/data')).rejects.toThrow(
        'Request failed: 400 Bad Request\nValidation error: field is required'
      );
    });
  });
});
