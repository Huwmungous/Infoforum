import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { LoggerService } from './LoggerService';
import { SfdLoggerProvider } from './SfdLoggerProvider';
import { ConfigService } from '../configServiceClient';

// Mock SfdLoggerProvider for unit tests only
vi.mock('./SfdLoggerProvider', () => ({
  SfdLoggerProvider: {
    configure: vi.fn(),
    createLogger: vi.fn(() => ({
      trace: vi.fn(),
      debug: vi.fn(),
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      critical: vi.fn(),
      isEnabled: vi.fn().mockReturnValue(true),
      log: vi.fn(),
    })),
    clearLoggers: vi.fn(),
  },
}));

// Mock ConfigService
vi.mock('../configServiceClient', () => ({
  ConfigService: {
    isInitialized: false,
    LoggerService: 'https://logger.example.com/api/log',
    LogLevel: 'Information',
    Realm: 'test-realm',
    Client: 'test-client',
    AppName: 'TestApp',
    Environment: 'test',
    initialize: vi.fn().mockResolvedValue(undefined),
  },
}));

describe('LoggerService - Unit Tests', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    LoggerService.reset();

    // Reset ConfigService mock state
    (ConfigService as any).isInitialized = false;
    (ConfigService as any).LoggerService = 'https://logger.example.com/api/log';
    (ConfigService as any).LogLevel = 'Information';
  });

  afterEach(() => {
    LoggerService.reset();
  });

  describe('create', () => {
    it('should create a new LoggerService instance', () => {
      const logger = LoggerService.create('TestCategory');

      expect(logger).toBeInstanceOf(LoggerService);
      expect(SfdLoggerProvider.createLogger).toHaveBeenCalledWith('TestCategory');
    });

    it('should auto-configure from ConfigService when initialized', () => {
      (ConfigService as any).isInitialized = true;

      LoggerService.create('TestCategory');

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        loggerService: 'https://logger.example.com/api/log',
        minimumLogLevel: 'Information',
      });
    });

    it('should use default config when ConfigService is not initialized', () => {
      (ConfigService as any).isInitialized = false;

      LoggerService.create('TestCategory');

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        loggerService: '',
        minimumLogLevel: 'Information',
      });
    });
  });

  describe('getLogger', () => {
    it('should return a LoggerService instance', () => {
      const logger = LoggerService.getLogger('TestCategory');

      expect(logger).toBeInstanceOf(LoggerService);
    });
  });

  describe('configure', () => {
    it('should configure SfdLoggerProvider with provided config', () => {
      LoggerService.configure({
        loggerService: 'https://custom-logger.example.com',
        minimumLogLevel: 'Debug',
      });

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        loggerService: 'https://custom-logger.example.com',
        minimumLogLevel: 'Debug',
      });
    });

    it('should accept partial configuration', () => {
      LoggerService.configure({
        minimumLogLevel: 'Error',
      });

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        minimumLogLevel: 'Error',
      });
    });
  });

  describe('configureFromConfigService', () => {
    it('should throw error when ConfigService is not initialized', async () => {
      (ConfigService as any).isInitialized = false;

      await expect(LoggerService.configureFromConfigService()).rejects.toThrow(
        'ConfigService must be initialized before configuring LoggerService'
      );
    });

    it('should configure with ConfigService values', async () => {
      (ConfigService as any).isInitialized = true;
      (ConfigService as any).LoggerService = 'https://config-logger.example.com';
      (ConfigService as any).LogLevel = 'Warning';

      await LoggerService.configureFromConfigService();

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        loggerService: 'https://config-logger.example.com',
        minimumLogLevel: 'Warning',
      });
    });

    it('should handle null LoggerService URL', async () => {
      (ConfigService as any).isInitialized = true;
      (ConfigService as any).LoggerService = null;

      await LoggerService.configureFromConfigService();

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith({
        loggerService: '',
        minimumLogLevel: 'Information',
      });
    });
  });

  describe('log level validation', () => {
    it('should accept valid string log levels', () => {
      (ConfigService as any).isInitialized = true;

      const validLevels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];

      for (const level of validLevels) {
        vi.clearAllMocks();
        LoggerService.reset();
        (ConfigService as any).LogLevel = level;

        LoggerService.create('Test');

        expect(SfdLoggerProvider.configure).toHaveBeenCalledWith(
          expect.objectContaining({ minimumLogLevel: level })
        );
      }
    });

    it('should convert numeric log levels to strings', () => {
      (ConfigService as any).isInitialized = true;
      (ConfigService as any).LogLevel = 0; // Trace

      LoggerService.create('Test');

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith(
        expect.objectContaining({ minimumLogLevel: 'Trace' })
      );
    });

    it('should convert numeric string log levels', () => {
      (ConfigService as any).isInitialized = true;
      (ConfigService as any).LogLevel = '2'; // Information

      LoggerService.create('Test');

      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith(
        expect.objectContaining({ minimumLogLevel: 'Information' })
      );
    });

    it('should fallback to Information for invalid log levels', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
      (ConfigService as any).isInitialized = true;
      (ConfigService as any).LogLevel = 'InvalidLevel';

      LoggerService.create('Test');

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining("Invalid log level 'InvalidLevel'")
      );
      expect(SfdLoggerProvider.configure).toHaveBeenCalledWith(
        expect.objectContaining({ minimumLogLevel: 'Information' })
      );

      consoleWarnSpy.mockRestore();
    });
  });

  describe('reset', () => {
    it('should clear configured state', () => {
      (ConfigService as any).isInitialized = true;

      LoggerService.create('Test');
      LoggerService.reset();

      // After reset, next create should reconfigure
      LoggerService.create('Test2');

      // configure should be called twice (once before reset, once after)
      expect(SfdLoggerProvider.configure).toHaveBeenCalledTimes(2);
    });

    it('should call SfdLoggerProvider.clearLoggers', () => {
      LoggerService.reset();

      expect(SfdLoggerProvider.clearLoggers).toHaveBeenCalled();
    });
  });
});

