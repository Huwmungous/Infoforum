import { useAuth } from 'react-oidc-context';
import { Chat } from './components/Chat';
import './styles/global.scss';

function App() {
  const auth = useAuth();

  if (auth.isLoading) {
    return (
      <div className="loading-screen">
        <div className="loading-content">
          <h1>IFOllama</h1>
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  if (auth.error) {
    return (
      <div className="error-screen">
        <div className="error-content">
          <h1>Authentication Error</h1>
          <p>{auth.error.message}</p>
          <button className="button" onClick={() => auth.signinRedirect()}>
            Try Again
          </button>
        </div>
      </div>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <div className="login-screen">
        <div className="login-content">
          <h1>IFOllama</h1>
          <p>AI Assistant with MCP Tool Integration</p>
          <button className="button" onClick={() => auth.signinRedirect()}>
            Sign In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="app">
      <Chat />
    </div>
  );
}

export default App;
