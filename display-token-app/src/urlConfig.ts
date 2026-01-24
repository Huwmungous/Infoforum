// src/urlConfig.ts
// Extracts realm and client from URL path for dynamic configuration

export interface UrlConfig {
  realm: string;
  client: string;
}

/**
 * Extract realm and client from URL path.
 * 
 * Expected URL patterns:
 *   /tokens/{realm}/{client}/...
 *   /{realm}/{client}/...  (when base is /)
 * 
 * Examples:
 *   https://example.com/tokens/IfDevelopment_Dev/dev-login/
 *   http://localhost:5313/IfDevelopment_Dev/dev-login/
 * 
 * @returns UrlConfig with realm and client, or null if not found
 */
export function getConfigFromUrl(): UrlConfig | null {
  const pathname = window.location.pathname;
  
  // Try pattern with /tokens/ prefix first (production)
  let match = pathname.match(/\/tokens\/([^/]+)\/([^/]+)/);
  
  // If no match, try pattern without prefix (development)
  // Pattern: /{realm}/{client}/... where realm and client are the first two segments
  if (!match) {
    match = pathname.match(/^\/([^/]+)\/([^/]+)/);
  }
  
  if (match) {
    const [, realm, client] = match;
    
    // Filter out known route segments that aren't realm/client
    const routeSegments = ['signin', 'signout', 'silent-callback', 'callback'];
    if (routeSegments.includes(realm.toLowerCase())) {
      return null;
    }
    
    return { realm, client };
  }
  
  return null;
}

/**
 * Get the base path for the application including realm/client.
 * Used for constructing redirect URIs.
 * 
 * @param urlConfig The extracted URL config
 * @returns Base path like "/tokens/IfDevelopment_Dev/dev-login"
 */
export function getAppBasePath(urlConfig: UrlConfig): string {
  const pathname = window.location.pathname;
  
  // Check if we're in production with /tokens/ prefix
  if (pathname.includes('/tokens/')) {
    return `/tokens/${urlConfig.realm}/${urlConfig.client}`;
  }
  
  // Development mode - just realm/client
  return `/${urlConfig.realm}/${urlConfig.client}`;
}

/**
 * Build a full URL for the application.
 * 
 * @param urlConfig The extracted URL config
 * @param path Additional path to append (e.g., "signin/callback")
 * @returns Full URL like "https://example.com/tokens/realm/client/signin/callback"
 */
export function buildFullUrl(urlConfig: UrlConfig, path?: string): string {
  const origin = window.location.origin;
  const basePath = getAppBasePath(urlConfig);
  
  if (path) {
    return `${origin}${basePath}/${path}`;
  }
  
  return `${origin}${basePath}`;
}
