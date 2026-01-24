# IF WebServices Deployment

Deployment scripts, nginx configurations, and systemd service files for IF WebServices.

## Architecture Overview

```
                    ┌─────────────────────────────────────────────────────────┐
                    │                     nginx (HTTPS)                        │
                    │                   longmanrd.net:443                      │
                    └─────────────────────────────────────────────────────────┘
                                              │
           ┌──────────────────────────────────┼──────────────────────────────────┐
           │                                  │                                  │
           ▼                                  ▼                                  ▼
    ┌─────────────┐                   ┌─────────────┐                    ┌─────────────┐
    │  /config    │                   │  /logger    │                    │  /tokens/*  │
    │             │                   │             │                    │             │
    │ proxy_pass  │                   │ proxy_pass  │                    │   alias     │
    │ :5000       │                   │ :5001       │                    │ /var/www/   │
    └─────────────┘                   └─────────────┘                    └─────────────┘
           │                                  │                                  │
           ▼                                  ▼                                  ▼
    ┌─────────────┐                   ┌─────────────┐                    ┌─────────────┐
    │ConfigWeb    │                   │LoggerWeb    │                    │ React SPA   │
    │Service      │                   │Service      │                    │ (static)    │
    │.NET :5000   │                   │.NET :5001   │                    │             │
    └─────────────┘                   └─────────────┘                    └─────────────┘
           │                                  │
           └──────────────┬───────────────────┘
                          ▼
                   ┌─────────────┐
                   │ PostgreSQL  │
                   │ IF_Config   │
                   │ IF_Log      │
                   └─────────────┘
```

## Directory Structure

```
deployment/
├── scripts/
│   ├── deploy-all.sh              # Master deployment script
│   ├── deploy-config-webservice.sh
│   ├── deploy-logger-webservice.sh
│   └── deploy-tokens-app.sh
├── nginx/
│   ├── config-webservice.inc      # nginx location block for ConfigWebService
│   ├── logger-webservice.inc      # nginx location block for LoggerWebService
│   └── tokens-app.inc             # nginx location block for Tokens App
├── systemd/
│   ├── config-webservice.service  # systemd service for ConfigWebService
│   └── logger-webservice.service  # systemd service for LoggerWebService
└── README.md
```

## Service Ports

| Service          | Port | Path      |
|------------------|------|-----------|
| ConfigWebService | 5000 | /config   |
| LoggerWebService | 5001 | /logger   |
| Tokens App       | N/A  | /tokens/* |

## Quick Start

### Deploy Everything

```bash
# Full deployment with build and restart
sudo ./scripts/deploy-all.sh all --build --restart
```

### Deploy Individual Services

```bash
# ConfigWebService only
sudo PROJECT_DIR=/path/to/ConfigWebService ./scripts/deploy-config-webservice.sh --build --restart

# LoggerWebService only
sudo PROJECT_DIR=/path/to/LoggerWebService ./scripts/deploy-logger-webservice.sh --build --restart

# Tokens App only
sudo PROJECT_DIR=/path/to/display-token-app ./scripts/deploy-tokens-app.sh --build
```

## nginx Configuration

### Main Server Block Setup

Add these includes to your main nginx server block (`/etc/nginx/sites-available/longmanrd.net`):

```nginx
server {
    listen 443 ssl http2;
    server_name longmanrd.net;

    # SSL configuration
    ssl_certificate /etc/letsencrypt/live/longmanrd.net/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/longmanrd.net/privkey.pem;

    # Include service configurations
    include /etc/nginx/conf.d/config-webservice.inc;
    include /etc/nginx/conf.d/logger-webservice.inc;
    include /etc/nginx/conf.d/tokens-app.inc;

    # Other locations...
}
```

### Test and Reload nginx

```bash
sudo nginx -t
sudo systemctl reload nginx
```

## Service Configuration

### ConfigWebService (appsettings.json)

```json
{
  "ConnectionStrings": {
    "LogDatabase": "Host=localhost;Port=5432;Database=IF_Log;Username=...;Password=...",
    "ConfigDatabase": "Host=localhost;Port=5432;Database=IF_Config;Username=...;Password=..."
  },
  "OidcConfig": {
    "Authority": "https://longmanrd.net",
    "Realm": {
      "Name": "LongmanRd",
      "service": {
        "ClientId": "dev-login-svc",
        "ClientSecret": "your-secret-here"
      }
    }
  }
}
```

### LoggerWebService (appsettings.json)

```json
{
  "IF": {
    "ConfigService": "https://longmanrd.net/config",
    "Realm": "LongmanRd",
    "Client": "dev-login"
  }
}
```

### Tokens App

The tokens app is configured via environment variables at build time:

```bash
VITE_IF_CONFIG_SERVICE_URL=/config npm run build
```

Or use the deployment script:

```bash
sudo ./deploy-tokens-app.sh --build --config-service /config
```

## URL Patterns

### Tokens App

The tokens app uses URL-based configuration:

```
https://longmanrd.net/tokens/{realm}/{client}/
```

Examples:
- `https://longmanrd.net/tokens/IfDevelopment_Dev/dev-login/`
- `https://longmanrd.net/tokens/SfdDevelopment_Dev/dev-login/`
- `https://longmanrd.net/tokens/LongmanRd/dev-login/`

### ConfigWebService API

```
# Bootstrap (unauthenticated)
GET /config/Config?cfg=bootstrap&type=service&realm={realm}&client={client}

# Get config (authenticated)
GET /config/Config?cfg={configName}&realm={realm}&client={client}
```

### LoggerWebService API

```
# Get logs (authenticated)
GET /logger/logs?realm={realm}&client={client}

# SignalR hub
/logger/loghub
```

## systemd Commands

```bash
# Start/stop/restart
sudo systemctl start config-webservice
sudo systemctl stop config-webservice
sudo systemctl restart config-webservice

# Enable/disable on boot
sudo systemctl enable config-webservice
sudo systemctl disable config-webservice

# Check status
sudo systemctl status config-webservice

# View logs
sudo journalctl -u config-webservice -f
sudo journalctl -u config-webservice --since "1 hour ago"
```

## Troubleshooting

### Service won't start

```bash
# Check logs
sudo journalctl -u config-webservice -n 50 --no-pager

# Check if port is in use
sudo ss -tlnp | grep 5000

# Test manually
cd /opt/if-webservices/ConfigWebService
sudo -u www-data ./ConfigWebService
```

### nginx 502 Bad Gateway

1. Check if the service is running: `systemctl status config-webservice`
2. Check if the port is correct: `ss -tlnp | grep 5000`
3. Check nginx error logs: `tail -f /var/log/nginx/error.log`

### Tokens App shows "Configuration Required"

The URL must include realm and client:
- ❌ `https://longmanrd.net/tokens/`
- ✅ `https://longmanrd.net/tokens/IfDevelopment_Dev/dev-login/`

### Authentication Errors

1. Verify Keycloak is accessible: `curl https://longmanrd.net/auth/realms/{realm}/.well-known/openid-configuration`
2. Check the client exists in Keycloak
3. Verify redirect URIs are configured in Keycloak

## Security Notes

- Services listen on localhost only (via nginx proxy)
- HTTPS terminates at nginx
- Firewall should block direct access to service ports (5030, 5031)
- Client secrets should be in appsettings.json (not in environment variables for ConfigWebService)
