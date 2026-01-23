import React, { useEffect, useState, useRef, ReactNode } from 'react';
import { useAuthInternal } from '../contexts/AuthContext';
import { authService } from '@if/web-common';
import { LoggerService } from '@if/web-common';

export interface SigninCallbackProps {
  onSuccess?: () => void;
  redirectUrl?: string;
  errorComponent?: (error: string) => ReactNode;
  loadingComponent?: ReactNode;
}

export function SigninCallback({
  onSuccess,
  redirectUrl = '/',
  errorComponent,
  loadingComponent
}: SigninCallbackProps) {
  const { setUser, initialized } = useAuthInternal();
  const [error, setError] = useState<string | null>(null);
  const [logger, setLogger] = useState<ReturnType<typeof LoggerService.create> | null>(null);
  const processingRef = useRef(false);

  useEffect(() => {
    if (!initialized) {
      console.log('Waiting for auth initialization...');
      return;
    }

    if (processingRef.current) {
      return;
    }
    processingRef.current = true;

    const callbackLogger = LoggerService.create('SigninCallback');
    setLogger(callbackLogger);

    const handleCallback = async () => {
      try {
        const user = await authService.completeSignin();

        setUser(user);

        const username = user.profile.preferred_username || user.profile.email;
        const userInfo = `userId: ${user.profile.sub}, email: ${user.profile.email}, scopes: ${user.scope}`;
        callbackLogger.info(`User ${username} authenticated successfully. ${userInfo}`);

        if (onSuccess) {
          onSuccess();
        } else {
          window.location.href = redirectUrl;
        }
      } catch (err: any) {
        // Check if this is a recoverable error that should restart the auth flow:
        // - State errors: stale callback, expired state, page refresh, etc.
        // - IdP errors: authentication_expired, login_required (another tab logged in), etc.
        if (authService.shouldRestartAuth(err)) {
          // First, check if user is already authenticated (e.g., page refresh after successful login)
          const existingUser = await authService.getUser();
          if (existingUser) {
            callbackLogger.info('Callback already processed, user is authenticated');
            setUser(existingUser);
            if (onSuccess) {
              onSuccess();
            } else {
              window.location.href = redirectUrl;
            }
            return;
          }

          // User is not authenticated - auth request expired or was invalidated
          // Standard OIDC pattern: clear stale state and restart the auth flow
          callbackLogger.warn('Authentication callback expired or invalidated, restarting auth flow', err);
          try {
            await authService.clearStaleState();
            await authService.signin();
            return;
          } catch (restartErr: any) {
            callbackLogger.error('Failed to restart auth flow', restartErr);
            setError('Authentication session expired. Please try again.');
            return;
          }
        }

        // Non-recoverable errors: log and display to user
        console.error('Signin callback error:', err);
        callbackLogger.error('Authentication failed', err);
        setError(err.message);
      }
    };

    handleCallback();
  }, [setUser, onSuccess, redirectUrl, initialized]);

  if (error) {
    if (errorComponent) {
      return <>{errorComponent(error)}</>;
    }

    return (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
        <div className="bg-red-50 border border-red-200 p-6 rounded-lg shadow-md max-w-2xl">
          <h2 className="text-xl font-bold text-red-800 mb-2">Authentication Error</h2>
          <p className="text-red-700 mb-4">{error}</p>
          <button
            onClick={() => window.location.href = redirectUrl}
            className="px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
          >
            Go to Home
          </button>
        </div>
      </div>
    );
  }

  if (loadingComponent) {
    return <>{loadingComponent}</>;
  }

  return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center">
      <div className="bg-white p-8 rounded-lg shadow-md">
        <div className="flex items-center space-x-3">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
          <div className="text-lg">Completing authentication...</div>
        </div>
      </div>
    </div>
  );
}
