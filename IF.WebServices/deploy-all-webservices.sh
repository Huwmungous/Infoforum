#!/bin/bash
set -e

# Usage:    ./deploy-all-webservices.sh

DEPLOY_ROOT="/srv/Infoforum/WebServices"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "======================================"
echo "  Web Services Deployment"
echo "======================================"
echo "User:          $USER"
echo "Deploy Root:   $DEPLOY_ROOT"
echo ""

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

# Check if running as root (needed for systemctl)
if [[ $EUID -ne 0 ]] && ! sudo -n true 2>/dev/null; then
    echo -e "${YELLOW}[WARNING] This script requires sudo access for systemctl commands${NC}"
    echo "You may be prompted for your sudo password"
    echo ""
fi

# Ports are defined by PortResolver in the services themselves
CONFIG_PORT=5000
LOGGER_PORT=5001

# Wait for LoggerWebService to be healthy
wait_for_logger_service() {
    local max_attempts=30
    local attempt=1
    local wait_seconds=2
    local url="http://localhost:${LOGGER_PORT}/health"
    
    echo -e "${YELLOW}Waiting for LoggerWebService to be ready on port ${LOGGER_PORT}...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null | grep -q "200"; then
            echo -e "${GREEN}[OK] LoggerWebService is ready${NC}"
            return 0
        fi
        
        echo -e "  Attempt $attempt/$max_attempts - waiting ${wait_seconds}s..."
        sleep $wait_seconds
        attempt=$((attempt + 1))
    done
    
    echo -e "${RED}[ERROR] LoggerWebService did not become ready within $((max_attempts * wait_seconds)) seconds${NC}"
    return 1
}

# Wait for ConfigWebService to be healthy
wait_for_config_service() {
    local max_attempts=30
    local attempt=1
    local wait_seconds=2
    local url="http://localhost:${CONFIG_PORT}/Config?cfg=bootstrap&type=service&realm=SfdDevelopment_Dev&client=dev-login"
    
    echo -e "${YELLOW}Waiting for ConfigWebService to be ready on port ${CONFIG_PORT}...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null | grep -q "200"; then
            echo -e "${GREEN}[OK] ConfigWebService is ready${NC}"
            return 0
        fi
        
        echo -e "  Attempt $attempt/$max_attempts - waiting ${wait_seconds}s..."
        sleep $wait_seconds
        attempt=$((attempt + 1))
    done
    
    echo -e "${RED}[ERROR] ConfigWebService did not become ready within $((max_attempts * wait_seconds)) seconds${NC}"
    return 1
}

# ConfigWebService must be deployed and started first (it's self-contained)
CONFIG_SERVICE="ConfigWebService"
CONFIG_SYSTEMD="config-ws"

# THEN LoggerWebService (depends on ConfigWebService for bootstrap)
LOGGER_SERVICE="LoggerWebService"
LOGGER_SYSTEMD="logger-ws"

# Other services that depend on ConfigWebService
DEPENDENT_SERVICES=(
    "SampleWebService" 
)
declare -A SERVERS=(
    ["SampleWebService"]="sample-ws"
)

# Ensure deploy root exists
sudo mkdir -p "$DEPLOY_ROOT"

# Make deploy-one script executable
chmod +x "$SCRIPT_DIR/deploy-one-webservice.sh"

# Arrays to track results
FAILED_SERVICES=()
DEPLOYED_SERVICES=()
SKIPPED_SERVICES=()

# ============================================================================
# PHASE 1: Deploy and start ConfigWebService (self-contained, no dependencies)
# ============================================================================
echo "======================================"
echo -e "${BLUE}  Phase 1: ConfigWebService${NC}"
echo "$SCRIPT_DIR/$CONFIG_SERVICE"
echo "======================================"
echo ""

SERVER_PATH="$SCRIPT_DIR/$CONFIG_SERVICE"

