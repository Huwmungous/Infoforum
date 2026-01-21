# IFOllama.React

React frontend for IFOllama AI chat service with SignalR streaming and MCP tool integration.

## Features

- **SignalR Streaming**: Real-time token streaming for responsive chat
- **Keycloak OIDC**: Secure authentication via Keycloak
- **MCP Tool Selection**: Enable/disable tool categories
- **Thinking Display**: Show/hide model's thinking process
- **Markdown Rendering**: Full markdown support with syntax highlighting
- **IF Brand Styling**: Consistent Infoforum colour scheme

## Project Structure

```
IFOllama.React/
├── public/
│   └── IF-Logo.png
├── src/
│   ├── auth/
│   │   ├── authConfig.ts
│   │   └── AuthCallback.tsx
│   ├── components/
│   │   ├── Chat.tsx
│   │   ├── ChatMessage.tsx
│   │   ├── ConversationList.tsx
│   │   ├── ThinkingProgress.tsx
│   │   └── ToolSelector.tsx
│   ├── hooks/
│   │   └── useChat.ts
│   ├── services/
│   │   ├── apiService.ts
│   │   └── chatService.ts
│   ├── styles/
│   │   └── global.scss
│   ├── types/
│   │   └── index.ts
│   ├── App.tsx
│   └── main.tsx
├── package.json
├── tsconfig.json
└── vite.config.ts
```

## Getting Started

### Install Dependencies

```bash
npm install
```

### Development

```bash
npm run dev
```

This starts the dev server on `http://localhost:3000` with hot reload.

### Build

```bash
npm run build
```

Output is in the `dist/` directory.

## Configuration

### Environment Variables

Create a `.env` file based on `.env.example`:

```env
# API URL (leave empty to use proxy in development)
VITE_API_URL=

# Keycloak Configuration
VITE_KEYCLOAK_REALM=LongmanRd
VITE_KEYCLOAK_CLIENT_ID=ifollama-react
```

### Vite Proxy

In development, API requests are proxied to the backend:

```typescript
// vite.config.ts
proxy: {
  '/api': 'http://localhost:6020',
  '/hubs': { target: 'http://localhost:6020', ws: true }
}
```

## Components

### Chat
Main chat interface with message history, input, and tool selection.

### ChatMessage
Renders individual messages with markdown, syntax highlighting, and thinking toggle.

### ThinkingProgress
Full-screen loading overlay shown during streaming.

### ToolSelector
Modal for selecting which MCP tool servers to enable.

### ConversationList
Sidebar for managing conversation history.

## Styling

Uses the IF brand colour scheme:

```scss
--if-light-colour: #1d97a6;   // Teal
--if-medium-colour: #214c8c;  // Blue
--if-dark-colour: #0b2144;    // Dark blue
--if-hl-light-colour: #faa236; // Orange highlight
```

## Authentication

Uses `react-oidc-context` for Keycloak integration:

1. User clicks "Sign In"
2. Redirect to Keycloak login
3. Callback receives tokens
4. Token automatically attached to API requests

## Deployment

```bash
./deploy.sh
```

This will:
1. Build the production version
2. Copy to deployment path
3. Configure nginx if needed

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+
