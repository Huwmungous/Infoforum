import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  normalizeBase,
  getAppBasePath,
  getCurrentRoutePath,
  buildAppUrl,
  _setTestBasePath,
} from "./AppRoutes";

// Store original window
const originalWindow = global.window;

// Helper to mock window.location
function mockWindowLocation(overrides: Partial<Location>) {
  const location = {
    pathname: "/",
    hash: "",
    origin: "http://localhost:3000",
    ...overrides,
  } as Location;

  Object.defineProperty(global, "window", {
    value: { location },
    writable: true,
    configurable: true,
  });
}

// Helper to remove window (SSR simulation)
function removeWindow() {
  Object.defineProperty(global, "window", {
    value: undefined,
    writable: true,
    configurable: true,
  });
}

describe("AppRoutes", () => {
  afterEach(() => {
    global.window = originalWindow;
    _setTestBasePath(null);
  });

  describe("normalizeBase", () => {
    it("should return '/' for empty string", () => {
      expect(normalizeBase("")).toBe("/");
    });

    it("should add leading slash when missing", () => {
      expect(normalizeBase("someRoute/")).toBe("/someRoute/");
    });

    it("should add trailing slash when missing", () => {
      expect(normalizeBase("/someRoute")).toBe("/someRoute/");
    });

    it("should add both slashes when both missing", () => {
      expect(normalizeBase("someRoute")).toBe("/someRoute/");
    });

    it("should preserve already normalized base", () => {
      expect(normalizeBase("/someRoute/")).toBe("/someRoute/");
    });

    it("should handle single slash", () => {
      expect(normalizeBase("/")).toBe("/");
    });
  });

  describe("getAppBasePath", () => {
    it("should return test override when set", () => {
      _setTestBasePath("/someRoute");
      mockWindowLocation({ pathname: "/other/page" });

      const result = getAppBasePath();

      expect(result).toBe("/someRoute/");
    });

    it("should normalize test override", () => {
      _setTestBasePath("someRoute");
      mockWindowLocation({ pathname: "/someRoute/page" });

      const result = getAppBasePath();

      expect(result).toBe("/someRoute/");
    });

    it("should return '/' for root base", () => {
      _setTestBasePath("/");
      mockWindowLocation({ pathname: "/" });

      const result = getAppBasePath();

      expect(result).toBe("/");
    });

    it("should return '/' when window is undefined (SSR)", () => {
      removeWindow();

      const result = getAppBasePath();

      expect(result).toBe("/");
    });

    it("should use Vite BASE_URL when no override (returns / in test env)", () => {
      // In Vitest, import.meta.env.BASE_URL defaults to "/"
      mockWindowLocation({ pathname: "/" });

      const result = getAppBasePath();

      expect(result).toBe("/");
    });
  });

  describe("getCurrentRoutePath", () => {
    it("should return '/' when window is undefined (SSR)", () => {
      removeWindow();

      const result = getCurrentRoutePath();

      expect(result).toBe("/");
    });

    it("should extract path from hash routing", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        hash: "#/signin/callback",
      });

      const result = getCurrentRoutePath();

      expect(result).toBe("/signin/callback");
    });

    it("should handle hash with just root", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        hash: "#/",
      });

      const result = getCurrentRoutePath();

      expect(result).toBe("/");
    });

    it("should strip base path in standard routing", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({ pathname: "/someRoute/dashboard" });

      const result = getCurrentRoutePath();

      expect(result).toBe("/dashboard");
    });

    it("should return '/' when at base path root", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({ pathname: "/someRoute/" });

      const result = getCurrentRoutePath();

      expect(result).toBe("/");
    });

    it("should return pathname as-is when base is '/'", () => {
      _setTestBasePath("/");
      mockWindowLocation({ pathname: "/about" });

      const result = getCurrentRoutePath();

      expect(result).toBe("/about");
    });

    it("should ignore non-path hashes and use pathname", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/page",
        hash: "#section-anchor",
      });

      const result = getCurrentRoutePath();

      // Hash doesn't start with "#/", so falls through to pathname logic
      expect(result).toBe("/page");
    });

    it("should handle deep nested paths", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({ pathname: "/someRoute/auth/signin/callback" });

      const result = getCurrentRoutePath();

      expect(result).toBe("/auth/signin/callback");
    });

    it("should handle empty hash", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/page",
        hash: "",
      });

      const result = getCurrentRoutePath();

      expect(result).toBe("/page");
    });
  });

  describe("buildAppUrl", () => {
    it("should build URL with relative path (no leading slash)", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        origin: "http://localhost:3000",
      });

      const result = buildAppUrl("dashboard");

      expect(result).toBe("http://localhost:3000/someRoute/dashboard");
    });

    it("should build URL with relative path (with leading slash)", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        origin: "http://localhost:3000",
      });

      const result = buildAppUrl("/dashboard");

      expect(result).toBe("http://localhost:3000/someRoute/dashboard");
    });

    it("should handle nested paths", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        origin: "http://localhost:3000",
      });

      const result = buildAppUrl("auth/callback");

      expect(result).toBe("http://localhost:3000/someRoute/auth/callback");
    });

    it("should work with root base path", () => {
      _setTestBasePath("/");
      mockWindowLocation({
        pathname: "/",
        origin: "http://localhost:3000",
      });

      const result = buildAppUrl("signin");

      expect(result).toBe("http://localhost:3000/signin");
    });

    it("should handle empty relative path", () => {
      _setTestBasePath("/someRoute/");
      mockWindowLocation({
        pathname: "/someRoute/",
        origin: "http://localhost:3000",
      });

      const result = buildAppUrl("");

      expect(result).toBe("http://localhost:3000/someRoute/");
    });

    it("should handle different origins", () => {
      _setTestBasePath("/app/");
      mockWindowLocation({
        pathname: "/app/",
        origin: "https://example.com",
      });

      const result = buildAppUrl("settings");

      expect(result).toBe("https://example.com/app/settings");
    });
  });
});