if [ -d "$SERVER_PATH" ]; then
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    if "$SCRIPT_DIR/deploy-one-webservice.sh" "$SERVER_PATH" "$CONFIG_SYSTEMD" "$DEPLOY_ROOT"; then
        DEPLOYED_SERVICES+=("$CONFIG_SERVICE:$CONFIG_SYSTEMD")
        echo -e "${GREEN}[SUCCESS] $CONFIG_SERVICE deployed${NC}"
    else
        FAILED_SERVICES+=("$CONFIG_SERVICE:$CONFIG_SYSTEMD")
        echo -e "${RED}[FAILED] $CONFIG_SERVICE deployment failed${NC}"
        echo -e "${RED}Cannot continue without ConfigWebService${NC}"
        exit 1
    fi
else
    echo -e "${RED}[ERROR] $CONFIG_SERVICE directory not found at $SERVER_PATH${NC}"
    exit 1
fi

# Reload systemd and start ConfigWebService
echo ""
echo "Reloading systemd daemon..."
sudo systemctl daemon-reload

echo -e "${YELLOW}Starting $CONFIG_SYSTEMD ($CONFIG_SERVICE)...${NC}"
sudo systemctl enable "$CONFIG_SYSTEMD" 2>/dev/null || true

if sudo systemctl start "$CONFIG_SYSTEMD" 2>/dev/null; then
    sleep 2
    if systemctl is-active --quiet "$CONFIG_SYSTEMD"; then
        echo -e "${GREEN}[OK] $CONFIG_SYSTEMD is running${NC}"
    else
        echo -e "${RED}[ERROR] $CONFIG_SYSTEMD failed to start${NC}"
        echo "Check logs with: sudo journalctl -u $CONFIG_SYSTEMD -n 50"
        exit 1
    fi
else
    echo -e "${RED}[ERROR] Failed to start $CONFIG_SYSTEMD${NC}"
    exit 1
fi

# Wait for ConfigWebService to be fully ready
if ! wait_for_config_service; then
    echo -e "${RED}[ERROR] ConfigWebService is not responding. Check logs:${NC}"
    echo "  sudo journalctl -u $CONFIG_SYSTEMD -n 50"
    exit 1
fi

echo ""

# ============================================================================
# PHASE 2: Deploy and start LoggerWebService (depends on ConfigWebService)
# ============================================================================
echo "======================================"
echo -e "${BLUE}  Phase 2: LoggerWebService${NC}"
echo "======================================"
echo ""

SERVER_PATH="$SCRIPT_DIR/$LOGGER_SERVICE"

if [ -d "$SERVER_PATH" ]; then
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    if "$SCRIPT_DIR/deploy-one-webservice.sh" "$SERVER_PATH" "$LOGGER_SYSTEMD" "$DEPLOY_ROOT"; then
        DEPLOYED_SERVICES+=("$LOGGER_SERVICE:$LOGGER_SYSTEMD")
        echo -e "${GREEN}[SUCCESS] $LOGGER_SERVICE deployed${NC}"
    else
        FAILED_SERVICES+=("$LOGGER_SERVICE:$LOGGER_SYSTEMD")
        echo -e "${RED}[FAILED] $LOGGER_SERVICE deployment failed${NC}"
        echo -e "${RED}Cannot continue without LoggerWebService${NC}"
        exit 1
    fi
else
    echo -e "${RED}[ERROR] $LOGGER_SERVICE directory not found at $SERVER_PATH${NC}"
    exit 1
fi

# Reload systemd and start LoggerWebService
echo ""
echo "Reloading systemd daemon..."
sudo systemctl daemon-reload

echo -e "${YELLOW}Starting $LOGGER_SYSTEMD ($LOGGER_SERVICE)...${NC}"
sudo systemctl enable "$LOGGER_SYSTEMD" 2>/dev/null || true

