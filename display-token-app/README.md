# display-token-app

Keycloak OIDC Authentication React Application

## Configuration

This application is configured with:
- Config Service URL: https://sfddevelopment.com/config
- Config Type: user
- Port: 5313
- Tailwind CSS: v4 (latest)

The config service should return a JSON response with:
```json
{
  "oidcConfigUrl": "https://your-keycloak-server/realms/your-realm/.well-known/openid-configuration",
  "clientId": "your-client-id"
}
```

## Running the Application

Development mode:
npm run dev

The application will run on: http://localhost:5313

The port is configured in vite.config.js

Production build:
npm run build

## Environment Variables

The application uses the following environment variables (configured in .env):

- VITE_CONFIG_SERVICE_URL
- VITE_CONFIG_TYPE

Note: Port 5313 is configured in vite.config.js, not .env

## Features

- Single config service URL for centralized configuration
- OpenID Connect authentication with Keycloak
- Authorization Code Flow with PKCE
- Token display with copy to clipboard
- Automatic token storage and retrieval
- Logout functionality
- Tailwind CSS v4 for styling

## Config Service API

The config service is called with 'cfg' and 'type' parameters:
- GET =oidc&type=user

Parameters:
- cfg: Configuration type (e.g., 'oidc')
- type: Client type ('user' or 'service')

Expected response:
```json
{
  "oidcConfigUrl": "string",
  "clientId": "string"
}
```

## Keycloak Configuration

Make sure your Keycloak client is configured with:

1. Client authentication: Public
2. Standard Flow Enabled: ON
3. Valid Redirect URIs: Add your application URL
   - Development: http://localhost:5313/*
   - Production: Your production URL
4. Web Origins: Add your application URL
   - Development: http://localhost:5313

## Tailwind CSS v4

This project uses Tailwind CSS v4 with the new PostCSS plugin architecture:
- Package: @tailwindcss/postcss
- Configuration: tailwind.config.js (simplified for v4)
- PostCSS: postcss.config.js

## Support

For more information, see the documentation in the source files.
