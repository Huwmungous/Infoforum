// src/urlConfig.ts
// Extracts appDomain from URL path for dynamic configuration

export interface UrlConfig {
  appDomain: string;
}

/**
 * Extract appDomain from URL path.
 * 
 * Expected URL pattern:
 *   /{appDomain}/ifollama/...
 * 
 * Examples:
 *   https://longmanrd.net/infoforum/ifollama/
 *   http://localhost:5029/infoforum/ifollama/
 * 
 * @returns UrlConfig with appDomain, or null if not found
 */
export function getConfigFromUrl(): UrlConfig | null {
  const pathname = window.location.pathname;
  
  // Pattern: /{appDomain}/ifollama/...
  const match = pathname.match(/^\/([^/]+)\/ifollama/i);
  
  if (match) {
    const [, appDomain] = match;
    
    // Filter out known route segments that aren't appDomain
    const routeSegments = ['signin', 'signout', 'silent-callback', 'callback', 'api', 'config'];
    if (routeSegments.includes(appDomain.toLowerCase())) {
      return null;
    }
    
    return { appDomain };
  }
  
  return null;
}

/**
 * Get the base path for the application including appDomain.
 * Used for constructing redirect URIs.
 * 
 * @param urlConfig The extracted URL config
 * @returns Base path like "/infoforum/ifollama"
 */
export function getAppBasePath(urlConfig: UrlConfig): string {
  return `/${urlConfig.appDomain}/ifollama`;
}

/**
 * Build a full URL for the application.
 * 
 * @param urlConfig The extracted URL config
 * @param path Additional path to append (e.g., "signin/callback")
 * @returns Full URL like "https://longmanrd.net/infoforum/ifollama/signin/callback"
 */
export function buildFullUrl(urlConfig: UrlConfig, path?: string): string {
  const origin = window.location.origin;
  const basePath = getAppBasePath(urlConfig);
  
  if (path) {
    return `${origin}${basePath}/${path}`;
  }
  
  return `${origin}${basePath}`;
}