if sudo systemctl start "$LOGGER_SYSTEMD" 2>/dev/null; then
    sleep 2
    if systemctl is-active --quiet "$LOGGER_SYSTEMD"; then
        echo -e "${GREEN}[OK] $LOGGER_SYSTEMD is running${NC}"
    else
        echo -e "${RED}[ERROR] $LOGGER_SYSTEMD failed to start${NC}"
        echo "Check logs with: sudo journalctl -u $LOGGER_SYSTEMD -n 50"
        exit 1
    fi
else
    echo -e "${RED}[ERROR] Failed to start $LOGGER_SYSTEMD${NC}"
    exit 1
fi

# Wait for LoggerWebService to be fully ready
if ! wait_for_logger_service; then
    echo -e "${RED}[ERROR] LoggerWebService is not responding. Check logs:${NC}"
    echo "  sudo journalctl -u $LOGGER_SYSTEMD -n 50"
    exit 1
fi

echo ""

# ============================================================================
# PHASE 3: Deploy dependent services
# ============================================================================
echo "======================================"
echo -e "${BLUE}  Phase 3: Dependent Services${NC}"
echo "======================================"
echo ""

for SERVER_NAME in "${DEPENDENT_SERVICES[@]}"; do
    SERVICE_NAME="${SERVERS[$SERVER_NAME]}"
    SERVER_PATH="$SCRIPT_DIR/$SERVER_NAME"
    
    if [ -d "$SERVER_PATH" ]; then
        echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
        if "$SCRIPT_DIR/deploy-one-webservice.sh" "$SERVER_PATH" "$SERVICE_NAME" "$DEPLOY_ROOT"; then
            DEPLOYED_SERVICES+=("$SERVER_NAME:$SERVICE_NAME")
            echo -e "${GREEN}[SUCCESS] $SERVER_NAME deployed${NC}"
        else
            FAILED_SERVICES+=("$SERVER_NAME:$SERVICE_NAME")
            echo -e "${RED}[FAILED] $SERVER_NAME deployment failed${NC}"
        fi
        echo ""
    else
        echo -e "${YELLOW}[SKIP] $SERVER_NAME - directory not found at $SERVER_PATH${NC}"
        SKIPPED_SERVICES+=("$SERVER_NAME")
        echo ""
    fi
done

# ============================================================================
# PHASE 4: Start dependent services
# ============================================================================
echo "======================================"
echo -e "${BLUE}  Phase 4: Starting Dependent Services${NC}"
echo "======================================"
echo ""

# Reload systemd to pick up any changes
sudo systemctl daemon-reload

# Start all dependent services
for SERVER_NAME in "${DEPENDENT_SERVICES[@]}"; do
    SERVICE_NAME="${SERVERS[$SERVER_NAME]}"
    
    # Check if this service was deployed
    if [[ ! " ${DEPLOYED_SERVICES[*]} " =~ " ${SERVER_NAME}:${SERVICE_NAME} " ]]; then
        continue
    fi
    
    echo -e "${YELLOW}Starting $SERVICE_NAME ($SERVER_NAME)...${NC}"
    
    # Enable service
    sudo systemctl enable "$SERVICE_NAME" 2>/dev/null || true
    
    # Start service
    if sudo systemctl start "$SERVICE_NAME" 2>/dev/null; then
        # Give it a moment to start
        sleep 2
        
        # Check status
        if systemctl is-active --quiet "$SERVICE_NAME"; then
            echo -e "${GREEN}[OK] $SERVICE_NAME is running${NC}"
        else
            echo -e "${RED}[ERROR] $SERVICE_NAME failed to start${NC}"
            echo "Check logs with: sudo journalctl -u $SERVICE_NAME -n 50"
            FAILED_SERVICES+=("$SERVER_NAME:$SERVICE_NAME")
        fi
    else
        echo -e "${RED}[ERROR] Failed to start $SERVICE_NAME${NC}"
        FAILED_SERVICES+=("$SERVER_NAME:$SERVICE_NAME")
    fi
    echo ""
done

# ============================================================================
# Summary
# ============================================================================
echo ""
echo "======================================"
echo -e "${GREEN}  Deployment Summary${NC}"
echo "======================================"
echo ""
echo "Deployed to: $DEPLOY_ROOT"
echo "ConfigService Port: $CONFIG_PORT"
echo "LoggerService Port: $LOGGER_PORT"
echo ""

