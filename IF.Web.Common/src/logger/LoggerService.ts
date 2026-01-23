import { IfLoggerProvider } from './IfLoggerProvider';
import { IfLogger, IfLoggerConfiguration, LogLevel } from './IfLogger';
import { ConfigService } from '../config';

export class LoggerService {
  private logger: IfLogger;
  private static _configured: boolean = false;

  private constructor(category: string) {
    // Auto-configure on first use if not already configured
    if (!LoggerService._configured) {
      LoggerService.autoConfigureFromConfigService();
    }
    
    this.logger = IfLoggerProvider.createLogger(category);
  }

/**
 * Validate and convert string or number to LogLevel, with fallback
 */
private static getValidLogLevel(level: string | number): LogLevel {
  const validLevels: LogLevel[] = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];
  
  // Handle numeric levels (0-5)
  if (typeof level === 'number' && level >= 0 && level < validLevels.length) {
    return validLevels[level];
  }
  
  // Handle string levels
  if (typeof level === 'string') {
    // Try direct match first
    if (validLevels.includes(level as LogLevel)) {
      return level as LogLevel;
    }
    
    // Try parsing as number string ("1" -> 1)
    const numLevel = parseInt(level, 10);
    if (!isNaN(numLevel) && numLevel >= 0 && numLevel < validLevels.length) {
      return validLevels[numLevel];
    }
  }
  
  console.warn(`Invalid log level '${level}', falling back to 'Information'`);
  return 'Information';
}

  /**
   * Automatically configure logger using ConfigService
   * This is called automatically on first logger creation
   */
  private static autoConfigureFromConfigService(): void {
    if (LoggerService._configured) {
      return;
    }

    try {
      // Check if ConfigService is initialized
      if (ConfigService.isInitialized) {
        const loggerServiceUrl = ConfigService.LoggerService;
        const logLevel = LoggerService.getValidLogLevel(ConfigService.LogLevel);
        
        console.log(`Auto-configuring logger with URL from ConfigService: ${loggerServiceUrl || '(none)'}, LogLevel: ${logLevel}`);
        IfLoggerProvider.configure({
          loggerService: loggerServiceUrl || '',
          minimumLogLevel: logLevel
        });
      } else {
        // This is expected during initial bootstrap - ConfigService and LoggerService
        // have a circular dependency that's resolved by using defaults initially
        // Don't warn about this as it's normal startup behaviour
        IfLoggerProvider.configure({
          loggerService: '',
          minimumLogLevel: 'Information'
        });
      }
      
      LoggerService._configured = true;
    } catch (error) {
      console.error('Failed to auto-configure logger from ConfigService:', error);
      // Fall back to default configuration
      IfLoggerProvider.configure({
        loggerService: '',
        minimumLogLevel: 'Information'
      });
      LoggerService._configured = true;
    }
  }

  /**
   * Configure the global logger manually
   * This overrides any auto-configuration from ConfigService
   */
  public static configure(config: Partial<IfLoggerConfiguration>): void {
    IfLoggerProvider.configure(config);
    LoggerService._configured = true;
  }

  /**
   * Configure logger using URL from ConfigService
   * Call this explicitly if you want to ensure ConfigService is used
   */
  public static async configureFromConfigService(): Promise<void> {
    // ConfigService must already be initialized by AppInitializer
    if (!ConfigService.isInitialized) {
      throw new Error('ConfigService must be initialized before configuring LoggerService');
    }

    const loggerServiceUrl = ConfigService.LoggerService;
    const logLevel = LoggerService.getValidLogLevel(ConfigService.LogLevel);

    console.log(`Configuring logger with URL from ConfigService: ${loggerServiceUrl || '(none)'}, LogLevel: ${logLevel}`);
    IfLoggerProvider.configure({
      loggerService: loggerServiceUrl || '',
      minimumLogLevel: logLevel
    });

    LoggerService._configured = true;
  }

  /**
   * Create a logger for a specific category
   */
  public static create(category: string): LoggerService {
    return new LoggerService(category);
  }

  /**
   * Get or create a logger for a specific category
   */
  public static getLogger(category: string): LoggerService {
    return new LoggerService(category);
  }

  /**
   * Reset configuration state (mainly for testing)
   */
  public static reset(): void {
    LoggerService._configured = false;
    IfLoggerProvider.clearLoggers();
  }

  // Convenience methods that delegate to the underlying IfLogger
  public trace(message: string, exception?: Error): void {
    this.logger.trace(message, exception);
  }

  public debug(message: string, exception?: Error): void {
    this.logger.debug(message, exception);
  }

  public info(message: string, exception?: Error): void {
    this.logger.info(message, exception);
  }

  public warn(message: string, exception?: Error): void {
    this.logger.warn(message, exception);
  }

  public error(message: string, exception?: Error): void {
    this.logger.error(message, exception);
  }

  public critical(message: string, exception?: Error): void {
    this.logger.critical(message, exception);
  }
}