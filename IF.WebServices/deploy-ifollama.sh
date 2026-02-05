#!/bin/bash
set -e

# ============================================================================
# IFOllama.WebService Deployment Script
# Target: intelligence (Fedora)
# Usage:  ./deploy-ifollama.sh [--no-pull] [--no-start]
# ============================================================================

# ----------------------------
# Configuration
# ----------------------------
SERVICE_NAME="ifollama-ws"
SERVER_NAME="IFOllama.WebService"
REPO_ROOT="$HOME/repos/Infoforum/IF.WebServices"
PROJECT_DIR="$REPO_ROOT/IFOllama.WebService"
DEPLOY_ROOT="/srv/Infoforum/WebServices"
DEPLOY_PATH="$DEPLOY_ROOT/$SERVER_NAME"
OLLAMA_PORT=6020

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

# ----------------------------
# Parse arguments
# ----------------------------
DO_PULL=true
DO_START=true

for arg in "$@"; do
    case $arg in
        --no-pull)  DO_PULL=false ;;
        --no-start) DO_START=false ;;
        --help|-h)
            echo "Usage: $0 [--no-pull] [--no-start]"
            echo "  --no-pull   Skip git pull (build from current state)"
            echo "  --no-start  Deploy only, don't start the service"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown argument: $arg${NC}"
            exit 1
            ;;
    esac
done

# Determine the actual user (even when running via sudo)
if [ -n "$SUDO_USER" ]; then
    ACTUAL_USER="$SUDO_USER"
else
    ACTUAL_USER="$USER"
fi

echo "========================================"
echo -e "${BLUE}  IFOllama.WebService Deployment${NC}"
echo "  Target: intelligence"
echo "========================================"
echo "User:          $ACTUAL_USER"
echo "Repo:          $REPO_ROOT"
echo "Project:       $PROJECT_DIR"
echo "Deploy path:   $DEPLOY_PATH"
echo "Service:       $SERVICE_NAME"
echo ""

# ----------------------------
# Sanity checks
# ----------------------------
if [ ! -d "$REPO_ROOT" ]; then
    echo -e "${RED}[ERROR] Repository not found: $REPO_ROOT${NC}"
    echo "Clone the repo first:"
    echo "  mkdir -p ~/repos/dotnet"
    echo "  git clone <repo-url> ~/repos/dotnet/IFOllama"
    exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
    echo -e "${RED}[ERROR] Project directory not found: $PROJECT_DIR${NC}"
    exit 1
fi

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}[ERROR] .NET SDK not found. Install it first.${NC}"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
echo -e "  .NET SDK: ${GREEN}$DOTNET_VERSION${NC}"
echo ""

# ----------------------------
# Phase 1: Git pull
# ----------------------------
if [ "$DO_PULL" = true ]; then
    echo -e "${BLUE}>>> Phase 1: Git Pull${NC}"
    cd "$REPO_ROOT"
    
    echo "    Pulling latest changes..."
    git pull
    echo -e "${GREEN}[OK] Repository updated${NC}"
    echo ""
else
    echo -e "${YELLOW}>>> Phase 1: Git Pull (skipped)${NC}"
    echo ""
fi

# ----------------------------
# Phase 2: Stop service
# ----------------------------
echo -e "${BLUE}>>> Phase 2: Stop Service${NC}"

if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
    echo "    Stopping $SERVICE_NAME..."
    sudo systemctl stop "$SERVICE_NAME"
    echo -e "${GREEN}[OK] Service stopped${NC}"
else
    echo -e "${YELLOW}    Service not running${NC}"
fi
echo ""

# ----------------------------
# Phase 3: Build
# ----------------------------
echo -e "${BLUE}>>> Phase 3: Build${NC}"

cd "$PROJECT_DIR"

echo "    Cleaning..."
if [ "$EUID" -eq 0 ] && [ -n "$SUDO_USER" ]; then
    sudo -u "$SUDO_USER" dotnet clean --configuration Release > /dev/null 2>&1 || true
    echo "    Publishing..."
    sudo -u "$SUDO_USER" dotnet publish --configuration Release --output "./publish" --self-contained false --runtime linux-x64
else
    dotnet clean --configuration Release > /dev/null 2>&1 || true
    echo "    Publishing..."
    dotnet publish --configuration Release --output "./publish" --self-contained false --runtime linux-x64
fi

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Build failed${NC}"
    exit 1
fi

echo -e "${GREEN}[OK] Build successful${NC}"
echo ""

# ----------------------------
# Phase 4: Deploy
# ----------------------------
echo -e "${BLUE}>>> Phase 4: Deploy${NC}"

sudo mkdir -p "$DEPLOY_PATH"

