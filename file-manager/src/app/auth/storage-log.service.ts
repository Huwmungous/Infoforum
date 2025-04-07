// log-auth.service.ts

const MAX_LOG_ENTRIES = 3;

export class StorageLogService {
    
    public log(message: string, data?: any): void {
      const timestamp = new Date().toISOString();
      const logEntry = {
        timestamp,
        message,
        data
      };
  
      // Store in localStorage with a unique key
      localStorage.setItem(`log_${timestamp}`, JSON.stringify(logEntry));
  
      // Keep only the last  MAX_LOG_ENTRIES log entries
      const keys = Object.keys(localStorage)
        .filter(key => key.startsWith('log_'))
        .sort()
        .reverse();
  
      if (keys.length > MAX_LOG_ENTRIES) {
        keys.slice( MAX_LOG_ENTRIES).forEach(key => localStorage.removeItem(key));
      }
    }
  }