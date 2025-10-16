#!/bin/bash
set -e

DEPLOY_ROOT="/srv/sfddevelopment/MCPServers"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "======================================"
echo "  MCP Servers Deployment"
echo "======================================"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

# List of all servers and their systemd service names (alphabetically sorted)
declare -A SERVERS=(
    ["BraveSearchMcpServer"]="bravesearch-mcp"
    ["CodeAnalysisMcpServer"]="codeanalysis-mcp"
    ["CodeFormatterMcpServer"]="codeformatter-mcp"
    ["ConfigManagementMcpServer"]="configmanagement-mcp"
    ["DatabaseCompareMcpServer"]="databasecompare-mcp"
    ["DocumentationMcpServer"]="documentation-mcp"
    ["DotNetBuildMcpServer"]="dotnetbuild-mcp"
    ["FileSystemMcpServer"]="filesystem-mcp"
    ["FileTransferMcpServer"]="filetransfer-mcp"
    ["FirebirdMcpServer"]="firebird-mcp"
    ["GitMcpServer"]="git-mcp"
    ["PlaywrightMcpServer"]="playwright-mcp"
    ["SqlGeneratorMcpServer"]="sqlgenerator-mcp"
    ["SqliteMcpServer"]="sqlite-mcp"
    ["TestGeneratorMcpServer"]="testgenerator-mcp"
    ["UiComponentConverterMcpServer"]="uicomponentconverter-mcp"
)

# Ensure deploy root exists
sudo mkdir -p "$DEPLOY_ROOT"
sudo chown -R $USER:$USER "$DEPLOY_ROOT"

# Make deploy-one script executable
chmod +x "$SCRIPT_DIR/deploy-one-MCPServer.sh"

# Deploy all servers (in alphabetical order)
FAILED_SERVERS=()
for SERVER_NAME in "BraveSearchMcpServer" "CodeAnalysisMcpServer" "CodeFormatterMcpServer" "ConfigManagementMcpServer" "DatabaseCompareMcpServer" "DocumentationMcpServer" "DotNetBuildMcpServer" "FileSystemMcpServer" "FileTransferMcpServer" "FirebirdMcpServer" "GitMcpServer" "PlaywrightMcpServer" "SqlGeneratorMcpServer" "SqliteMcpServer" "TestGeneratorMcpServer" "UiComponentConverterMcpServer"; do
    SERVICE_NAME="${SERVERS[$SERVER_NAME]}"
    
    if [ -d "$SCRIPT_DIR/$SERVER_NAME" ]; then
        if "$SCRIPT_DIR/deploy-one-MCPServer.sh" "$SERVER_NAME" "$SERVICE_NAME" "$DEPLOY_ROOT"; then
            echo ""
        else
            FAILED_SERVERS+=("$SERVER_NAME")
            echo -e "${RED}[FAILED] $SERVER_NAME${NC}"
            echo ""
        fi
    else
        echo -e "${YELLOW}[SKIP] $SERVER_NAME directory not found${NC}"
        echo ""
    fi
done

echo "======================================"
echo -e "${BLUE}  Reloading systemd and starting services${NC}"
echo "======================================"
echo ""

# Reload systemd
sudo systemctl daemon-reload

# Enable and start all services (in alphabetical order)
for SERVER_NAME in "BraveSearchMcpServer" "CodeAnalysisMcpServer" "CodeFormatterMcpServer" "ConfigManagementMcpServer" "DatabaseCompareMcpServer" "DocumentationMcpServer" "DotNetBuildMcpServer" "FileSystemMcpServer" "FileTransferMcpServer" "FirebirdMcpServer" "GitMcpServer" "PlaywrightMcpServer" "SqlGeneratorMcpServer" "SqliteMcpServer" "TestGeneratorMcpServer" "UiComponentConverterMcpServer"; do
    SERVICE_NAME="${SERVERS[$SERVER_NAME]}"
    
    # Skip if deployment failed
    if [[ " ${FAILED_SERVERS[@]} " =~ " ${SERVER_NAME} " ]]; then
        continue
    fi
    
    echo -e "${YELLOW}Starting $SERVICE_NAME...${NC}"
    
    # Enable service
    sudo systemctl enable "$SERVICE_NAME" 2>/dev/null
    
    # Start service
    sudo systemctl start "$SERVICE_NAME"
    
    # Check status
    if systemctl is-active --quiet "$SERVICE_NAME"; then
        echo -e "${GREEN}[OK] $SERVICE_NAME is running${NC}"
    else
        echo -e "${RED}[ERROR] $SERVICE_NAME failed to start${NC}"
        echo "Check logs with: sudo journalctl -u $SERVICE_NAME -n 50"
    fi
done

echo ""
echo "======================================"
echo -e "${GREEN}  Deployment Complete!${NC}"
echo "======================================"
echo ""
echo "Servers deployed to: $DEPLOY_ROOT"
echo ""

if [ ${#FAILED_SERVERS[@]} -gt 0 ]; then
    echo -e "${RED}Failed deployments:${NC}"
    for SERVER in "${FAILED_SERVERS[@]}"; do
        echo -e "  ${RED}✗${NC} $SERVER"
    done
    echo ""
fi

echo "Services status (alphabetically):"
for SERVICE_NAME in "bravesearch-mcp" "codeanalysis-mcp" "codeformatter-mcp" "configmanagement-mcp" "databasecompare-mcp" "documentation-mcp" "dotnetbuild-mcp" "filesystem-mcp" "filetransfer-mcp" "firebird-mcp" "git-mcp" "playwright-mcp" "sqlgenerator-mcp" "sqlite-mcp" "testgenerator-mcp" "uicomponentconverter-mcp"; do
    STATUS=$(systemctl is-active "$SERVICE_NAME" 2>/dev/null || echo "inactive")
    if [ "$STATUS" = "active" ]; then
        echo -e "  ${GREEN}●${NC} $SERVICE_NAME"
    else
        echo -e "  ${RED}○${NC} $SERVICE_NAME"
    fi
done

echo ""
echo "Commands:"
echo "  View logs:        sudo journalctl -u <service-name> -f"
echo "  Restart service:  sudo systemctl restart <service-name>"
echo "  Stop service:     sudo systemctl stop <service-name>"
echo "  Service status:   sudo systemctl status <service-name>"
echo ""
echo "Deploy single server:"
echo "  ./deploy-one-MCPServer.sh <ServerName> <service-name> $DEPLOY_ROOT"
echo ""
echo "All services :"
echo "  - bravesearch-mcp"
echo "  - codeanalysis-mcp"
echo "  - codeformatter-mcp"
echo "  - configmanagement-mcp"
echo "  - databasecompare-mcp"
echo "  - documentation-mcp"
echo "  - dotnetbuild-mcp"
echo "  - filesystem-mcp"
echo "  - filetransfer-mcp"
echo "  - firebird-mcp"
echo "  - git-mcp"
echo "  - playwright-mcp"
echo "  - sqlgenerator-mcp"
echo "  - sqlite-mcp"
echo "  - testgenerator-mcp"
echo "  - uicomponentconverter-mcp"
echo ""
echo "Total: 16 MCP Servers"