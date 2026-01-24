import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { IfLogger, LogLevel, IfLoggerConfiguration } from './IfLogger';

// Mock ConfigService
vi.mock('../config', () => ({
  ConfigService: {
    Realm: 'test-realm',
    ClientId: 'test-client',
    AppName: 'TestApp',
    Environment: 'test',
  },
}));

describe('IfLogger', () => {
  let consoleDebugSpy: ReturnType<typeof vi.spyOn>;
  let consoleLogSpy: ReturnType<typeof vi.spyOn>;
  let consoleWarnSpy: ReturnType<typeof vi.spyOn>;
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  const createLogger = (
    minimumLogLevel: LogLevel = 'Information',
    loggerService = ''
  ): IfLogger => {
    const config: Required<IfLoggerConfiguration> = {
      minimumLogLevel,
      loggerService,
    };
    return new IfLogger('TestCategory', config);
  };

  beforeEach(() => {
    consoleDebugSpy = vi.spyOn(console, 'debug').mockImplementation(() => {});
    consoleLogSpy = vi.spyOn(console, 'log').mockImplementation(() => {});
    consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('isEnabled', () => {
    it('should return true when log level is >= minimum level', () => {
      const logger = createLogger('Information');

      expect(logger.isEnabled('Information')).toBe(true);
      expect(logger.isEnabled('Warning')).toBe(true);
      expect(logger.isEnabled('Error')).toBe(true);
      expect(logger.isEnabled('Critical')).toBe(true);
    });

    it('should return false when log level is < minimum level', () => {
      const logger = createLogger('Warning');

      expect(logger.isEnabled('Trace')).toBe(false);
      expect(logger.isEnabled('Debug')).toBe(false);
      expect(logger.isEnabled('Information')).toBe(false);
    });

    it('should respect all log levels in order', () => {
      const levels: LogLevel[] = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];

      for (let i = 0; i < levels.length; i++) {
        const logger = createLogger(levels[i]);

        // All levels >= current should be enabled
        for (let j = i; j < levels.length; j++) {
          expect(logger.isEnabled(levels[j])).toBe(true);
        }

        // All levels < current should be disabled
        for (let j = 0; j < i; j++) {
          expect(logger.isEnabled(levels[j])).toBe(false);
        }
      }
    });
  });

  describe('local logging', () => {
    it('should log Trace messages to console.debug', () => {
      const logger = createLogger('Trace');
      logger.trace('test trace message');

      expect(consoleDebugSpy).toHaveBeenCalledTimes(1);
      expect(consoleDebugSpy.mock.calls[0][0]).toContain('[Trace]');
      expect(consoleDebugSpy.mock.calls[0][0]).toContain('TestCategory');
      expect(consoleDebugSpy.mock.calls[0][0]).toContain('test trace message');
    });

    it('should log Debug messages to console.debug', () => {
      const logger = createLogger('Debug');
      logger.debug('test debug message');

      expect(consoleDebugSpy).toHaveBeenCalledTimes(1);
      expect(consoleDebugSpy.mock.calls[0][0]).toContain('[Debug]');
    });

    it('should log Information messages to console.log', () => {
      const logger = createLogger('Information');
      logger.info('test info message');

      expect(consoleLogSpy).toHaveBeenCalledTimes(1);
      expect(consoleLogSpy.mock.calls[0][0]).toContain('[Information]');
    });

    it('should log Warning messages to console.warn', () => {
      const logger = createLogger('Warning');
      logger.warn('test warning message');

      expect(consoleWarnSpy).toHaveBeenCalledTimes(1);
      expect(consoleWarnSpy.mock.calls[0][0]).toContain('[Warning]');
    });

    it('should log Error messages to console.error', () => {
      const logger = createLogger('Error');
      logger.error('test error message');

      expect(consoleErrorSpy).toHaveBeenCalledTimes(1);
      expect(consoleErrorSpy.mock.calls[0][0]).toContain('[Error]');
    });

    it('should log Critical messages to console.error', () => {
      const logger = createLogger('Critical');
      logger.critical('test critical message');

      expect(consoleErrorSpy).toHaveBeenCalledTimes(1);
      expect(consoleErrorSpy.mock.calls[0][0]).toContain('[Critical]');
    });

    it('should include exception in console output', () => {
      const logger = createLogger('Error');
      const error = new Error('Test exception');
      logger.error('error with exception', error);

      expect(consoleErrorSpy).toHaveBeenCalledWith(error);
    });

    it('should not log when level is below minimum', () => {
      const logger = createLogger('Error');
      logger.debug('should not appear');
      logger.info('should not appear');
      logger.warn('should not appear');

      expect(consoleDebugSpy).not.toHaveBeenCalled();
      expect(consoleLogSpy).not.toHaveBeenCalled();
      expect(consoleWarnSpy).not.toHaveBeenCalled();
    });
  });

  describe('remote logging', () => {
    it('should POST log entry to remote service when configured', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
      });
      global.fetch = mockFetch;

      const logger = createLogger('Information', 'https://logger.example.com/api/log');
      logger.info('test remote message');

      // Wait for async queue processing
      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(mockFetch).toHaveBeenCalledWith(
        'https://logger.example.com/api/log',
        expect.objectContaining({
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
        })
      );

      // Verify the body structure
      const callBody = JSON.parse(mockFetch.mock.calls[0][1].body);
      expect(callBody).toMatchObject({
        realm: 'test-realm',
        client: 'test-client',
        environment: 'test',
        application: 'TestApp',
        logLevel: 'Information',
      });
      expect(callBody.logData).toMatchObject({
        level: 'Information',
        category: 'TestCategory',
        message: 'test remote message',
      });
    });

    it('should not POST to remote when loggerService is empty', async () => {
      const mockFetch = vi.fn();
      global.fetch = mockFetch;

      const logger = createLogger('Information', '');
      logger.info('test message');

      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(mockFetch).not.toHaveBeenCalled();
    });

    it('should handle remote POST failure gracefully', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
      });
      global.fetch = mockFetch;

      const logger = createLogger('Information', 'https://logger.example.com/api/log');

      // This should not throw
      expect(() => logger.info('test message')).not.toThrow();

      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(consoleErrorSpy).toHaveBeenCalledWith('Remote logging failed: 500');
    });

    it('should handle network error gracefully', async () => {
      const mockFetch = vi.fn().mockRejectedValue(new Error('Network error'));
      global.fetch = mockFetch;

      const logger = createLogger('Information', 'https://logger.example.com/api/log');

      expect(() => logger.info('test message')).not.toThrow();

      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Exception sending log to remote service:',
        expect.any(Error)
      );
    });

    it('should include exception details in log entry', async () => {
      const mockFetch = vi.fn().mockResolvedValue({ ok: true });
      global.fetch = mockFetch;

      const logger = createLogger('Error', 'https://logger.example.com/api/log');
      const error = new Error('Test error');
      error.stack = 'Error: Test error\n    at test.ts:1:1';

      logger.error('error occurred', error);

      await new Promise((resolve) => setTimeout(resolve, 50));

      const callBody = JSON.parse(mockFetch.mock.calls[0][1].body);
      expect(callBody.logData.exception).toBe('Test error');
      expect(callBody.logData.stackTrace).toContain('Error: Test error');
    });
  });

  describe('log method with eventId and eventName', () => {
    it('should include eventId and eventName in log entry', async () => {
      const mockFetch = vi.fn().mockResolvedValue({ ok: true });
      global.fetch = mockFetch;

      const logger = createLogger('Information', 'https://logger.example.com/api/log');
      logger.log('Information', 'test message', undefined, 1001, 'UserLogin');

      await new Promise((resolve) => setTimeout(resolve, 50));

      const callBody = JSON.parse(mockFetch.mock.calls[0][1].body);
      expect(callBody.logData.eventId).toBe(1001);
      expect(callBody.logData.eventName).toBe('UserLogin');
    });
  });

  describe('convenience methods', () => {
    it('trace() should call log with Trace level', () => {
      const logger = createLogger('Trace');
      const logSpy = vi.spyOn(logger, 'log');

      logger.trace('trace message');

      expect(logSpy).toHaveBeenCalledWith('Trace', 'trace message', undefined);
    });

    it('debug() should call log with Debug level', () => {
      const logger = createLogger('Debug');
      const logSpy = vi.spyOn(logger, 'log');

      logger.debug('debug message');

      expect(logSpy).toHaveBeenCalledWith('Debug', 'debug message', undefined);
    });

    it('info() should call log with Information level', () => {
      const logger = createLogger('Information');
      const logSpy = vi.spyOn(logger, 'log');

      logger.info('info message');

      expect(logSpy).toHaveBeenCalledWith('Information', 'info message', undefined);
    });

    it('warn() should call log with Warning level', () => {
      const logger = createLogger('Warning');
      const logSpy = vi.spyOn(logger, 'log');

      logger.warn('warning message');

      expect(logSpy).toHaveBeenCalledWith('Warning', 'warning message', undefined);
    });

    it('error() should call log with Error level', () => {
      const logger = createLogger('Error');
      const logSpy = vi.spyOn(logger, 'log');
      const error = new Error('test');

      logger.error('error message', error);

      expect(logSpy).toHaveBeenCalledWith('Error', 'error message', error);
    });

    it('critical() should call log with Critical level', () => {
      const logger = createLogger('Critical');
      const logSpy = vi.spyOn(logger, 'log');

      logger.critical('critical message');

      expect(logSpy).toHaveBeenCalledWith('Critical', 'critical message', undefined);
    });
  });

  describe('log entry structure', () => {
    it('should include all required fields in log entry', async () => {
      const mockFetch = vi.fn().mockResolvedValue({ ok: true });
      global.fetch = mockFetch;

      const logger = createLogger('Information', 'https://logger.example.com/api/log');
      logger.info('test message');

      await new Promise((resolve) => setTimeout(resolve, 50));

      const callBody = JSON.parse(mockFetch.mock.calls[0][1].body);

      expect(callBody.logData).toMatchObject({
        realm: 'test-realm',
        client: 'test-client',
        level: 'Information',
        category: 'TestCategory',
        message: 'test message',
        application: 'TestApp',
        environment: 'test',
      });

      // Should have timestamp in ISO format
      expect(callBody.logData.timestamp).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/);
    });
  });
});
