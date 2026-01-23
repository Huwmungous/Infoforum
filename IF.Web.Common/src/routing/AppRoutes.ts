// Runtime override for dynamic URL-based configuration
let _dynamicBasePath: string | null = null;

/**
 * Set dynamic base path at runtime (for URL-based realm/client).
 * Call this before AppInitializer renders.
 */
export function setDynamicBasePath(basePath: string | null): void {
  _dynamicBasePath = basePath;
}

/**
 * Get the current dynamic base path, if set.
 */
export function getDynamicBasePath(): string | null {
  return _dynamicBasePath;
}

export function normalizeBase(base: string): string {
  if (!base) return "/";
  if (!base.startsWith("/")) base = "/" + base;
  if (!base.endsWith("/")) base = base + "/";
  return base;
}

// Test override for import.meta.env.BASE_URL (not available at runtime)
let _testBasePathOverride: string | null = null;

/** @internal For testing only */
export function _setTestBasePath(base: string | null): void {
  _testBasePathOverride = base;
}

export function getAppBasePath(): string {
  // Test override takes precedence
  if (_testBasePathOverride !== null) return normalizeBase(_testBasePathOverride);

  // Dynamic base path (from URL) takes precedence over Vite base
  if (_dynamicBasePath !== null) return normalizeBase(_dynamicBasePath);

  // Best source in Vite apps: this reflects the configured `base`.
  // Falls back safely if not present.
  const viteBase =
    typeof import.meta !== "undefined" &&
    (import.meta as any).env &&
    (import.meta as any).env.BASE_URL;

  if (typeof viteBase === "string") return normalizeBase(viteBase);

  // Fallback: infer first segment (works for your /logs hosting).
  if (typeof window === "undefined") return "/";
  const parts = window.location.pathname.split("/").filter(Boolean);
  return parts.length > 0 ? `/${parts[0]}/` : "/";
}

export function getCurrentRoutePath(): string {
  if (typeof window === "undefined") return "/";

  const { pathname, hash } = window.location;

  // Hash routing: "#/x" => "/x"
  if (hash && hash.startsWith("#/")) return hash.slice(1);

  // Standard routing: strip base path so callers can compare to "/signin/callback"
  const base = getAppBasePath(); // "/tokens/realm/client/" or "/"
  if (base !== "/" && pathname.startsWith(base)) {
    const rest = pathname.slice(base.length - 1); // keep leading "/"
    return rest || "/";
  }

  return pathname || "/";
}

export function buildAppUrl(relativePath: string): string {
  const base = getAppBasePath();
  const rel = relativePath.startsWith("/") ? relativePath.slice(1) : relativePath;
  return new URL(base + rel, window.location.origin).toString();
}