/**
 * Unit tests for LoggerService convenience methods.
 * These tests verify that LoggerService correctly delegates to the underlying SfdLogger.
 *
 * Note: Actual console output is tested in SfdLogger.test.ts which doesn't mock the logger.
 * This separation ensures we test:
 * 1. LoggerService delegation (here) - that methods call through correctly
 * 2. SfdLogger output (in SfdLogger.test.ts) - that actual logging works
 */
describe('LoggerService - Convenience Methods (Delegation)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    LoggerService.reset();

    // Reset ConfigService mock state
    (ConfigService as any).isInitialized = true;
    (ConfigService as any).LoggerService = '';
    (ConfigService as any).LogLevel = 'Trace'; // Enable all levels for testing
  });

  afterEach(() => {
    LoggerService.reset();
  });

  it('should delegate trace() to underlying logger with correct arguments', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.trace('trace message');

    expect(mockSfdLogger.trace).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.trace).toHaveBeenCalledWith('trace message', undefined);
  });

  it('should delegate trace() with exception to underlying logger', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;
    const error = new Error('trace error');

    logger.trace('trace with error', error);

    expect(mockSfdLogger.trace).toHaveBeenCalledWith('trace with error', error);
  });

  it('should delegate debug() to underlying logger with correct arguments', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.debug('debug message');

    expect(mockSfdLogger.debug).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.debug).toHaveBeenCalledWith('debug message', undefined);
  });

  it('should delegate info() to underlying logger with correct arguments', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.info('info message');

    expect(mockSfdLogger.info).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.info).toHaveBeenCalledWith('info message', undefined);
  });

  it('should delegate warn() to underlying logger with correct arguments', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.warn('warn message');

    expect(mockSfdLogger.warn).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.warn).toHaveBeenCalledWith('warn message', undefined);
  });

  it('should delegate error() to underlying logger with message only', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.error('error message');

    expect(mockSfdLogger.error).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.error).toHaveBeenCalledWith('error message', undefined);
  });

  it('should delegate error() to underlying logger with exception', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;
    const error = new Error('test error');

    logger.error('error message', error);

    expect(mockSfdLogger.error).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.error).toHaveBeenCalledWith('error message', error);
  });

  it('should delegate critical() to underlying logger with correct arguments', () => {
    const logger = LoggerService.create('TestCategory');
    const mockSfdLogger = (SfdLoggerProvider.createLogger as ReturnType<typeof vi.fn>).mock.results[0].value;

    logger.critical('critical message');

    expect(mockSfdLogger.critical).toHaveBeenCalledTimes(1);
    expect(mockSfdLogger.critical).toHaveBeenCalledWith('critical message', undefined);
  });

  it('should create logger with correct category', () => {
    LoggerService.create('MyCustomCategory');

    expect(SfdLoggerProvider.createLogger).toHaveBeenCalledWith('MyCustomCategory');
  });

  it('should create separate logger instances for different categories', () => {
    const logger1 = LoggerService.create('Category1');
    const logger2 = LoggerService.create('Category2');

    expect(SfdLoggerProvider.createLogger).toHaveBeenCalledWith('Category1');
    expect(SfdLoggerProvider.createLogger).toHaveBeenCalledWith('Category2');
    expect(logger1).not.toBe(logger2);
  });
});
