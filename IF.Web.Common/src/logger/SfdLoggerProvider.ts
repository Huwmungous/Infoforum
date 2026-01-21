import { SfdLogger, SfdLoggerConfiguration, LogLevel } from './SfdLogger';

export class SfdLoggerProvider {
  private static config: Required<SfdLoggerConfiguration> = {
    minimumLogLevel: 'Information',
    loggerService: ''
  };

  private static loggers: Map<string, SfdLogger> = new Map();

  public static configure(config: Partial<SfdLoggerConfiguration>): void {
    // Use Object.assign to mutate existing config object
    // This ensures existing loggers see the updated config
    Object.assign(SfdLoggerProvider.config, config);
  }

  public static createLogger(categoryName: string): SfdLogger {
    let logger = SfdLoggerProvider.loggers.get(categoryName);
    
    if (!logger) {
      logger = new SfdLogger(categoryName, SfdLoggerProvider.config);
      SfdLoggerProvider.loggers.set(categoryName, logger);
    }

    return logger;
  }

  public static getLogger(categoryName: string): SfdLogger {
    return SfdLoggerProvider.createLogger(categoryName);
  }

  public static clearLoggers(): void {
    SfdLoggerProvider.loggers.clear();
  }
}
