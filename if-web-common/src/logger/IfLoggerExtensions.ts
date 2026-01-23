import { IfLoggerProvider } from './IfLoggerProvider';
import { IfLoggerConfiguration, LogLevel } from './IfLogger';

export class IfLoggerExtensions {
  /**
   * Configure the global logger with the specified configuration
   */
  public static addIfLogger(config?: Partial<IfLoggerConfiguration>): void {
    if (config) {
      IfLoggerProvider.configure(config);
    }
  }

  /**
   * Configure the logger with URL and minimum log level
   */
  public static addIfLoggerWithUrl(
    loggerServiceUrl: string,
    minimumLogLevel: LogLevel = 'Information'
  ): void {
    IfLoggerProvider.configure({
      loggerService: loggerServiceUrl,
      minimumLogLevel
    });
  }

  /**
   * Quick configuration method
   */
  public static configureIfLogger(
    loggerServiceUrl: string,
    minimumLogLevel: LogLevel = 'Information'
  ): void {
    IfLoggerProvider.configure({
      loggerService: loggerServiceUrl,
      minimumLogLevel
    });
  }
}
