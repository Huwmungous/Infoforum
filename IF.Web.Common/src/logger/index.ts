/**
 * @sfd/web-common/logger
 *
 * Low-level logger API for direct LoggerService access.
 * For most apps, prefer using useAppContext().createLogger(category) instead.
 */

export { LoggerService } from './LoggerService';
export type { LogLevel, SfdLoggerConfiguration, SfdLogEntry } from './SfdLogger';
