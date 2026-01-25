#!/bin/bash
# deploy-all.sh
# Master deployment script for IF WebServices and Apps
#
# Usage: ./deploy-all.sh [service...] [options]
#
# Services:
#   config    Deploy ConfigWebService
#   logger    Deploy LoggerWebService  
#   tokens    Deploy Tokens App
#   all       Deploy everything (default)
#
# Options:
#   --build     Build before deploying
#   --restart   Restart services after deploying

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_header() { echo -e "\n${BLUE}========================================${NC}"; echo -e "${BLUE} $1${NC}"; echo -e "${BLUE}========================================${NC}\n"; }

# Parse arguments
SERVICES=()
BUILD_FLAG=""
RESTART_FLAG=""

for arg in "$@"; do
    case $arg in
        config|logger|tokens|all)
            SERVICES+=("$arg")
            ;;
        --build)
            BUILD_FLAG="--build"
            ;;
        --restart)
            RESTART_FLAG="--restart"
            ;;
        --help)
            echo "Usage: $0 [service...] [options]"
            echo ""
            echo "Services:"
            echo "  config    Deploy ConfigWebService"
            echo "  logger    Deploy LoggerWebService"
            echo "  tokens    Deploy Tokens App"
            echo "  all       Deploy everything (default)"
            echo ""
            echo "Options:"
            echo "  --build     Build before deploying"
            echo "  --restart   Restart services after deploying"
            echo ""
            echo "Examples:"
            echo "  $0 all --build --restart    # Full deployment with build"
            echo "  $0 config --restart         # Deploy and restart ConfigWebService only"
            echo "  $0 tokens --build           # Build and deploy tokens app"
            exit 0
            ;;
    esac
done

# Default to 'all' if no services specified
if [ ${#SERVICES[@]} -eq 0 ]; then
    SERVICES=("all")
fi

# Expand 'all' to individual services
if [[ " ${SERVICES[*]} " =~ " all " ]]; then
    SERVICES=("config" "logger" "tokens")
fi

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    log_error "Please run as root (sudo)"
    exit 1
fi

echo ""
log_info "Deploying services: ${SERVICES[*]}"
log_info "Build flag: ${BUILD_FLAG:-none}"
log_info "Restart flag: ${RESTART_FLAG:-none}"
echo ""

# Deploy each service
for service in "${SERVICES[@]}"; do
    case $service in
        config)
            log_header "Deploying ConfigWebService"
            "${SCRIPT_DIR}/deploy-config-webservice.sh" $BUILD_FLAG $RESTART_FLAG
            ;;
        logger)
            log_header "Deploying LoggerWebService"
            "${SCRIPT_DIR}/deploy-logger-webservice.sh" $BUILD_FLAG $RESTART_FLAG
            ;;
        tokens)
            log_header "Deploying Tokens App"
            "${SCRIPT_DIR}/deploy-tokens-app.sh" $BUILD_FLAG
            ;;
    esac
done

# Reload nginx if any nginx configs were deployed
log_header "Finalizing Deployment"
log_info "Testing nginx configuration..."
if nginx -t 2>&1 | grep -q "successful"; then
    log_info "Reloading nginx..."
    systemctl reload nginx
    log_info "nginx reloaded successfully"
else
    log_warn "nginx configuration test failed - not reloading"
    log_warn "Fix the configuration and run: systemctl reload nginx"
fi

echo ""
log_info "Deployment complete!"
echo ""
echo "Service status:"
echo "  ConfigWebService: $(systemctl is-active config-webservice 2>/dev/null || echo 'not installed')"
echo "  LoggerWebService: $(systemctl is-active logger-webservice 2>/dev/null || echo 'not installed')"
echo ""
echo "URLs:"
echo "  ConfigWebService: https://longmanrd.net/config"
echo "  LoggerWebService: https://longmanrd.net/logger"
echo "  Tokens App:       https://longmanrd.net/tokens/{realm}/{client}/"
