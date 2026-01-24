# Sample Web Service

A sample ASP.NET Core web service demonstrating SfD infrastructure integration with encapsulated data access.

## Architecture

This service demonstrates clean separation of concerns:

1. **Program.cs** - Bootstrap and configuration only
2. **AccountRepository** - Encapsulates all data access logic including:
   - Fetching `RequiresRelay` from ConfigService
   - Direct Firebird connection or Relay mode selection
   - Connection management and disposal

## Configuration

All configuration is in `appsettings.json`:

```json
{
  "IF": {
    "ConfigService": "https://longmanrd.net/config",
    "Realm": "SfdDevelopment_Dev",
    "Client": "dev-login"
  }
}
```

**No environment variables required!** The client secret is fetched securely from the ConfigWebService bootstrap endpoint.

## How Data Access Works

The `AccountRepository` handles everything internally:

1. On first database operation, it authenticates with ConfigService
2. Fetches `relay` config to check `RequiresRelay` flag
3. If `RequiresRelay: false` → Fetches `firebirddb` config and connects directly
4. If `RequiresRelay: true` → Uses relay connection (not yet implemented)

This means **Program.cs doesn't need to know about data access configuration**.

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/account` | GET | Get all accounts |
| `/api/account/{id}` | GET | Get account by ID |
| `/api/account` | POST | Create new account |
| `/api/account/{id}` | PUT | Update account |
| `/api/account/{id}` | DELETE | Delete account |
| `/health` | GET | Health check |

## Running

```bash
# Just run - no environment variables needed!
dotnet run
```

Or open: `http://localhost:{port}/swagger`
