import React, { useEffect, useRef, ReactNode, useMemo } from 'react';
import { useAuthInternal } from './AuthContext';
import { LoggerService } from '../logger';

export interface ProtectedRouteProps {
  /**
   * Content to render when authenticated.
   * For react-router-dom nested routes, pass <Outlet /> here.
   * Optional - if not provided, renders nothing (useful as a guard wrapper).
   */
  children?: ReactNode;
  loadingComponent?: ReactNode;
  redirectingComponent?: ReactNode;
}

export function ProtectedRoute({
  children,
  loadingComponent,
  redirectingComponent
}: ProtectedRouteProps) {
  const { isAuthenticated, loading, signin, initialized } = useAuthInternal();

  const loginAttemptedRef = useRef(false);
  const wasAuthenticatedRef = useRef(false);

  const logger = useMemo(() => LoggerService.create('ProtectedRoute'), []);

  // 1) Log auth state only when the state changes (not on every render)
  useEffect(() => {
    if (!initialized || loading) {
      logger.debug(`Auth state: initialized=${initialized}, loading=${loading}`);
    }
  }, [initialized, loading, logger]);

  // 2) Redirect to signin (keep deps honest; guard prevents repeats)
  useEffect(() => {
    if (initialized && !loading && !isAuthenticated && !loginAttemptedRef.current) {
      loginAttemptedRef.current = true;
      logger.debug('User not authenticated, redirecting to signin');
      void signin();
    }

    // Reset the guard after successful signin
    if (isAuthenticated) {
      loginAttemptedRef.current = false;
    }
  }, [initialized, loading, isAuthenticated, signin, logger]);

  // 3) Log "access granted" only on transition false -> true
  useEffect(() => {
    if (isAuthenticated && !wasAuthenticatedRef.current) {
      wasAuthenticatedRef.current = true;
      logger.debug('User authenticated, access granted');
    }
    if (!isAuthenticated && wasAuthenticatedRef.current) {
      wasAuthenticatedRef.current = false;
    }
  }, [isAuthenticated, logger]);

  if (!initialized || loading) {
    return loadingComponent ? <>{loadingComponent}</> : (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center">
        <div className="bg-white p-8 rounded-lg shadow-md">
          <div className="flex items-center space-x-3">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            <div className="text-lg">Loading...</div>
          </div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return redirectingComponent ? <>{redirectingComponent}</> : (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center">
        <div className="bg-white p-8 rounded-lg shadow-md">
          <div className="flex items-center space-x-3">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            <div className="text-lg">Redirecting to login...</div>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