# Backup existing deployment
if [ -d "$DEPLOY_PATH" ] && [ "$(ls -A $DEPLOY_PATH 2>/dev/null)" ]; then
    BACKUP_PATH="${DEPLOY_PATH}.backup.$(date +%Y%m%d_%H%M%S)"
    echo "    Creating backup: $BACKUP_PATH"
    sudo mv "$DEPLOY_PATH" "$BACKUP_PATH"
    sudo mkdir -p "$DEPLOY_PATH"
    
    # Keep only last 3 backups
    BACKUP_COUNT=$(ls -d ${DEPLOY_PATH}.backup.* 2>/dev/null | wc -l)
    if [ "$BACKUP_COUNT" -gt 3 ]; then
        echo "    Cleaning old backups (keeping last 3)..."
        ls -d ${DEPLOY_PATH}.backup.* 2>/dev/null | sort | head -n -3 | xargs sudo rm -rf
    fi
fi

# Copy published files
echo "    Copying files to $DEPLOY_PATH"
sudo cp -r ./publish/* "$DEPLOY_PATH/"
sudo chown -R "$ACTUAL_USER:$ACTUAL_USER" "$DEPLOY_PATH"

# Clean up publish folder
rm -rf ./publish

echo -e "${GREEN}[OK] Files deployed${NC}"
echo ""

# ----------------------------
# Phase 5: Create run script
# ----------------------------
echo -e "${BLUE}>>> Phase 5: Configure${NC}"

sudo tee "$DEPLOY_PATH/run.sh" > /dev/null << 'RUNEOF'
#!/bin/bash
cd "$(dirname "$0")"
exec dotnet IFOllama.WebService.dll "$@"
RUNEOF
sudo chmod +x "$DEPLOY_PATH/run.sh"

# ----------------------------
# Phase 6: Create/update systemd service
# ----------------------------
echo "    Creating systemd service: $SERVICE_NAME"

sudo tee "/etc/systemd/system/$SERVICE_NAME.service" > /dev/null << EOF
[Unit]
Description=IFOllama Web Service (AI Chat with Ollama + MCP)
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

sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE_NAME" 2>/dev/null || true

echo -e "${GREEN}[OK] Service configured${NC}"
echo ""

# ----------------------------
# Phase 7: Start and verify
# ----------------------------
if [ "$DO_START" = true ]; then
    echo -e "${BLUE}>>> Phase 7: Start & Verify${NC}"
    
    echo "    Starting $SERVICE_NAME..."
    if sudo systemctl start "$SERVICE_NAME" 2>/dev/null; then
        sleep 3
        
        if systemctl is-active --quiet "$SERVICE_NAME"; then
            echo -e "${GREEN}[OK] $SERVICE_NAME is running${NC}"
            
            # Health check
            echo "    Checking health on port $OLLAMA_PORT..."
            local_attempts=0
            max_attempts=10
            
            while [ $local_attempts -lt $max_attempts ]; do
                HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:${OLLAMA_PORT}/Health" 2>/dev/null || echo "000")
                
                if [ "$HTTP_CODE" = "200" ]; then
                    echo -e "${GREEN}[OK] Health check passed (HTTP $HTTP_CODE)${NC}"
                    break
                fi
                
                local_attempts=$((local_attempts + 1))
                if [ $local_attempts -lt $max_attempts ]; then
                    sleep 2
                fi
            done
            
            if [ "$HTTP_CODE" != "200" ]; then
                echo -e "${YELLOW}[WARN] Health check returned HTTP $HTTP_CODE - service may still be starting${NC}"
                echo "  Check logs: sudo journalctl -u $SERVICE_NAME -f"
            fi
        else
            echo -e "${RED}[ERROR] $SERVICE_NAME failed to start${NC}"
            echo ""
            echo "Recent logs:"
            sudo journalctl -u "$SERVICE_NAME" -n 20 --no-pager
            exit 1
        fi
    else
        echo -e "${RED}[ERROR] Failed to start $SERVICE_NAME${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}>>> Phase 7: Start (skipped - use: sudo systemctl start $SERVICE_NAME)${NC}"
fi

echo ""

# ----------------------------
# Summary
# ----------------------------
echo "========================================"
echo -e "${GREEN}  Deployment Complete${NC}"
echo "========================================"
echo ""
echo "  Service:     $SERVICE_NAME"
echo "  Deploy path: $DEPLOY_PATH"
echo "  Port:        $OLLAMA_PORT"
echo ""
echo "  Useful commands:"
echo "    sudo journalctl -u $SERVICE_NAME -f       # Follow logs"
echo "    sudo journalctl -u $SERVICE_NAME -n 100   # Recent logs"
echo "    sudo systemctl restart $SERVICE_NAME       # Restart"
echo "    sudo systemctl status $SERVICE_NAME        # Status"
echo ""
echo "  Quick test:"
echo "    curl http://localhost:${OLLAMA_PORT}/Health"
echo "    curl http://localhost:${OLLAMA_PORT}/swagger/index.html"
echo ""