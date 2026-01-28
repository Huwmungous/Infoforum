# @if/web-common-react

React bindings for [@if/web-common](../if-web-common). Provides components, hooks, and context providers for authentication and configuration.

## Installation

```bash
npm install @if/web-common @if/web-common-react
```

## Quick Start

```tsx
import { AppInitializer, useAuth, useAppContext } from '@if/web-common-react';

function App() {
  return (
    <AppInitializer appType="user">
      <MyApp />
    </AppInitializer>
  );
}

function MyApp() {
  const { auth, createLogger } = useAppContext();
  const logger = createLogger('MyApp');

  if (!auth.isAuthenticated) {
    return <button onClick={() => auth.signin()}>Sign In</button>;
  }

  logger.info('User is authenticated');
  return <div>Welcome, {auth.user?.profile.name}</div>;
}
```

## Components

### AppInitializer

The main entry point component that handles all initialisation.

```tsx
import { AppInitializer } from '@if/web-common-react';

// Dynamic configuration (fetches from config service)
<AppInitializer appType="user">
  <App />
</AppInitializer>

// Static configuration
<AppInitializer
  staticConfig={{
    clientId: 'my-client-id',
    openIdConfig: 'https://auth.example.com/realms/my-realm',
    loggerService: 'https://logs.example.com/api/log'
  }}
>
  <App />
</AppInitializer>

// With auth config override (useful for local development)
<AppInitializer
  appType="user"
  staticAuthConfig={{
    clientId: 'dev-client',
    authority: 'http://localhost:8080/realms/dev'
  }}
>
  <App />
</AppInitializer>
```

**Props:**

| Prop | Type | Description |
|------|------|-------------|
| `appType` | `'user' \| 'service'` | Application type for config service |
| `staticConfig` | `StaticConfigOverride` | Static configuration (bypasses config service) |
| `staticAuthConfig` | `StaticAuthConfigOverride` | Override auth settings |
| `children` | `ReactNode` | Application content |
| `loadingComponent` | `ReactNode` | Custom loading UI |
| `errorComponent` | `(error: string) => ReactNode` | Custom error UI |

### ProtectedRoute

Guards routes that require authentication.

```tsx
import { ProtectedRoute } from '@if/web-common-react';

<ProtectedRoute>
  <ProtectedContent />
</ProtectedRoute>

// With custom loading/unauthorised UI
<ProtectedRoute
  loadingComponent={<Spinner />}
  unauthorizedComponent={<LoginPrompt />}
>
  <ProtectedContent />
</ProtectedRoute>
```

### Callback Components

Handle OIDC redirect callbacks.

```tsx
import { SigninCallback, SignoutCallback, SilentCallback } from '@if/web-common-react';

// In your router:
<Route path="/auth/callback" element={<SigninCallback />} />
<Route path="/auth/logout" element={<SignoutCallback />} />
<Route path="/auth/silent" element={<SilentCallback />} />
```

## Hooks

### useAppContext

Access the full application context including config, auth, and logger factory.

```tsx
import { useAppContext } from '@if/web-common-react';

function MyComponent() {
  const { config, auth, createLogger } = useAppContext();
  const logger = createLogger('MyComponent');

  // Access config
  console.log(config.clientId);

  // Access auth
  if (auth.isAuthenticated) {
    console.log(auth.user?.profile.name);
  }

  // Use logger
  logger.info('Component rendered');
}
```

### useAuth

Convenience hook for auth-only access.

```tsx
import { useAuth } from '@if/web-common-react';

function LoginButton() {
  const { isAuthenticated, signin, signout, user } = useAuth();

  if (isAuthenticated) {
    return (
      <div>
        <span>Welcome, {user?.profile.name}</span>
        <button onClick={signout}>Sign Out</button>
      </div>
    );
  }

  return <button onClick={signin}>Sign In</button>;
}
```

**Returns:**

| Property | Type | Description |
|----------|------|-------------|
| `initialized` | `boolean` | Whether auth has completed initialisation |
| `isAuthenticated` | `boolean` | Whether user is logged in |
| `user` | `User \| null` | Current user object |
| `signin` | `() => Promise<void>` | Redirect to login |
| `signout` | `() => Promise<void>` | Redirect to logout |
| `renewToken` | `() => Promise<User>` | Silent token renewal |
| `getAccessToken` | `() => Promise<string \| null>` | Get current access token |

## Context Providers

For advanced use cases, you can use the providers directly:

```tsx
import { AuthProvider, AppContextProvider } from '@if/web-common-react';

// Customise the provider hierarchy
<AuthProvider>
  <AppContextProvider>
    <App />
  </AppContextProvider>
</AuthProvider>
```

## Environment Variables

The following environment variables are read by `AppInitializer`:

| Variable | Description |
|----------|-------------|
| `VITE_IF_CONFIG_SERVICE_URL` | URL to configuration service |
| `VITE_IF_REALM` | Keycloak/OIDC realm name |
| `VITE_IF_CLIENT` | OIDC client identifier |
| `VITE_IF_APP_NAME` | Application name for logging |
| `VITE_IF_ENVIRONMENT` | Environment (DEV, SIT, UAT, PRD) |

## Licence

MIT
