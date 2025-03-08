// auth.service.ts
export class LogAuthService {
    /**
     * Logs an authentication-related debug message.
     *
     * @param {string} message - The debug message to log.
     * @param {*} [data] - Optional data to include in the log entry.
     */
    public logAuthDebug(message: string, data?: any): void {
      const timestamp = new Date().toISOString();
      const logEntry = {
        timestamp,
        message,
        data
      };
  
      // Store in localStorage with a unique key
      localStorage.setItem(`auth_log_${timestamp}`, JSON.stringify(logEntry));
  
      // Keep only the last 10 log entries
      const keys = Object.keys(localStorage)
        .filter(key => key.startsWith('auth_log_'))
        .sort()
        .reverse();
  
      if (keys.length > 10) {
        keys.slice(10).forEach(key => localStorage.removeItem(key));
      }
    }
  }