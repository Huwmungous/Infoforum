#!/bin/bash
# deploy-logger-webservice.sh
# Deploys LoggerWebService to the server
#
# Usage: ./deploy-logger-webservice.sh [--build] [--restart]
#   --build    Build the project before deploying
#   --restart  Restart the service after deploying

set -e

# Configuration
SERVICE_NAME="logger-webservice"
SERVICE_DISPLAY_NAME="LoggerWebService"
PROJECT_DIR="${PROJECT_DIR:-/path/to/LoggerWebService}"
DEPLOY_DIR="/opt/if-webservices/LoggerWebService"
SYSTEMD_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
NGINX_INC="/etc/nginx/conf.d/${SERVICE_NAME}.inc"
BUILD_CONFIG="${BUILD_CONFIG:-Release}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Parse arguments
DO_BUILD=false
DO_RESTART=false
for arg in "$@"; do
    case $arg in
        --build) DO_BUILD=true ;;
        --restart) DO_RESTART=true ;;
        --help) 
            echo "Usage: $0 [--build] [--restart]"
            echo "  --build    Build the project before deploying"
            echo "  --restart  Restart the service after deploying"
            exit 0
            ;;
    esac
done

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    log_error "Please run as root (sudo)"
    exit 1
fi

# Build if requested
if [ "$DO_BUILD" = true ]; then
    log_info "Building ${SERVICE_DISPLAY_NAME}..."
    if [ ! -d "$PROJECT_DIR" ]; then
        log_error "Project directory not found: $PROJECT_DIR"
        log_info "Set PROJECT_DIR environment variable to the correct path"
        exit 1
    fi
    cd "$PROJECT_DIR"
    dotnet publish -c "$BUILD_CONFIG" -o "${PROJECT_DIR}/publish"
    PROJECT_DIR="${PROJECT_DIR}/publish"
fi

# Create deployment directory
log_info "Creating deployment directory..."
mkdir -p "$DEPLOY_DIR"

# Stop service if running
if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
    log_info "Stopping ${SERVICE_DISPLAY_NAME} service..."
    systemctl stop "$SERVICE_NAME"
fi

# Copy files
log_info "Deploying files to ${DEPLOY_DIR}..."
if [ -d "${PROJECT_DIR}/publish" ]; then
    cp -r "${PROJECT_DIR}/publish/"* "$DEPLOY_DIR/"
elif [ -f "${PROJECT_DIR}/${SERVICE_DISPLAY_NAME}" ] || [ -f "${PROJECT_DIR}/${SERVICE_DISPLAY_NAME}.dll" ]; then
    cp -r "${PROJECT_DIR}/"* "$DEPLOY_DIR/"
else
    log_error "Cannot find built files in ${PROJECT_DIR}"
    log_info "Either run with --build or ensure publish output exists"
    exit 1
fi

# Set permissions
log_info "Setting permissions..."
chown -R www-data:www-data "$DEPLOY_DIR"
chmod +x "${DEPLOY_DIR}/${SERVICE_DISPLAY_NAME}" 2>/dev/null || true

# Install systemd service if not exists
if [ ! -f "$SYSTEMD_FILE" ]; then
    log_info "Installing systemd service..."
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    if [ -f "${SCRIPT_DIR}/../systemd/${SERVICE_NAME}.service" ]; then
        cp "${SCRIPT_DIR}/../systemd/${SERVICE_NAME}.service" "$SYSTEMD_FILE"
        systemctl daemon-reload
        systemctl enable "$SERVICE_NAME"
    else
        log_warn "Systemd service file not found, skipping..."
    fi
fi

# Install nginx config if not exists
if [ ! -f "$NGINX_INC" ]; then
    log_info "Installing nginx configuration..."
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    if [ -f "${SCRIPT_DIR}/../nginx/${SERVICE_NAME}.inc" ]; then
        cp "${SCRIPT_DIR}/../nginx/${SERVICE_NAME}.inc" "$NGINX_INC"
        log_warn "Remember to include this file in your nginx server block:"
        log_warn "  include ${NGINX_INC};"
    else
        log_warn "nginx config file not found, skipping..."
    fi
fi

# Restart service if requested
if [ "$DO_RESTART" = true ]; then
    log_info "Starting ${SERVICE_DISPLAY_NAME} service..."
    systemctl start "$SERVICE_NAME"
    sleep 2
    if systemctl is-active --quiet "$SERVICE_NAME"; then
        log_info "${SERVICE_DISPLAY_NAME} is running"
        systemctl status "$SERVICE_NAME" --no-pager -l | head -15
    else
        log_error "${SERVICE_DISPLAY_NAME} failed to start"
        journalctl -u "$SERVICE_NAME" --no-pager -n 20
        exit 1
    fi
fi

log_info "Deployment complete!"
echo ""
echo "Next steps:"
echo "  1. Ensure appsettings.json is configured (IF section for ConfigService)"
echo "  2. Ensure nginx includes: include ${NGINX_INC};"
echo "  3. Reload nginx: systemctl reload nginx"
echo "  4. Start service: systemctl start ${SERVICE_NAME}"
echo "  5. Check logs: journalctl -u ${SERVICE_NAME} -f"
