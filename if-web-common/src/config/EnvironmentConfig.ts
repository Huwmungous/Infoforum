/**
 * EnvironmentConfig reads environment variables from import.meta.env (Vite).
 * All config values must be provided via VITE_* environment variables.
 */
export class EnvironmentConfig {
  private static readonly VALID_ENVIRONMENTS = ['DBG', 'DEV', 'SIT', 'UAT', 'PRD'];

  /**
   * Get an environment variable by key
   */
  public static get(key: string): string {
    // Vite env vars
    try {
      // @ts-ignore
      const viteKey = `VITE_${key}`;
      // @ts-ignore
      if (typeof import.meta !== 'undefined' && import.meta.env && import.meta.env[viteKey]) {
        // @ts-ignore
        return import.meta.env[viteKey];
      }

      // Special case: IF_ENVIRONMENT can be derived from Vite's MODE (--mode flag)
      // Only return MODE if it's a valid environment value
      // @ts-ignore
      if (key === 'IF_ENVIRONMENT' && typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.MODE) {
        // @ts-ignore
        const modeValue = import.meta.env.MODE.toUpperCase();
        if (this.VALID_ENVIRONMENTS.includes(modeValue)) {
          return modeValue;
        }
      }
    } catch {
      // Not in Vite environment
    }

    return '';
  }

  /**
   * Validate required env vars.
   */
  public static validate(useStaticConfig: boolean): string[] {
    const errors: string[] = [];

    const requiredForAll = ['IF_REALM', 'IF_CLIENT', 'IF_APP_NAME', 'IF_ENVIRONMENT', 'LOG_LEVEL'];
    const requiredForDynamic = ['IF_CONFIG_SERVICE_URL'];
    const requiredForStatic = ['AUTH_CLIENT_ID', 'AUTH_AUTHORITY', 'LOG_SERVICE_URL'];

    for (const key of requiredForAll) {
      if (!this.get(key)) {
        errors.push(`Missing: ${key}`);
      }
    }

    // Validate IF_ENVIRONMENT is one of the allowed values
    const environment = this.get('IF_ENVIRONMENT');
    if (environment && !this.VALID_ENVIRONMENTS.includes(environment.toUpperCase())) {
      errors.push(`Invalid IF_ENVIRONMENT: '${environment}'. Must be one of: ${this.VALID_ENVIRONMENTS.join(', ')}`);
    }

    if (useStaticConfig) {
      for (const key of requiredForStatic) {
        if (!this.get(key)) {
          errors.push(`Missing: ${key}`);
        }
      }
    } else {
      for (const key of requiredForDynamic) {
        if (!this.get(key)) {
          errors.push(`Missing: ${key}`);
        }
      }
    }

    return errors;
  }

  /**
   * Log validation errors
   */
  public static logValidationErrors(errors: string[]): void {
    console.error(`[EnvironmentConfig] Validation failed:\n` + errors.map(e => `  - ${e}`).join('\n'));
  }

  /**
   * Log all configuration values to console
   */
  public static logConfiguration(useStaticConfig: boolean): void {
    const commonKeys = ['IF_REALM', 'IF_CLIENT', 'IF_APP_NAME', 'IF_ENVIRONMENT', 'LOG_LEVEL'];
    const dynamicKeys = ['IF_CONFIG_SERVICE_URL'];
    const staticKeys = ['AUTH_CLIENT_ID', 'AUTH_AUTHORITY', 'LOG_SERVICE_URL'];

    const keysToLog = useStaticConfig
      ? [...commonKeys, ...staticKeys]
      : [...commonKeys, ...dynamicKeys];

    const configValues = keysToLog.map(key => `  ${key}: ${this.get(key) || '(not set)'}`).join('\n');

    console.log(`[EnvironmentConfig] Configuration:\n${configValues}`);
  }
}
