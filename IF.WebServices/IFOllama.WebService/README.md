# IFOllama.WebService

.NET 10.0 web service providing AI chat capabilities with MCP tool integration.

## Features

- **SignalR Streaming**: Real-time chat streaming using SignalR
- **Keycloak Authentication**: JWT-based authentication via Keycloak
- **MCP Tool Integration**: Route requests to multiple MCP servers
- **Conversation Persistence**: File-based conversation storage
- **File Attachments**: Support for uploading and processing files

## Project Structure

```
IFOllama.WebService/
├── IFOllama.Classes/          # Shared models
│   └── Models/
│       ├── ConversationListItem.cs
│       ├── FileAttachment.cs
│       ├── Message.cs
│       └── ToolCategory.cs
├── IFOllama.WebService/       # Main web service
│   ├── Controllers/
│   │   ├── ConversationsController.cs
│   │   ├── DiagController.cs
│   │   └── McpController.cs
│   ├── Data/
│   │   ├── ConversationStore.cs
│   │   └── IConversationStore.cs
│   ├── Hubs/
│   │   └── ChatHub.cs
│   ├── Models/
│   │   ├── McpModels.cs
│   │   └── OllamaModels.cs
│   ├── Services/
│   │   ├── FileStorageService.cs
│   │   ├── McpRouterService.cs
│   │   └── OllamaService.cs
│   └── Program.cs
└── IFOllama.sln
```

## Configuration

### appsettings.json

```json
{
  "Port": 6020,
  "Keycloak": {
    "Authority": "https://longmanrd.net/auth/realms/LongmanRd",
    "Audience": "account"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5:32b"
  },
  "Mcp": {
    "BaseHost": "localhost",
    "Servers": {
      "FileSystem": "6008",
      "Git": "6011"
    }
  }
}
```

## Running Locally

```bash
cd IFOllama.WebService
dotnet run
```

The service will start on `http://localhost:6020`.

## API Endpoints

### Conversations
- `GET /api/conversations?userId={userId}` - List conversations
- `POST /api/conversations?userId={userId}` - Create conversation
- `GET /api/conversations/{id}/messages?userId={userId}` - Get messages
- `POST /api/conversations/{id}/messages?userId={userId}` - Append message
- `DELETE /api/conversations/{id}?userId={userId}` - Delete conversation

### MCP Tools
- `GET /api/mcp/tools` - List all available tools
- `GET /api/mcp/servers` - List configured servers
- `POST /api/mcp/tools/call` - Call a specific tool
- `POST /api/mcp/chat/ollama` - Chat with tool support

### SignalR Hub
- `/hubs/chat` - SignalR hub for streaming chat

## Deployment

```bash
./deploy.sh
```

This will:
1. Build the release version
2. Stop the existing service
3. Copy files to `/opt/sfd/ifollama-webservice`
4. Start the service

## Authentication

The service uses Keycloak for authentication. Users must be in the `IntelligenceUsers` group to access protected endpoints.

Pass the JWT token:
- For REST API: `Authorization: Bearer {token}`
- For SignalR: `?access_token={token}` query parameter
