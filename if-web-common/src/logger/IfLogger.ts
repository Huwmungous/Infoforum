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
  _retryCount?: number;  // Internal: track retry attempts
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
  private static retryTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private static initialDelayComplete = false;
  private static initialDelayTimeoutId: ReturnType<typeof setTimeout> | null = null;
  private static readonly MAX_RETRIES = 3;
  private static readonly RETRY_DELAY_MS = 2000;  // Wait 2 seconds before retry
  private static readonly INITIAL_DELAY_MS = 1500;  // Wait for auth to initialise

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

    // On first attempt, wait for auth to initialise
    if (!IfLogger.initialDelayComplete) {
      if (IfLogger.initialDelayTimeoutId === null) {
        IfLogger.initialDelayTimeoutId = setTimeout(() => {
          IfLogger.initialDelayComplete = true;
          IfLogger.initialDelayTimeoutId = null;
          this.processLogQueue();
        }, IfLogger.INITIAL_DELAY_MS);
      }
      return;
    }

    IfLogger.isProcessing = true;

    try {
      const entriesToRetry: IfLogEntry[] = [];

      while (IfLogger.logQueue.length > 0) {
        try {
          const logEntry = IfLogger.logQueue.shift();
          if (logEntry) {
            const result = await this.sendToRemoteService(logEntry);
            
            // If we got a 401 and haven't exceeded retries, queue for retry
            if (result === 'retry') {
              const retryCount = (logEntry._retryCount || 0) + 1;
              if (retryCount <= IfLogger.MAX_RETRIES) {
                logEntry._retryCount = retryCount;
                entriesToRetry.push(logEntry);
              } else {
                console.warn(`Dropping log entry after ${IfLogger.MAX_RETRIES} failed attempts (auth not ready)`);
              }
            }
          }
        } catch (error) {
          console.error('Exception processing log entry:', error);
        }
      }

      // Re-queue entries that need retry
      if (entriesToRetry.length > 0) {
        // Add back to front of queue
        IfLogger.logQueue.unshift(...entriesToRetry);
        
        // Schedule retry after delay
        this.scheduleRetry();
      }
    } finally {
      IfLogger.isProcessing = false;
    }
  }

  private scheduleRetry(): void {
    // Don't schedule if already scheduled
    if (IfLogger.retryTimeoutId !== null) {
      return;
    }

    IfLogger.retryTimeoutId = setTimeout(() => {
      IfLogger.retryTimeoutId = null;
      this.processLogQueue();
    }, IfLogger.RETRY_DELAY_MS);
  }

  /**
   * Get the effective logger service URL.
   * In development (localhost), use relative URL to leverage Vite proxy.
   */
  private getEffectiveLoggerUrl(): string {
    const configuredUrl = this.config.loggerService;
    
    // In dev mode (localhost), use relative URL to avoid CORS
    if (typeof window !== 'undefined' && 
        (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')) {
      // Extract just the path from the configured URL
      try {
        const url = new URL(configuredUrl);
        return url.pathname; // e.g., '/logger'
      } catch {
        // If it's already a relative URL, use as-is
        return configuredUrl;
      }
    }
    
    return configuredUrl;
  }

  private async sendToRemoteService(logEntry: IfLogEntry): Promise<'success' | 'retry' | 'error'> {
    try {
      // Create a clean copy without internal properties
      const { _retryCount, ...cleanEntry } = logEntry;

      const loggerUrl = this.getEffectiveLoggerUrl();
      const response = await fetch(loggerUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          realm: cleanEntry.realm,
          client: cleanEntry.client,
          logData: cleanEntry,
          environment: cleanEntry.environment,
          application: cleanEntry.application,
          logLevel: cleanEntry.level
        })
      });

      if (response.ok) {
        return 'success';
      }

      // 401 means auth token not ready yet - retry later
      if (response.status === 401) {
        const retryCount = logEntry._retryCount || 0;
        if (retryCount === 0) {
          // Only log on first attempt to avoid spam
          console.debug('Remote logging: auth not ready, will retry...');
        }
        return 'retry';
      }

      // Other errors - log and don't retry
      console.error(`Remote logging failed: ${response.status}`);
      return 'error';
    } catch (error) {
      // Network error - log but don't crash
      console.error('Exception sending log to remote service:', error);
      return 'error';
    }
  }
}