#!/bin/bash
set -e

# Deploy this MCP server using the shared helper one level up.
# Usage: ./deploy.sh [extra dotnet args...]

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DEPLOY_ROOT="/srv/sfddevelopment/MCPServers"

exec "$SCRIPT_DIR/../deploy-one-MCPServer.sh" "PlaywrightMcpServer" "playwright-mcp" "$DEPLOY_ROOT" "$@"
