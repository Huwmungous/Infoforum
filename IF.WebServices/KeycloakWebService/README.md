# Keycloak Web Service

A C# ASP.NET Core web service for managing Keycloak users, groups, and memberships.

## Prerequisites

- .NET 9.0 or later
- A running Keycloak instance
- Service account credentials for Keycloak Admin API

## Setup

1. **Install dependencies:**
   ```bash
   dotnet restore
   ```

2. **Configure Keycloak settings:**
   
   Update `appsettings.json` with your Keycloak details:
   ```json
   {
     "Keycloak": {
       "Url": "https://your-keycloak-server.com",
       "Realm": "your-realm",
       "ClientId": "your-client-id",
       "ClientSecret": "your-client-secret"
     }
   }
   ```

3. **Create a service account in Keycloak:**
   - Navigate to your realm's Clients
   - Create a new client (e.g., "service-account")
   - Enable "Service Accounts Enabled"
   - Go to the Service Account Roles tab
   - Add roles: `manage-users` and `manage-groups` from the `admin-cli` client

## Running

```bash
dotnet run
```

The API will be available at `http://localhost:5000` and Swagger UI at `/swagger`.

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/keycloak/users` | List all users |
| GET | `/api/keycloak/users/{userId}` | Get specific user |
| GET | `/api/keycloak/groups` | List all groups |
| GET | `/api/keycloak/groups/{groupId}/members` | Get group members |
| GET | `/api/keycloak/users/{userId}/groups` | Get user's groups |
| POST | `/api/keycloak/users/{userId}/groups/{groupId}` | Add user to group |
| DELETE | `/api/keycloak/users/{userId}/groups/{groupId}` | Remove user from group |

## Example Requests

### List all users
```bash
curl http://localhost:5000/api/keycloak/users
```

### Add user to group
```bash
curl -X POST http://localhost:5000/api/keycloak/users/user-id/groups/group-id
```

### Remove user from group
```bash
curl -X DELETE http://localhost:5000/api/keycloak/users/user-id/groups/group-id
```

## Publishing

```bash
dotnet publish -c Release -o ./publish
```

## License

MIT