# Show deployment results
if [ ${#DEPLOYED_SERVICES[@]} -gt 0 ]; then
    echo -e "${GREEN}Successfully deployed:${NC}"
    for SERVICE_INFO in "${DEPLOYED_SERVICES[@]}"; do
        IFS=':' read -r SERVER_NAME SERVICE_NAME <<< "$SERVICE_INFO"
        STATUS=$(systemctl is-active "$SERVICE_NAME" 2>/dev/null || echo "inactive")
        if [ "$STATUS" = "active" ]; then
            echo -e "  ${GREEN}✔${NC} $SERVER_NAME ($SERVICE_NAME) - ${GREEN}running${NC}"
        else
            echo -e "  ${YELLOW}✔${NC} $SERVER_NAME ($SERVICE_NAME) - ${YELLOW}deployed but not running${NC}"
        fi
    done
    echo ""
fi

if [ ${#SKIPPED_SERVICES[@]} -gt 0 ]; then
    echo -e "${YELLOW}Skipped (not found):${NC}"
    for SERVER in "${SKIPPED_SERVICES[@]}"; do
        echo -e "  ${YELLOW}○${NC} $SERVER"
    done
    echo ""
fi

if [ ${#FAILED_SERVICES[@]} -gt 0 ]; then
    echo -e "${RED}Failed deployments/starts:${NC}"
    for SERVICE_INFO in "${FAILED_SERVICES[@]}"; do
        IFS=':' read -r SERVER_NAME SERVICE_NAME <<< "$SERVICE_INFO"
        echo -e "  ${RED}✗${NC} $SERVER_NAME ($SERVICE_NAME)"
    done
    echo ""
fi

echo "======================================"
echo "  Service Status"
echo "======================================"
echo ""

# Include Config and Logger in the status display
declare -A ALL_SERVERS=(
    ["ConfigWebService"]="config-ws"
    ["LoggerWebService"]="logger-ws"
    ["SampleWebService"]="sample-ws"
)

for SERVER_NAME in "ConfigWebService" "LoggerWebService" "${DEPENDENT_SERVICES[@]}"; do
    SERVICE_NAME="${ALL_SERVERS[$SERVER_NAME]}"
    
    # Check if service exists
    if systemctl list-unit-files | grep -q "^$SERVICE_NAME.service"; then
        STATUS=$(systemctl is-active "$SERVICE_NAME" 2>/dev/null || echo "inactive")
        
        if [ "$STATUS" = "active" ]; then
            echo -e "  ${GREEN}●${NC} $SERVICE_NAME - ${GREEN}active${NC}"
        elif [ "$STATUS" = "inactive" ]; then
            echo -e "  ${RED}○${NC} $SERVICE_NAME - ${RED}inactive${NC}"
        else
            echo -e "  ${YELLOW}○${NC} $SERVICE_NAME - ${YELLOW}$STATUS${NC}"
        fi
    else
        echo -e "  ${YELLOW}○${NC} $SERVICE_NAME - ${YELLOW}not installed${NC}"
    fi
done

echo ""
echo "======================================"
echo "  Useful Commands"
echo "======================================"
echo ""
echo "View logs:"
echo "  sudo journalctl -u <service-name> -f"
echo "  sudo journalctl -u <service-name> -n 100"
echo ""
echo "Manage services:"
echo "  sudo systemctl status <service-name>"
echo "  sudo systemctl restart <service-name>"
echo "  sudo systemctl stop <service-name>"
echo "  sudo systemctl start <service-name>"
echo ""

# Exit with error if any deployments failed
if [ ${#FAILED_SERVICES[@]} -gt 0 ]; then
    echo -e "${RED}Deployment completed with errors${NC}"
    exit 1
else
    echo -e "${GREEN}All deployments successful!${NC}"
    exit 0
fi