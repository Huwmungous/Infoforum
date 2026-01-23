import { ConfigService } from "../config";

export type LogLevel = 'Trace' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';

export interface IfLoggerConfiguration {
  minimumLogLevel: LogLevel;
  loggerService: string;
}

export interface IfLogEntry {
  realm: string;
  client: string;
  timestamp: string;
  level: string;
  category: string;
  eventId?: number;
  eventName?: string;
  message: string;
  exception?: string;
  stackTrace?: string;
  application: string;
  environment: string;
  host: string;
  pathname: string;
}

const LOG_LEVELS: Record<LogLevel, number> = {
  'Trace': 0,
  'Debug': 1,
  'Information': 2,
  'Warning': 3,
  'Error': 4,
  'Critical': 5
};

export class IfLogger {
  private static logQueue: IfLogEntry[] = [];
  private static isProcessing = false;

  constructor(
    private categoryName: string,
    private config: Required<IfLoggerConfiguration>
  ) {}

  public isEnabled(logLevel: LogLevel): boolean {
    return LOG_LEVELS[logLevel] >= LOG_LEVELS[this.config.minimumLogLevel];
  }

  public log(
    logLevel: LogLevel,
    message: string,
    exception?: Error,
    eventId?: number,
    eventName?: string
  ): void {
    if (!this.isEnabled(logLevel)) return;

    // Safely access ConfigService properties - they may not be available during initialization
    const safeGetConfig = () => {
      try {
        return {
          realm: ConfigService.Realm || '',
          clientId: ConfigService.isInitialized ? ConfigService.ClientId : '',
          appName: ConfigService.AppName || '',
          environment: ConfigService.Environment || ''
        };
      } catch {
        return { realm: '', clientId: '', appName: '', environment: '' };
      }
    };
    const config = safeGetConfig();

    const logEntry: IfLogEntry = {
      realm: config.realm,
      client: config.clientId,
      timestamp: new Date().toISOString(),
      level: logLevel,
      category: this.categoryName,
      eventId,
      eventName,
      message,
      exception: exception?.message,
      stackTrace: exception?.stack,
      application: config.appName,
      environment: config.environment,
      host: typeof window !== 'undefined' ? window.location.host : '',
      pathname: typeof window !== 'undefined' ? window.location.pathname : ''
    };

    // Log locally
    this.logLocally(logLevel, message, exception);

    // Queue for remote logging if loggerService URL is configured
    if (this.config.loggerService) {
      IfLogger.logQueue.push(logEntry);
      this.processLogQueue();
    }
  }

  public trace(message: string, exception?: Error): void {
    this.log('Trace', message, exception);
  }

  public debug(message: string, exception?: Error): void {
    this.log('Debug', message, exception);
  }

  public info(message: string, exception?: Error): void {
    this.log('Information', message, exception);
  }

  public warn(message: string, exception?: Error): void {
    this.log('Warning', message, exception);
  }

  public error(message: string, exception?: Error): void {
    this.log('Error', message, exception);
  }

  public critical(message: string, exception?: Error): void {
    this.log('Critical', message, exception);
  }

  private logLocally(logLevel: LogLevel, message: string, exception?: Error): void {
    const timestamp = new Date().toISOString();
    const formattedMessage = `[${timestamp}] [${logLevel}] ${this.categoryName}: ${message}`;

    switch (logLevel) {
      case 'Trace':
      case 'Debug':
        console.debug(formattedMessage);
        break;
      case 'Information':
        console.log(formattedMessage);
        break;
      case 'Warning':
        console.warn(formattedMessage);
        break;
      case 'Error':
      case 'Critical':
        console.error(formattedMessage);
        break;
    }

    if (exception) {
      console.error(exception);
    }
  }

  private async processLogQueue(): Promise<void> {
    if (IfLogger.isProcessing || IfLogger.logQueue.length === 0) {
      return;
    }

    IfLogger.isProcessing = true;

    try {
      while (IfLogger.logQueue.length > 0) {
        try {
          const logEntry = IfLogger.logQueue.shift();
          if (logEntry) 
            await this.sendToRemoteService(logEntry); 
        } catch (error) {
          console.error('Exception processing log entry:', error); // but must swallow the exception
        }
      }
    } finally {
      IfLogger.isProcessing = false;
    }
  }

private async sendToRemoteService(logEntry: IfLogEntry): Promise<void> {
  try {
    const response = await fetch(`${this.config.loggerService}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        realm: logEntry.realm,
        client: logEntry.client,
        logData: logEntry,
        environment: logEntry.environment,
        application: logEntry.application,
        logLevel: logEntry.level
      })
    });

      if (!response.ok) {
        console.error(`Remote logging failed: ${response.status}`);
      }
    } catch (error) {
      // Log error but don't crash the app
      console.error('Exception sending log to remote service:', error);
    }
  }
}