#!/bin/bash
#===============================================================================
# Deploy script for download-app
# Rebuilds the application and deploys to nginx serving directory
#
# Usage: ./deploy.sh <app-domain>
#   app-domain: 'infoforum' (default)
#
# Examples:
#   ./deploy.sh infoforum   -> https://longmanrd.net/infoforum/downloads/
#===============================================================================

set -e  # Exit on any error

# Colours for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Colour

# Default to infoforum if no argument
APP_DOMAIN="${1:-infoforum}"
APP_DOMAIN=$(echo "$APP_DOMAIN" | tr '[:upper:]' '[:lower:]')

# Configuration
APP_NAME="download-app"
BASE_PATH="/${APP_DOMAIN}/downloads/"
DEPLOY_DIR="/srv/Infoforum/Apps/${APP_NAME}"
VITE_CONFIG="vite.config.js"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Deploying ${APP_NAME}${NC}"
echo -e "${GREEN}Base path: ${BASE_PATH}${NC}"
echo -e "${GREEN}========================================${NC}"

# Check we're in the right directory (should contain package.json)
if [ ! -f "package.json" ]; then
    echo -e "${RED}Error: package.json not found in current directory${NC}"
    echo -e "${YELLOW}Please run this script from the ${APP_NAME} source directory${NC}"
    exit 1
fi

# Verify this is the correct app
APP_CHECK=$(grep -o '"name": "download-app"' package.json || true)
if [ -z "$APP_CHECK" ]; then
    echo -e "${RED}Error: This doesn't appear to be the ${APP_NAME} directory${NC}"
    exit 1
fi

# Check vite.config.js exists
if [ ! -f "$VITE_CONFIG" ]; then
    echo -e "${RED}Error: ${VITE_CONFIG} not found${NC}"
    exit 1
fi

echo -e "${YELLOW}Step 1: Updating base path in ${VITE_CONFIG}...${NC}"
# Update the base path in vite.config.js
sed -i "s|base: '[^']*'|base: '${BASE_PATH}'|" "$VITE_CONFIG"
echo -e "Set base to: ${BASE_PATH}"

echo -e "${YELLOW}Step 2: Running npm rebuild...${NC}"
npm run rebuild

if [ ! -d "dist" ]; then
    echo -e "${RED}Error: Build failed - dist directory not created${NC}"
    exit 1
fi

echo -e "${YELLOW}Step 3: Creating deployment directory...${NC}"
sudo mkdir -p "${DEPLOY_DIR}"

echo -e "${YELLOW}Step 4: Deploying to ${DEPLOY_DIR}...${NC}"
# Remove old files and copy new build
sudo rm -rf "${DEPLOY_DIR:?}"/*
sudo cp -r dist/* "${DEPLOY_DIR}/"

# Set appropriate permissions for nginx
if id "nginx" &>/dev/null; then
    NGINX_USER="nginx"
elif id "www-data" &>/dev/null; then
    NGINX_USER="www-data"
else
    NGINX_USER="root"
    echo -e "${YELLOW}Warning: Neither nginx nor www-data user found, using root${NC}"
fi

sudo chown -R "${NGINX_USER}:${NGINX_USER}" "${DEPLOY_DIR}"
sudo chmod -R 755 "${DEPLOY_DIR}"

# Fix SELinux context for nginx access
if command -v restorecon &>/dev/null; then
    echo -e "${YELLOW}Step 5: Restoring SELinux context...${NC}"
    sudo restorecon -Rv "${DEPLOY_DIR}"
fi

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Deployment complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "Application deployed to: ${DEPLOY_DIR}"
echo -e "Accessible at: https://longmanrd.net${BASE_PATH}"
echo ""
echo -e "${YELLOW}Note: You may need to reload nginx if this is a new deployment:${NC}"
echo "  sudo systemctl reload nginx"
