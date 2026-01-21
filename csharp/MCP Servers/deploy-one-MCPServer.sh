#!/bin/bash
set -e

# Usage: ./deploy-one-MCPServer.sh <ServerName> <service-name> <deploy-root>
# Example: ./deploy-one-MCPServer.sh FileSystemMcpServer filesystem-mcp /srv/sfddevelopment/MCPServers

if [ $# -lt 3 ]; then
    echo "Usage: $0 <ServerName> <service-name> <deploy-root>"
    exit 1
fi

SERVER_NAME=$1
SERVICE_NAME=$2
DEPLOY_ROOT=$3
BUILD_CONFIG="Release"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${YELLOW}>>> Deploying $SERVER_NAME...${NC}"

# Check if server directory exists
if [ ! -d "$SERVER_NAME" ]; then
    echo -e "${RED}[ERROR] Directory $SERVER_NAME not found${NC}"
    exit 1
fi

# Stop service if running
if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
    echo "    Stopping service $SERVICE_NAME..."
    sudo systemctl stop "$SERVICE_NAME"
fi

cd "$SERVER_NAME"

# Clean (ignore failures - clean isn't critical)
dotnet clean "$SERVER_NAME.csproj" --configuration $BUILD_CONFIG > /dev/null 2>&1 || true

# Publish
dotnet publish "$SERVER_NAME.csproj" --configuration $BUILD_CONFIG --output "./publish" --self-contained false --runtime linux-x64

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Build failed for $SERVER_NAME${NC}"
    exit 1
fi

DEPLOY_PATH="$DEPLOY_ROOT/$SERVER_NAME"
sudo mkdir -p "$DEPLOY_PATH"

# Backup existing deployment
if [ -d "$DEPLOY_PATH" ] && [ "$(ls -A $DEPLOY_PATH)" ]; then
    BACKUP_PATH="${DEPLOY_PATH}.backup.$(date +%Y%m%d_%H%M%S)"
    sudo mv "$DEPLOY_PATH" "$BACKUP_PATH"
    sudo mkdir -p "$DEPLOY_PATH"
    echo "    Backup created: $BACKUP_PATH"
    
    # Prune old backups: keep all from today, max 2 from previous days
    TODAY=$(date +%Y%m%d)
    ls -d ${DEPLOY_PATH}.backup.* 2>/dev/null | while read BACKUP; do
        BACKUP_DATE=$(basename "$BACKUP" | sed 's/.*\.backup\.\([0-9]\{8\}\).*/\1/')
        if [ "$BACKUP_DATE" != "$TODAY" ]; then
            echo "$BACKUP"
        fi
    done | sort -r | tail -n +3 | while read OLD_BACKUP; do
        sudo rm -rf "$OLD_BACKUP"
        echo "    Removed old backup: $OLD_BACKUP"
    done
fi

# Copy files
sudo cp -r ./publish/* "$DEPLOY_PATH/"
sudo chown -R $USER:$USER "$DEPLOY_PATH"

# Create run script
cat > "$DEPLOY_PATH/run.sh" << EOF
#!/bin/bash
cd "\$(dirname "\$0")"
exec dotnet $SERVER_NAME.dll "\$@"
EOF
chmod +x "$DEPLOY_PATH/run.sh"

# Create/update systemd service
sudo tee "/etc/systemd/system/$SERVICE_NAME.service" > /dev/null << EOF
[Unit]
Description=$SERVER_NAME
After=network.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$DEPLOY_PATH
ExecStart=/usr/bin/dotnet $DEPLOY_PATH/$SERVER_NAME.dll
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal
Environment=BRAVE_API_KEY=$BRAVE_API_KEY
Environment=SQLITE_DB_PATH=/srv/sfddevelopment/data/migration_metadata.db

[Install]
WantedBy=multi-user.target
EOF

# Clean up
rm -rf ./publish
cd - > /dev/null
cd "/home/hugh/repos/dotnet/MCP Servers"

sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE_NAME" 2>/dev/null  
sudo systemctl start "$SERVICE_NAME"

echo -e "${GREEN}[OK] $SERVER_NAME deployed${NC}"