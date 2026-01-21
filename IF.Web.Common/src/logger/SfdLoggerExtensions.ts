import { SfdLoggerProvider } from './SfdLoggerProvider';
import { SfdLoggerConfiguration, LogLevel } from './SfdLogger';

export class SfdLoggerExtensions {
  /**
   * Configure the global logger with the specified configuration
   */
  public static addSfdLogger(config?: Partial<SfdLoggerConfiguration>): void {
    if (config) {
      SfdLoggerProvider.configure(config);
    }
  }

  /**
   * Configure the logger with URL and minimum log level
   */
  public static addSfdLoggerWithUrl(
    loggerServiceUrl: string,
    minimumLogLevel: LogLevel = 'Information'
  ): void {
    SfdLoggerProvider.configure({
      loggerService: loggerServiceUrl,
      minimumLogLevel
    });
  }

  /**
   * Quick configuration method
   */
  public static configureSfdLogger(
    loggerServiceUrl: string,
    minimumLogLevel: LogLevel = 'Information'
  ): void {
    SfdLoggerProvider.configure({
      loggerService: loggerServiceUrl,
      minimumLogLevel
    });
  }
}
