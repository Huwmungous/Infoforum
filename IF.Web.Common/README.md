# @if/web-common

A framework-agnostic library for authentication, configuration, and logging. Works with any TypeScript framework: Angular, React, Vue, or vanilla TypeScript.

## Features

- **OIDC Authentication** - Built on oidc-client-ts for standards-compliant auth
- **Configuration Service** - Dynamic configuration from a remote service or static config
- **Logging Service** - Structured logging with remote logging support
- **HTTP Interceptor** - Automatic auth token injection for fetch requests
- **Zero Framework Dependencies** - Pure TypeScript, works everywhere

## Installation

```bash
npm install @if/web-common
```

## Quick Start

### Basic Setup

```typescript
import { ConfigService, AuthService, LoggerService } from '@if/web-common';

// 1. Initialise configuration
await ConfigService.initialize({
  configServiceUrl: 'https://config.example.com',
  realm: 'my-realm',
  client: 'my-client',
  appType: 'user',
  appName: 'my-app',
  environment: 'DEV'
});

// 2. Initialise authentication
const authService = AuthService.getInstance();
await authService.initialize({
  redirectUri: window.location.origin + '/auth/callback',
  silentRedirectUri: window.location.origin + '/auth/silent'
});

// 3. Create loggers
const logger = LoggerService.create('MyService');
logger.info('Application started');
```

### Angular Example

```typescript
import { Injectable } from '@angular/core';
import { AuthService, ConfigService, LoggerService } from '@if/web-common';
import type { User } from '@if/web-common';

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private authService = AuthService.getInstance();
  private logger = LoggerService.create('AuthenticationService');

  async initialize(): Promise<void> {
    await ConfigService.initialize({
      configServiceUrl: environment.configServiceUrl,
      realm: environment.realm,
      client: environment.client,
      appType: 'user',
      appName: 'my-angular-app',
      environment: environment.name
    });

    await this.authService.initialize({
      redirectUri: window.location.origin + '/auth/callback',
      silentRedirectUri: window.location.origin + '/auth/silent'
    });

    this.logger.info('Authentication initialised');
  }

  async login(): Promise<void> {
    await this.authService.signin();
  }

  async logout(): Promise<void> {
    await this.authService.signout();
  }

  async getUser(): Promise<User | null> {
    return this.authService.getUser();
  }

  async getAccessToken(): Promise<string | null> {
    return this.authService.getAccessToken();
  }

  onUserChange(callback: (user: User | null) => void): () => void {
    return this.authService.onUserChange(callback);
  }
}
```

### Using the HTTP Interceptor

```typescript
import { api, setupFetchInterceptor, AuthService } from '@if/web-common';

// Option 1: Use the pre-configured api object
const response = await api.get('https://api.example.com/data');
const data = await api.post('https://api.example.com/data', { name: 'test' });

// Option 2: Set up global fetch interception
setupFetchInterceptor({
  getAccessToken: () => AuthService.getInstance().getAccessToken(),
  excludeUrls: ['/public/', '/health']
});

// Now all fetch() calls automatically include the auth token
const response = await fetch('https://api.example.com/protected');
```

## API Reference

### AuthService

Singleton service for OIDC authentication.

```typescript
const authService = AuthService.getInstance();

// Initialise with config
await authService.initialize(config: AuthConfig);

// Authentication actions
await authService.signin();           // Redirect to login
await authService.signout();          // Redirect to logout
await authService.renewToken();       // Silent token renewal

// Get user/token
const user = await authService.getUser();
const token = await authService.getAccessToken();

// Listen for changes
const unsubscribe = authService.onUserChange((user) => {
  console.log('User changed:', user);
});

// Callback handlers (for redirect pages)
await authService.completeSignin();   // Handle signin callback
await authService.completeSignout();  // Handle signout callback
await authService.completeSilentSignin(); // Handle silent callback
```

### ConfigService

Static service for application configuration.

```typescript
// Dynamic config (fetches from config service)
await ConfigService.initialize({
  configServiceUrl: string,
  realm: string,
  client: string,
  appType: 'user' | 'service',
  appName: string,
  environment: string
});

// Static config (no remote fetch)
ConfigService.initializeWithStaticConfig({
  clientId: string,
  openIdConfig: string,
  loggerService?: string,
  logLevel?: string
});

// Access config values
ConfigService.ClientId;
ConfigService.OpenIdConfig;
ConfigService.LoggerService;
ConfigService.LogLevel;
ConfigService.isInitialized;
```

### LoggerService

Structured logging with optional remote logging.

```typescript
const logger = LoggerService.create('MyCategory');

logger.trace('Detailed trace info');
logger.debug('Debug information');
logger.info('General information');
logger.warn('Warning message');
logger.error('Error occurred', error);
logger.critical('Critical failure', error);
```

### EnvironmentConfig

Access environment variables with validation.

```typescript
import { EnvironmentConfig } from '@if/web-common';

const realm = EnvironmentConfig.get('IF_REALM');
const errors = EnvironmentConfig.validate();
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `VITE_IF_CONFIG_SERVICE_URL` | URL to configuration service |
| `VITE_IF_REALM` | Keycloak/OIDC realm name |
| `VITE_IF_CLIENT` | OIDC client identifier |
| `VITE_IF_APP_NAME` | Application name for logging |
| `VITE_IF_ENVIRONMENT` | Environment (DEV, SIT, UAT, PRD) |

## React Bindings

For React applications, see [@if/web-common-react](../if-web-common-react) which provides hooks, context providers, and components built on top of this library.

## Licence

MIT
