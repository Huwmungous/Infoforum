# Sample Web Service

A sample ASP.NET Core web service demonstrating IF infrastructure integration with PostgreSQL database access.

## Purpose

This service serves as a **reference implementation** showing how to:

1. Bootstrap using `ServiceFactory` for standardised configuration
2. Integrate with `ConfigWebService` for centralised configuration
3. Use `IFLogger` for remote logging to `LoggerWebService`
4. Access PostgreSQL databases via configuration
5. Implement the repository pattern with DI

## Database

Connects to the **rozebowl** database and queries the `dbo.coach` table.

## API Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/health` | GET | No | Health check |
| `/swagger` | GET | No | API documentation |
| `/api/coach` | GET | Yes | Get all coaches |
| `/api/coach/{id}` | GET | Yes | Get coach by idx |
| `/api/coach/count` | GET | Yes | Get coach count |

## Configuration

All configuration is in `appsettings.json`:

```json
{
  "IF": {
    "ConfigService": "https://longmanrd.net/config",
    "AppDomain": "Infoforum"
  }
}
```

The database connection (`rozebowl`) is fetched from ConfigWebService at startup.

## Running

```bash
dotnet run
```

Then open: `http://localhost:{port}/swagger`
