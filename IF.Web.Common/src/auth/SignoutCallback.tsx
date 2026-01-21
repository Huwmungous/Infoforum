import React, { useEffect, useRef } from 'react';
import { authService } from './AuthService';
import { LoggerService } from '../logger';

export interface SignoutCallbackProps {
  redirectUrl?: string;
}

export function SignoutCallback({ redirectUrl = '/' }: SignoutCallbackProps) {
  const processingRef = useRef(false);

  useEffect(() => {
    if (processingRef.current) {
      return;
    }
    processingRef.current = true;

    const logger = LoggerService.create('SignoutCallback');

    const handleSignoutCallback = async () => {
      try {
        await authService.completeSignout();
        window.location.replace(redirectUrl);
      } catch (err: any) {
        logger.error('Signout callback error', err);
        window.location.replace(redirectUrl);
      }
    };

    handleSignoutCallback();
  }, [redirectUrl]);

  return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center">
      <div className="bg-white p-8 rounded-lg shadow-md">
        <div className="flex items-center space-x-3">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
          <div className="text-lg">Completing sign out...</div>
        </div>
      </div>
    </div>
  );
}
