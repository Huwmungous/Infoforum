import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { authService, AuthConfig, LoggerService, ConfigService, User } from '@if/web-common';

export interface AuthContextValue {
  user: User | null;
  loading: boolean;
  error: string | null;
  initialized: boolean;
  isAuthenticated: boolean;
  signin: () => Promise<void>;
  signout: () => Promise<void>;
  renewToken: () => Promise<User>;
  getAccessToken: () => Promise<string | null>;
  setUser: (user: User | null) => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** Internal hook - use useAuth() from root for public API */
export const useAuthInternal = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuthInternal must be used within an AuthProvider');
  }
  return context;
};

export interface AuthProviderProps {
  children: ReactNode;
  config: AuthConfig;
}

interface AuthState {
  user: User | null;
  loading: boolean;
  error: string | null;
  initialized: boolean;
}

export function AuthProvider({ children, config }: AuthProviderProps) {
  // FIXED: Use atomic state updates to prevent race conditions
  const [state, setState] = useState<AuthState>({
    user: null,
    loading: true,
    error: null,
    initialized: false
  });
  
  const [logger, setLogger] = useState<ReturnType<typeof LoggerService.create> | null>(null);

  useEffect(() => {
    const initAuth = async () => {
      try {
        // ConfigService must already be initialized by AppInitializer
        if (!ConfigService.isInitialized) {
          throw new Error('ConfigService must be initialized before AuthProvider. Use AppInitializer.');
        }
        
        // Create logger AFTER ConfigService is initialized
        const authLogger = LoggerService.create('AuthProvider');
        setLogger(authLogger);
         
        await authService.initialize(config);

        const currentUser = await authService.getUser();
        
        if (currentUser && !currentUser.expired) {
          // Log existing authenticated session
          const username = currentUser.profile.preferred_username || currentUser.profile.email || 'unknown';
          const expiresAt = currentUser.expires_at ? new Date(currentUser.expires_at * 1000).toISOString() : 'unknown';
          authLogger.info(`Existing session found for user: ${username}, expires: ${expiresAt}`);

          // FIXED: Atomic state update
          setState({
            user: currentUser,
            loading: false,
            error: null,
            initialized: true
          });
        } else {
          authLogger.debug('No existing session found');
          // FIXED: Atomic state update
          setState({
            user: null,
            loading: false,
            error: null,
            initialized: true
          });
        }
      } catch (err: any) {
        console.error('AuthProvider: Failed to initialize auth:', err);
        
        // Try to log auth initialization failure
        try {
          const failLogger = LoggerService.create('AuthProvider');
          failLogger.warn(`Authentication initialization failed: ${err.message}`, err);
        } catch {
          // Logger not available, console.error already logged
        }
        
        // FIXED: Atomic state update
        setState({
          user: null,
          loading: false,
          error: err.message,
          initialized: false
        });
      }
    };

    initAuth();

    // Subscribe to auth state changes from UserManager events
    const unsubscribe = authService.onUserChange((user) => {
      setState(prev => ({ ...prev, user }));
    });

    return () => unsubscribe();
  }, [config]);

  const signin = async () => {
    try {
      if (!state.initialized) {
        throw new Error('Authentication system not initialized. Please wait...');
      }

      logger?.debug('Initiating signin redirect');
      setState(prev => ({ ...prev, error: null }));
      await authService.signin();
    } catch (err: any) {
      logger?.warn(`Sign-in redirect failed: ${err.message}`, err);
      setState(prev => ({ ...prev, error: err.message }));
      throw err;
    }
  };

  const signout = async () => {
    try {
      if (!state.initialized) {
        throw new Error('Authentication system not initialized');
      }

      setState(prev => ({ ...prev, error: null }));

      if (state.user && logger) {
        const username = state.user.profile.preferred_username || state.user.profile.email;
        const sessionDuration = state.user.expires_at ? Math.floor((state.user.expires_at - (Date.now() / 1000)) / 60) : 'unknown';
        const userInfo = `userId: ${state.user.profile.sub}, sessionDuration: ${sessionDuration} minutes`;
        logger.info(`User ${username} signing out. ${userInfo}`);
      } else if (logger) {
        logger.info('No user session found');
      }

      await authService.signout();

      setState(prev => ({ ...prev, user: null }));
    } catch (err: any) {
      console.error('Signout error:', err);
      logger?.error('User sign-out failed', err);
      setState(prev => ({ ...prev, error: err.message }));
      throw err;
    }
  };

  const renewToken = async () => {
    try {
      // FIXED: Defensive guard
      if (!state.initialized) {
        throw new Error('Authentication system not initialized');
      }

      logger?.debug('Attempting token renewal');
      setState(prev => ({ ...prev, error: null }));
      const renewedUser = await authService.renewToken();
      setState(prev => ({ ...prev, user: renewedUser }));
      logger?.info('Token renewed successfully');
      return renewedUser;
    } catch (err: any) {
      logger?.error('Token renewal failed', err);
      setState(prev => ({ ...prev, error: err.message }));
      throw err;
    }
  };

  const getAccessToken = async () => {
    // FIXED: Defensive guard
    if (!state.initialized) {
      throw new Error('Authentication system not initialized');
    }
    return authService.getAccessToken();
  };

  const setUser = (user: User | null) => {
    setState(prev => ({ ...prev, user }));
  };

  const value: AuthContextValue = {
    user: state.user,
    loading: state.loading,
    error: state.error,
    initialized: state.initialized,
    isAuthenticated: state.user !== null && !state.user.expired,
    signin,
    signout,
    getAccessToken,
    renewToken,    
    setUser,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}