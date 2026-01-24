#!/bin/bash
# deploy-tokens-app.sh
# Deploys the Tokens App (display-token-app) React SPA to the server
#
# Usage: ./deploy-tokens-app.sh [--build] [--config-service URL]
#   --build              Build the project before deploying
#   --config-service URL Set the ConfigWebService URL (default: /config)

set -e

# Configuration
APP_NAME="tokens-app"
APP_DISPLAY_NAME="Tokens App"
PROJECT_DIR="${PROJECT_DIR:-/path/to/display-token-app}"
DEPLOY_DIR="/var/www/tokens"
NGINX_INC="/etc/nginx/conf.d/${APP_NAME}.inc"
CONFIG_SERVICE_URL="${CONFIG_SERVICE_URL:-/config}"

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
while [[ $# -gt 0 ]]; do
    case $1 in
        --build) DO_BUILD=true; shift ;;
        --config-service) CONFIG_SERVICE_URL="$2"; shift 2 ;;
        --help) 
            echo "Usage: $0 [--build] [--config-service URL]"
            echo "  --build              Build the project before deploying"
            echo "  --config-service URL Set the ConfigWebService URL (default: /config)"
            exit 0
            ;;
        *) shift ;;
    esac
done

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    log_error "Please run as root (sudo)"
    exit 1
fi

# Build if requested
if [ "$DO_BUILD" = true ]; then
    log_info "Building ${APP_DISPLAY_NAME}..."
    if [ ! -d "$PROJECT_DIR" ]; then
        log_error "Project directory not found: $PROJECT_DIR"
        log_info "Set PROJECT_DIR environment variable to the correct path"
        exit 1
    fi
    cd "$PROJECT_DIR"
    
    # Install dependencies if needed
    if [ ! -d "node_modules" ]; then
        log_info "Installing npm dependencies..."
        npm install
    fi
    
    # Set environment variables for build
    export VITE_IF_CONFIG_SERVICE_URL="$CONFIG_SERVICE_URL"
    
    log_info "Building with VITE_IF_CONFIG_SERVICE_URL=${CONFIG_SERVICE_URL}..."
    npm run build
fi

# Determine source directory
if [ -d "${PROJECT_DIR}/dist" ]; then
    SOURCE_DIR="${PROJECT_DIR}/dist"
elif [ -d "${PROJECT_DIR}/build" ]; then
    SOURCE_DIR="${PROJECT_DIR}/build"
else
    log_error "Cannot find build output (dist or build directory) in ${PROJECT_DIR}"
    log_info "Either run with --build or ensure build output exists"
    exit 1
fi

# Create deployment directory
log_info "Creating deployment directory..."
mkdir -p "$DEPLOY_DIR"

# Backup existing deployment
if [ -d "$DEPLOY_DIR" ] && [ "$(ls -A $DEPLOY_DIR 2>/dev/null)" ]; then
    BACKUP_DIR="${DEPLOY_DIR}.backup.$(date +%Y%m%d_%H%M%S)"
    log_info "Backing up existing deployment to ${BACKUP_DIR}..."
    mv "$DEPLOY_DIR" "$BACKUP_DIR"
    mkdir -p "$DEPLOY_DIR"
fi

# Copy files
log_info "Deploying files to ${DEPLOY_DIR}..."
cp -r "${SOURCE_DIR}/"* "$DEPLOY_DIR/"

# Set permissions
log_info "Setting permissions..."
chown -R www-data:www-data "$DEPLOY_DIR"
find "$DEPLOY_DIR" -type d -exec chmod 755 {} \;
find "$DEPLOY_DIR" -type f -exec chmod 644 {} \;

# Install nginx config if not exists
if [ ! -f "$NGINX_INC" ]; then
    log_info "Installing nginx configuration..."
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    if [ -f "${SCRIPT_DIR}/../nginx/${APP_NAME}.inc" ]; then
        cp "${SCRIPT_DIR}/../nginx/${APP_NAME}.inc" "$NGINX_INC"
        log_warn "Remember to include this file in your nginx server block:"
        log_warn "  include ${NGINX_INC};"
    else
        log_warn "nginx config file not found, skipping..."
    fi
fi

# Test nginx config
if command -v nginx &> /dev/null; then
    log_info "Testing nginx configuration..."
    if nginx -t 2>&1 | grep -q "successful"; then
        log_info "nginx configuration test passed"
    else
        log_warn "nginx configuration test failed - check your config"
    fi
fi

log_info "Deployment complete!"
echo ""
echo "Deployed files:"
ls -la "$DEPLOY_DIR" | head -10
echo ""
echo "Next steps:"
echo "  1. Ensure nginx includes: include ${NGINX_INC};"
echo "  2. Reload nginx: systemctl reload nginx"
echo "  3. Test: https://longmanrd.net/tokens/{realm}/{client}/"
echo ""
echo "Example URLs:"
echo "  https://longmanrd.net/tokens/IfDevelopment_Dev/dev-login/"
echo "  https://longmanrd.net/tokens/SfdDevelopment_Dev/dev-login/"
