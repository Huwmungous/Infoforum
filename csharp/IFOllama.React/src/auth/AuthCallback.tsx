import { useEffect } from 'react';
import { useAuth } from 'react-oidc-context';
import { useNavigate } from 'react-router-dom';

export function AuthCallback() {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!auth.isLoading) {
      if (auth.isAuthenticated) {
        navigate('/', { replace: true });
      } else if (auth.error) {
        console.error('Auth callback error:', auth.error);
        navigate('/', { replace: true });
      }
    }
  }, [auth.isLoading, auth.isAuthenticated, auth.error, navigate]);

  return (
    <div className="auth-callback">
      <div className="callback-content">
        <h2>Completing sign in...</h2>
        <div className="spinner"></div>
      </div>
    </div>
  );
}
