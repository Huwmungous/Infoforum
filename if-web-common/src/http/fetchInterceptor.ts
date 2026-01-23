/**
 * Global fetch interceptor that automatically adds auth token to all requests
 * PLUS convenience API wrapper (GET, POST, PUT, DELETE)
 *
 * NOTE: This module uses console.debug directly instead of LoggerService
 * to avoid infinite loops (LoggerService uses fetch for remote logging).
 */

export interface FetchInterceptorOptions {
  /** Async function to get the current access token. Returns null if no token available. */
  getAccessToken: () => Promise<string | null>;
}

export function setupFetchInterceptor(options: FetchInterceptorOptions) {
  if (typeof window === 'undefined') return;

  const { getAccessToken } = options;
  const originalFetch = window.fetch;

  window.fetch = async function (...args: any[]) {
    let [url, fetchOptions = {}] = args;

    const token = await getAccessToken();

    // Add Authorization header if token exists
    if (token) {
      fetchOptions.headers = {
        ...fetchOptions.headers,
        "Authorization": `Bearer ${token}`
      };
      console.debug(`[FetchInterceptor] Token attached to request: ${url}`);
    } else {
      console.debug(`[FetchInterceptor] No token available for request: ${url}`);
    }

    return originalFetch(url, fetchOptions);
  };
}

/* -------------------------------------------------------
 *   Enhanced Fetch API (GET, POST, PUT, DELETE)
 * -----------------------------------------------------*/

export const api = {
  async get<T = any>(url: string, options: RequestInit = {}): Promise<T> {
    return request<T>(url, { ...options, method: "GET" });
  },

  async post<T = any>(
    url: string,
    body?: any,
    options: RequestInit = {}
  ): Promise<T> {
    return request<T>(url, {
      ...options,
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {})
      },
      body: body !== undefined ? JSON.stringify(body) : undefined
    });
  },

  async put<T = any>(
    url: string,
    body?: any,
    options: RequestInit = {}
  ): Promise<T> {
    return request<T>(url, {
      ...options,
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {})
      },
      body: body !== undefined ? JSON.stringify(body) : undefined
    });
  },

  async delete<T = any>(
    url: string,
    options: RequestInit = {}
  ): Promise<T> {
    return request<T>(url, { ...options, method: "DELETE" });
  }
};

/**
 * Core request wrapper used by all api.* methods
 */
async function request<T>(url: string, options: RequestInit): Promise<T> {
  const response = await fetch(url, options);

  if (!response.ok) {
    const text = await response.text().catch(() => "");
    throw new Error(
      `Request failed: ${response.status} ${response.statusText}\n${text}`
    );
  }

  const contentType = response.headers.get("Content-Type")?.toLowerCase() || "";

  if (contentType.includes("application/json")) {
    return response.json() as Promise<T>;
  }

  // Fallback to text responses
  return (await response.text()) as unknown as T;
}

export default setupFetchInterceptor;