import React, { createContext, useContext, ReactNode } from 'react';
import { ConfigService } from '../configServiceClient';
import { useAuthInternal } from '../auth/AuthContext';
import { LoggerService } from '../logger';

export interface Logger {
  trace(message: string, error?: Error): void;
  debug(message: string, error?: Error): void;
  info(message: string, error?: Error): void;
  warn(message: string, error?: Error): void;
  error(message: string, error?: Error): void;
  critical(message: string, error?: Error): void;
}


export interface AppContextValue {
  config: {
    loggerService: string;
    logLevel: string;
    clientId: string;
    openIdConfig: string;
    isInitialized: boolean;
  };
  auth: {
    initialized: boolean;
    isAuthenticated: boolean;
    user: ReturnType<typeof useAuthInternal>['user'] | null;
    getAccessToken(): Promise<string | null>;
    renewToken: () => Promise<ReturnType<typeof useAuthInternal>['user']>;
    signin(): Promise<void>;
    signout(): Promise<void>;
  };
  createLogger: (category: string) => Logger;
}

const AppContext = createContext<AppContextValue | null>(null);

export function useAppContext(): AppContextValue {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useAppContext must be used within AppInitializer');
  }
  return context;
}

/** Public auth hook - thin wrapper over useAppContext().auth */
export function useAuth(): AppContextValue['auth'] {
  return useAppContext().auth;
}

interface AppContextProviderProps {
  children: ReactNode;
}

export function AppContextProvider({ children }: AppContextProviderProps) {
  const auth = useAuthInternal();

  const value: AppContextValue = {
    config: {
      loggerService: ConfigService.LoggerService || '',
      logLevel: ConfigService.LogLevel || '',
      clientId: ConfigService.ClientId || '',
      openIdConfig: ConfigService.OpenIdConfig || '',
      isInitialized: ConfigService.isInitialized,
    },
    auth: {
      initialized: auth.initialized,
      isAuthenticated: auth.isAuthenticated,
      user: auth.user,
      getAccessToken: () => auth.getAccessToken(),
      renewToken: () => auth.renewToken(),
      signin: () => auth.signin(),
      signout: () => auth.signout(),
    },
    createLogger: (category: string): Logger => {
      return LoggerService.create(category);
    },
  };

  return (
    <AppContext.Provider value={value}>
      {children}
    </AppContext.Provider>
  );
}
