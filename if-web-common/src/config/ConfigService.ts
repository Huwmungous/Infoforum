// src/config/ConfigService.ts
import { LoggerService } from '../logger';
import { EnvironmentConfig } from './EnvironmentConfig';

export type AppType = 'user' | 'service';

export interface BootstrapConfig {
  clientId: string;
  openIdConfig: string;
  loggerService: string;
  logLevel: string;
}

export interface ConfigServiceInitParams {
  configServiceUrl: string;
  realm: string;
  client: string;
  appType: AppType;
  appName: string;
  environment: string;
}

/**
 * ConfigService stores application configuration.
 * It is agnostic to environment variables - values are passed in during initialization.
 */
export class ConfigService {
  private static _clientId: string | null = null;
  private static _openIdConfig: string | null = null;
  private static _loggerService: string | null = null;
  private static _logLevel: string = '';
  private static _realm: string | null = null;
  private static _appName: string | null = null;
  private static _environment: string | null = null;
  private static _initialized: boolean = false;
  private static _initializationPromise: Promise<void> | null = null;
  private static _appType: AppType = 'user';
  private static _logger: ReturnType<typeof LoggerService.create> | null = null;

  // Lazy-initialize the logger to avoid circular dependency with LoggerService
  private static get logger(): ReturnType<typeof LoggerService.create> {
    if (!this._logger) {
      this._logger = LoggerService.create('ConfigService');
    }
    return this._logger;
  }

  // --------------------------
  // Config properties
  // --------------------------
  public static get ClientId(): string {
    if (!this._clientId) {
      throw new Error('ConfigService not initialized. Call initialize() first.');
    }
    return this._clientId;
  }

  public static get OpenIdConfig(): string {
    if (!this._openIdConfig) {
      throw new Error('ConfigService not initialized. Call initialize() first.');
    }
    return this._openIdConfig;
  }

  public static get LoggerService(): string | null {
    return this._loggerService;
  }

  public static get LogLevel(): string {
    // API logLevel overrides EnvironmentConfig, fallback to EnvironmentConfig
    return this._logLevel || EnvironmentConfig.get('LOG_LEVEL');
  }

  public static get Realm(): string | null {
    return this._realm;
  }

  public static get AppName(): string | null {
    return this._appName;
  }

  public static get Environment(): string | null {
    return this._environment;
  }

  public static get isInitialized(): boolean {
    return this._initialized;
  }

  public static get appType(): AppType {
    return this._appType;
  }

  // --------------------------
  // Initialize by fetching bootstrap config from API
  // --------------------------
  public static async initialize(params: ConfigServiceInitParams): Promise<void> {
    if (this._initialized) return;
    if (this._initializationPromise) return this._initializationPromise;

    this._appType = params.appType;
    this._initializationPromise = this.performInitialization(params);

    try {
      await this._initializationPromise;
    } finally {
      this._initializationPromise = null;
    }
  }

  private static async performInitialization(params: ConfigServiceInitParams): Promise<void> {
    try {
      const url = `${params.configServiceUrl}/Config?cfg=bootstrap&type=${params.appType}&realm=${params.realm}&client=${params.client}`;
      this.logger.debug(`Fetching bootstrap configuration: ${url}`);

      console.log('ConfigService fetch URL:', url);

      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(`Failed to fetch bootstrap configuration: ${response.status} ${response.statusText}`);
      }

      const bootstrapConfig: BootstrapConfig = await response.json();

      if (!bootstrapConfig.clientId || !bootstrapConfig.openIdConfig) {
        throw new Error('Invalid bootstrap configuration: missing clientId or openIdConfig');
      }

      this._clientId = bootstrapConfig.clientId;
      this._openIdConfig = `${bootstrapConfig.openIdConfig}/${params.realm}`;
      this._loggerService = bootstrapConfig.loggerService || null;
      this._logLevel = bootstrapConfig.logLevel || '';
      this._realm = params.realm;
      this._appName = params.appName;
      this._environment = params.environment;

      this.logger.info('Bootstrap configuration loaded successfully');
      this.logger.debug(`ClientId: ${this._clientId}`);
      this.logger.debug(`Authority: ${this._openIdConfig}`);
      if (this._loggerService) {
        this.logger.debug(`Logger Service URL: ${this._loggerService}`);
      }
      this.logger.debug(`Log Level: ${this._logLevel}`);

      this._initialized = true;
    } catch (error) {
      this.logger.error('Failed to initialize ConfigService', error as Error);
      throw error;
    }
  }

  // --------------------------
  // Initialize with static config (bypasses API)
  // --------------------------
  public static initializeWithStaticConfig(config: {
    clientId: string;
    openIdConfig: string;
    loggerService?: string | null;
    logLevel?: string;
    realm?: string;
    appName?: string;
    environment?: string;
    appType?: AppType;
  }): void {
    if (this._initialized) return;

    this._clientId = config.clientId;
    this._openIdConfig = config.openIdConfig;
    this._loggerService = config.loggerService || null;
    this._logLevel = config.logLevel || this._logLevel; // Keep existing value if not provided
    this._realm = config.realm || null;
    this._appName = config.appName || null;
    this._environment = config.environment || null;
    this._appType = config.appType || 'user';

    this.logger.info('ConfigService initialized with static config');
    this.logger.debug(`ClientId: ${this._clientId}`);
    this.logger.debug(`Authority: ${this._openIdConfig}`);

    this._initialized = true;
  }

  // --------------------------
  // Reset service (for testing)
  // --------------------------
  public static reset(): void {
    this._clientId = null;
    this._openIdConfig = null;
    this._loggerService = null;
    this._logLevel = '';
    this._realm = null;
    this._appName = null;
    this._environment = null;
    this._initialized = false;
    this._initializationPromise = null;
    this._appType = 'user';
    this._logger = null;
  }
}
