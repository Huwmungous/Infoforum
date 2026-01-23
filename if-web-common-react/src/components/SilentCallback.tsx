import { useEffect } from 'react';
import { authService } from '@if/web-common';

/**
 * Handles the silent renewal callback in an iframe.
 * This component should be rendered when:
 * 1. The app is running inside an iframe
 * 2. The route is /silent-callback
 *
 * It calls signinSilentCallback() which parses the tokens from the URL
 * and posts them back to the parent window.
 */
export function SilentCallback() {
  useEffect(() => {
    authService.completeSilentSignin()
      .catch(err => {
        console.error('Silent callback error:', err);
      });
  }, []);

  return null;
}
