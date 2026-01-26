#!/bin/bash
set -e

# Usage: ./deploy-one-webservice.sh <ServerPath> <service-name> <deploy-root>
# Example: ./deploy-one-webservice.sh /path/to/ConfigWebService config-ws /srv/Infoforum/WebServices

if [ $# -lt 3 ]; then
    echo "Usage: $0 <ServerPath> <service-name> <deploy-root>"
    exit 1
fi

SERVER_PATH=$1
SERVICE_NAME=$2
DEPLOY_ROOT=$3

# Extract just the server name from the path
SERVER_NAME=$(basename "$SERVER_PATH")

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Determine the actual user (even when running via sudo)
if [ -n "$SUDO_USER" ]; then
    ACTUAL_USER="$SUDO_USER"
else
    ACTUAL_USER="$USER"
fi

echo -e "${YELLOW}>>> Deploying $SERVER_NAME...${NC}"

# Check if server directory exists
if [ ! -d "$SERVER_PATH" ]; then
    echo -e "${RED}[ERROR] Directory $SERVER_PATH not found${NC}"
    exit 1
fi

# Stop service if running
if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
    echo "    Stopping service $SERVICE_NAME..."
    sudo systemctl stop "$SERVICE_NAME"
fi

# Navigate to server directory
cd "$SERVER_PATH"

# Clean and publish (run as original user if invoked via sudo to avoid permission issues)
echo "    Building $SERVER_NAME..."
if [ "$EUID" -eq 0 ] && [ -n "$SUDO_USER" ]; then
    sudo -u "$SUDO_USER" dotnet clean --configuration Release > /dev/null 2>&1 || true
    sudo -u "$SUDO_USER" dotnet publish --configuration Release --output "./publish" --self-contained false --runtime linux-x64
else
    dotnet clean --configuration Release > /dev/null 2>&1 || true
    dotnet publish --configuration Release --output "./publish" --self-contained false --runtime linux-x64
fi

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Build failed for $SERVER_NAME${NC}"
    cd - > /dev/null
    exit 1
fi

echo -e "${GREEN}[OK] $SERVER_NAME cleaned and built${NC}"

DEPLOY_PATH="$DEPLOY_ROOT/$SERVER_NAME"
sudo mkdir -p "$DEPLOY_PATH"

# Backup existing deployment
if [ -d "$DEPLOY_PATH" ] && [ "$(ls -A $DEPLOY_PATH 2>/dev/null)" ]; then
    BACKUP_PATH="${DEPLOY_PATH}.backup.$(date +%Y%m%d_%H%M%S)"
    echo "    Creating backup: $BACKUP_PATH"
    sudo mv "$DEPLOY_PATH" "$BACKUP_PATH"
    sudo mkdir -p "$DEPLOY_PATH"
fi

# Copy files
echo "    Copying files to $DEPLOY_PATH"
sudo cp -r ./publish/* "$DEPLOY_PATH/"
sudo chown -R "$ACTUAL_USER:$ACTUAL_USER" "$DEPLOY_PATH"

# Create run script
sudo tee "$DEPLOY_PATH/run.sh" > /dev/null << EOF
#!/bin/bash
cd "\$(dirname "\$0")"
exec dotnet $SERVER_NAME.dll "\$@"
EOF
sudo chmod +x "$DEPLOY_PATH/run.sh"

# Create/update systemd service
echo "    Creating systemd service: $SERVICE_NAME"
sudo tee "/etc/systemd/system/$SERVICE_NAME.service" > /dev/null << EOF
[Unit]
Description=$SERVER_NAME Web Service
After=network.target
Documentation=https://longmanrd.net

[Service]
Type=simple
User=$ACTUAL_USER
WorkingDirectory=$DEPLOY_PATH
ExecStart=/usr/bin/dotnet $DEPLOY_PATH/$SERVER_NAME.dll

# Restart configuration
Restart=always
RestartSec=10
KillMode=mixed
KillSignal=SIGINT
TimeoutStopSec=30

# Security
NoNewPrivileges=true
PrivateTmp=true

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=$SERVICE_NAME

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
EnvironmentFile=-/etc/sysconfig/if-secrets

# Resource limits
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
EOF

# Clean up publish folder
rm -rf ./publish
cd - > /dev/null

echo -e "${GREEN}[OK] $SERVER_NAME deployed to $DEPLOY_PATH${NC}"
echo ""