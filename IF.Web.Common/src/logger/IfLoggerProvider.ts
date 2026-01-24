import { IfLogger, IfLoggerConfiguration, LogLevel } from './IfLogger';

export class IfLoggerProvider {
  private static config: Required<IfLoggerConfiguration> = {
    minimumLogLevel: 'Information',
    loggerService: ''
  };

  private static loggers: Map<string, IfLogger> = new Map();

  public static configure(config: Partial<IfLoggerConfiguration>): void {
    // Use Object.assign to mutate existing config object
    // This ensures existing loggers see the updated config
    Object.assign(IfLoggerProvider.config, config);
  }

  public static createLogger(categoryName: string): IfLogger {
    let logger = IfLoggerProvider.loggers.get(categoryName);
    
    if (!logger) {
      logger = new IfLogger(categoryName, IfLoggerProvider.config);
      IfLoggerProvider.loggers.set(categoryName, logger);
    }

    return logger;
  }

  public static getLogger(categoryName: string): IfLogger {
    return IfLoggerProvider.createLogger(categoryName);
  }

  public static clearLoggers(): void {
    IfLoggerProvider.loggers.clear();
  }
}
