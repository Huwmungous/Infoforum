import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { EnvironmentConfig } from './EnvironmentConfig';

describe('EnvironmentConfig', () => {
  // Store original import.meta.env
  let originalImportMetaEnv: Record<string, string>;

  beforeEach(() => {
    // Store and clear import.meta.env
    originalImportMetaEnv = { ...import.meta.env };
    Object.keys(import.meta.env).forEach(key => {
      delete (import.meta.env as any)[key];
    });
  });

  afterEach(() => {
    // Restore import.meta.env
    Object.keys(import.meta.env).forEach(key => {
      delete (import.meta.env as any)[key];
    });
    Object.assign(import.meta.env, originalImportMetaEnv);

    vi.restoreAllMocks();
  });

  // ===========================================
  // VITE ENVIRONMENT (Development)
  // ===========================================
  describe('Vite environment (import.meta.env)', () => {

    describe('get()', () => {
      it('should read SFD_REALM from VITE_SFD_REALM', () => {
        (import.meta.env as any).VITE_SFD_REALM = 'vite-realm';
        expect(EnvironmentConfig.get('SFD_REALM')).toBe('vite-realm');
      });

      it('should read SFD_CLIENT from VITE_SFD_CLIENT', () => {
        (import.meta.env as any).VITE_SFD_CLIENT = 'vite-client';
        expect(EnvironmentConfig.get('SFD_CLIENT')).toBe('vite-client');
      });

      it('should read SFD_APP_NAME from VITE_SFD_APP_NAME', () => {
        (import.meta.env as any).VITE_SFD_APP_NAME = 'ViteTestApp';
        expect(EnvironmentConfig.get('SFD_APP_NAME')).toBe('ViteTestApp');
      });

      it('should read SFD_ENVIRONMENT from VITE_SFD_ENVIRONMENT', () => {
        (import.meta.env as any).VITE_SFD_ENVIRONMENT = 'development';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('development');
      });

      it('should read SFD_CONFIG_SERVICE_URL from VITE_SFD_CONFIG_SERVICE_URL', () => {
        (import.meta.env as any).VITE_SFD_CONFIG_SERVICE_URL = 'https://vite-config.example.com';
        expect(EnvironmentConfig.get('SFD_CONFIG_SERVICE_URL')).toBe('https://vite-config.example.com');
      });

      it('should derive SFD_ENVIRONMENT from import.meta.env.MODE when VITE_SFD_ENVIRONMENT is not set', () => {
        // MODE is automatically set by Vite from --mode flag
        (import.meta.env as any).MODE = 'dev';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('DEV');
      });

      it('should convert MODE to uppercase for SFD_ENVIRONMENT', () => {
        (import.meta.env as any).MODE = 'sit';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('SIT');
      });

      it('should accept all valid MODE values (dbg, dev, sit, uat, prd)', () => {
        (import.meta.env as any).MODE = 'uat';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('UAT');

        (import.meta.env as any).MODE = 'prd';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('PRD');
      });

      it('should return empty string for invalid MODE values', () => {
        (import.meta.env as any).MODE = 'invalid';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('');
      });

      it('should return empty string for non-standard MODE values like "production"', () => {
        (import.meta.env as any).MODE = 'production';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('');
      });

      it('should prefer VITE_SFD_ENVIRONMENT over MODE when both are set', () => {
        (import.meta.env as any).VITE_SFD_ENVIRONMENT = 'UAT';
        (import.meta.env as any).MODE = 'dev';
        expect(EnvironmentConfig.get('SFD_ENVIRONMENT')).toBe('UAT');
      });
    });

    describe('validate() - individual missing variables', () => {
      // Helper to set all required Vite vars except the one being tested
      const setAllViteVarsExcept = (excludeKey: string) => {
        const allVars: Record<string, string> = {
          VITE_SFD_REALM: 'test-realm',
          VITE_SFD_CLIENT: 'test-client',
          VITE_SFD_APP_NAME: 'TestApp',
          VITE_SFD_ENVIRONMENT: 'DEV',
          VITE_SFD_CONFIG_SERVICE_URL: 'https://config.example.com',
          VITE_LOG_LEVEL: 'Information',
        };
        Object.entries(allVars).forEach(([key, value]) => {
          if (key !== excludeKey) {
            (import.meta.env as any)[key] = value;
          }
        });
      };

      it('should return error when SFD_REALM is missing', () => {
        setAllViteVarsExcept('VITE_SFD_REALM');

        const errors = EnvironmentConfig.validate(false);

        expect(errors).toContain('Missing: SFD_REALM');
        expect(errors).toHaveLength(1);
      });

      it('should return error when SFD_CLIENT is missing', () => {
        setAllViteVarsExcept('VITE_SFD_CLIENT');

        const errors = EnvironmentConfig.validate(false);

        expect(errors).toContain('Missing: SFD_CLIENT');
        expect(errors).toHaveLength(1);
      });

      it('should return error when SFD_APP_NAME is missing', () => {
        setAllViteVarsExcept('VITE_SFD_APP_NAME');

        const errors = EnvironmentConfig.validate(false);

        expect(errors).toContain('Missing: SFD_APP_NAME');
        expect(errors).toHaveLength(1);
      });

      it('should return error when SFD_ENVIRONMENT is missing', () => {
        setAllViteVarsExcept('VITE_SFD_ENVIRONMENT');

        const errors = EnvironmentConfig.validate(false);

        expect(errors).toContain('Missing: SFD_ENVIRONMENT');
        expect(errors).toHaveLength(1);
      });

      it('should return error when SFD_CONFIG_SERVICE_URL is missing (dynamic config)', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');

        const errors = EnvironmentConfig.validate(false); // useStaticConfig = false

        expect(errors).toContain('Missing: SFD_CONFIG_SERVICE_URL');
        expect(errors).toHaveLength(1);
      });

      it('should return error when LOG_LEVEL is missing', () => {
        setAllViteVarsExcept('VITE_LOG_LEVEL');

        const errors = EnvironmentConfig.validate(false);

        expect(errors).toContain('Missing: LOG_LEVEL');
        expect(errors).toHaveLength(1);
      });

      it('should NOT return error when SFD_CONFIG_SERVICE_URL is missing (static config)', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');
        // Add all static config vars
        (import.meta.env as any).VITE_AUTH_CLIENT_ID = 'test-client-id';
        (import.meta.env as any).VITE_AUTH_AUTHORITY = 'https://auth.example.com';
        (import.meta.env as any).VITE_LOG_SERVICE_URL = 'https://logger.example.com';

        const errors = EnvironmentConfig.validate(true); // useStaticConfig = true

        expect(errors).not.toContain('Missing: SFD_CONFIG_SERVICE_URL');
        expect(errors).toHaveLength(0);
      });

      it('should return error when AUTH_CLIENT_ID is missing (static config)', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');
        // Add all static config vars except AUTH_CLIENT_ID
        (import.meta.env as any).VITE_AUTH_AUTHORITY = 'https://auth.example.com';
        (import.meta.env as any).VITE_LOG_SERVICE_URL = 'https://logger.example.com';

        const errors = EnvironmentConfig.validate(true);

        expect(errors).toContain('Missing: AUTH_CLIENT_ID');
        expect(errors).toHaveLength(1);
      });

      it('should return error when AUTH_AUTHORITY is missing (static config)', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');
        // Add all static config vars except AUTH_AUTHORITY
        (import.meta.env as any).VITE_AUTH_CLIENT_ID = 'test-client-id';
        (import.meta.env as any).VITE_LOG_SERVICE_URL = 'https://logger.example.com';

        const errors = EnvironmentConfig.validate(true);

        expect(errors).toContain('Missing: AUTH_AUTHORITY');
        expect(errors).toHaveLength(1);
      });

      it('should return error when LOG_SERVICE_URL is missing (static config)', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');
        // Add all static config vars except LOG_SERVICE_URL
        (import.meta.env as any).VITE_AUTH_CLIENT_ID = 'test-client-id';
        (import.meta.env as any).VITE_AUTH_AUTHORITY = 'https://auth.example.com';

        const errors = EnvironmentConfig.validate(true);

        expect(errors).toContain('Missing: LOG_SERVICE_URL');
        expect(errors).toHaveLength(1);
      });

      it('should return all errors when all static config vars missing', () => {
        setAllViteVarsExcept('VITE_SFD_CONFIG_SERVICE_URL');
        // Don't add any static config vars

        const errors = EnvironmentConfig.validate(true);

        expect(errors).toContain('Missing: AUTH_CLIENT_ID');
        expect(errors).toContain('Missing: AUTH_AUTHORITY');
        expect(errors).toContain('Missing: LOG_SERVICE_URL');
        expect(errors).toHaveLength(3);
      });
    });

    describe('validate() - multiple missing variables', () => {
      it('should return all errors when all vars are missing (dynamic config)', () => {
        // No vars set
        const errors = EnvironmentConfig.validate(false);

        // 5 requiredForAll + 1 requiredForDynamic = 6 total
        expect(errors).toContain('Missing: SFD_REALM');
        expect(errors).toContain('Missing: SFD_CLIENT');
        expect(errors).toContain('Missing: SFD_APP_NAME');
        expect(errors).toContain('Missing: SFD_ENVIRONMENT');
        expect(errors).toContain('Missing: LOG_LEVEL');
        expect(errors).toContain('Missing: SFD_CONFIG_SERVICE_URL');
        expect(errors).toHaveLength(6);
      });

      it('should return 8 errors when all vars missing but using static config', () => {
        const errors = EnvironmentConfig.validate(true);

        // 5 requiredForAll + 3 requiredForStatic = 8 total
        expect(errors).toHaveLength(8);
        expect(errors).not.toContain('Missing: SFD_CONFIG_SERVICE_URL');
        expect(errors).toContain('Missing: SFD_REALM');
        expect(errors).toContain('Missing: SFD_CLIENT');
        expect(errors).toContain('Missing: SFD_APP_NAME');
        expect(errors).toContain('Missing: SFD_ENVIRONMENT');
        expect(errors).toContain('Missing: LOG_LEVEL');
        expect(errors).toContain('Missing: AUTH_CLIENT_ID');
        expect(errors).toContain('Missing: AUTH_AUTHORITY');
        expect(errors).toContain('Missing: LOG_SERVICE_URL');
      });
    });

    describe('validate() - SFD_ENVIRONMENT value validation', () => {
      const setAllVarsWithEnvironment = (envValue: string) => {
        (import.meta.env as any).VITE_SFD_REALM = 'test-realm';
        (import.meta.env as any).VITE_SFD_CLIENT = 'test-client';
        (import.meta.env as any).VITE_SFD_APP_NAME = 'TestApp';
        (import.meta.env as any).VITE_SFD_ENVIRONMENT = envValue;
        (import.meta.env as any).VITE_SFD_CONFIG_SERVICE_URL = 'https://config.example.com';
        (import.meta.env as any).VITE_LOG_LEVEL = 'Information';
      };

      it('should accept DEV as valid environment', () => {
        setAllVarsWithEnvironment('DEV');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should accept SIT as valid environment', () => {
        setAllVarsWithEnvironment('SIT');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should accept UAT as valid environment', () => {
        setAllVarsWithEnvironment('UAT');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should accept PRD as valid environment', () => {
        setAllVarsWithEnvironment('PRD');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should accept lowercase environment values (case-insensitive)', () => {
        setAllVarsWithEnvironment('dev');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should accept mixed-case environment values (case-insensitive)', () => {
        setAllVarsWithEnvironment('Dev');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toHaveLength(0);
      });

      it('should return error for invalid environment value', () => {
        setAllVarsWithEnvironment('INVALID');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toContain("Invalid SFD_ENVIRONMENT: 'INVALID'. Must be one of: DEV, SIT, UAT, PRD");
        expect(errors).toHaveLength(1);
      });

      it('should return error for test environment', () => {
        setAllVarsWithEnvironment('test');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toContain("Invalid SFD_ENVIRONMENT: 'test'. Must be one of: DEV, SIT, UAT, PRD");
        expect(errors).toHaveLength(1);
      });

      it('should return error for production (must be PRD)', () => {
        setAllVarsWithEnvironment('production');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toContain("Invalid SFD_ENVIRONMENT: 'production'. Must be one of: DEV, SIT, UAT, PRD");
        expect(errors).toHaveLength(1);
      });

      it('should return error for development (must be DEV)', () => {
        setAllVarsWithEnvironment('development');
        const errors = EnvironmentConfig.validate(false);
        expect(errors).toContain("Invalid SFD_ENVIRONMENT: 'development'. Must be one of: DEV, SIT, UAT, PRD");
        expect(errors).toHaveLength(1);
      });
    });
  });


  // ===========================================
  // CONSOLE ERROR LOGGING
  // ===========================================
  describe('logValidationErrors()', () => {
    it('should log single error to console.error', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      EnvironmentConfig.logValidationErrors(['Missing: SFD_REALM']);

      expect(consoleSpy).toHaveBeenCalledTimes(1);
      expect(consoleSpy).toHaveBeenCalledWith(
        '[EnvironmentConfig] Validation failed:\n  - Missing: SFD_REALM'
      );
    });

    it('should log multiple errors to console.error', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      EnvironmentConfig.logValidationErrors([
        'Missing: SFD_REALM',
        'Missing: SFD_CLIENT',
        'Missing: SFD_APP_NAME'
      ]);

      expect(consoleSpy).toHaveBeenCalledTimes(1);
      expect(consoleSpy).toHaveBeenCalledWith(
        '[EnvironmentConfig] Validation failed:\n  - Missing: SFD_REALM\n  - Missing: SFD_CLIENT\n  - Missing: SFD_APP_NAME'
      );
    });
  });

  // ===========================================
  // EDGE CASES
  // ===========================================
  describe('edge cases', () => {
    it('should return empty string for unknown key', () => {
      expect(EnvironmentConfig.get('UNKNOWN_KEY')).toBe('');
    });

    it('should return empty string when no config sources available', () => {
      // No import.meta.env values set
      expect(EnvironmentConfig.get('SFD_REALM')).toBe('');
    });
  });

  // ===========================================
  // LOG CONFIGURATION
  // ===========================================
  describe('logConfiguration()', () => {
    it('should log dynamic config keys when useStaticConfig is false', () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => {});

      (import.meta.env as any).VITE_SFD_REALM = 'test-realm';
      (import.meta.env as any).VITE_SFD_CLIENT = 'test-client';
      (import.meta.env as any).VITE_SFD_APP_NAME = 'TestApp';
      (import.meta.env as any).VITE_SFD_ENVIRONMENT = 'DEV';
      (import.meta.env as any).VITE_LOG_LEVEL = 'Information';
      (import.meta.env as any).VITE_SFD_CONFIG_SERVICE_URL = 'https://config.example.com';

      EnvironmentConfig.logConfiguration(false);

      expect(consoleSpy).toHaveBeenCalledTimes(1);
      const logOutput = consoleSpy.mock.calls[0][0];
      expect(logOutput).toContain('[EnvironmentConfig] Configuration:');
      expect(logOutput).toContain('SFD_REALM: test-realm');
      expect(logOutput).toContain('SFD_CLIENT: test-client');
      expect(logOutput).toContain('SFD_APP_NAME: TestApp');
      expect(logOutput).toContain('SFD_ENVIRONMENT: DEV');
      expect(logOutput).toContain('LOG_LEVEL: Information');
      expect(logOutput).toContain('SFD_CONFIG_SERVICE_URL: https://config.example.com');
      // Should NOT contain static config keys
      expect(logOutput).not.toContain('AUTH_CLIENT_ID');
      expect(logOutput).not.toContain('AUTH_AUTHORITY');
      expect(logOutput).not.toContain('LOG_SERVICE_URL');
    });

    it('should log static config keys when useStaticConfig is true', () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => {});

      (import.meta.env as any).VITE_SFD_REALM = 'test-realm';
      (import.meta.env as any).VITE_SFD_CLIENT = 'test-client';
      (import.meta.env as any).VITE_SFD_APP_NAME = 'TestApp';
      (import.meta.env as any).VITE_SFD_ENVIRONMENT = 'DEV';
      (import.meta.env as any).VITE_LOG_LEVEL = 'Information';
      (import.meta.env as any).VITE_AUTH_CLIENT_ID = 'my-client-id';
      (import.meta.env as any).VITE_AUTH_AUTHORITY = 'https://auth.example.com';
      (import.meta.env as any).VITE_LOG_SERVICE_URL = 'https://logger.example.com';

      EnvironmentConfig.logConfiguration(true);

      expect(consoleSpy).toHaveBeenCalledTimes(1);
      const logOutput = consoleSpy.mock.calls[0][0];
      expect(logOutput).toContain('[EnvironmentConfig] Configuration:');
      expect(logOutput).toContain('SFD_REALM: test-realm');
      expect(logOutput).toContain('AUTH_CLIENT_ID: my-client-id');
      expect(logOutput).toContain('AUTH_AUTHORITY: https://auth.example.com');
      expect(logOutput).toContain('LOG_SERVICE_URL: https://logger.example.com');
      // Should NOT contain dynamic config key
      expect(logOutput).not.toContain('SFD_CONFIG_SERVICE_URL');
    });

    it('should show (not set) for missing values', () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation(() => {});

      // Only set some values
      (import.meta.env as any).VITE_SFD_REALM = 'test-realm';

      EnvironmentConfig.logConfiguration(false);

      const logOutput = consoleSpy.mock.calls[0][0];
      expect(logOutput).toContain('SFD_REALM: test-realm');
      expect(logOutput).toContain('SFD_CLIENT: (not set)');
      expect(logOutput).toContain('SFD_APP_NAME: (not set)');
    });
  });
});
