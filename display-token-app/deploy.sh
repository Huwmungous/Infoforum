#!/bin/bash
#===============================================================================
# Deploy script for display-token-app
# Rebuilds the application and deploys to nginx serving directory
#===============================================================================

set -e  # Exit on any error

# Configuration
APP_NAME="display-token-app"
DEPLOY_DIR="/srv/Infoforum/Apps/${APP_NAME}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colours for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Colour

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Deploying ${APP_NAME}${NC}"
echo -e "${GREEN}========================================${NC}"

# Check we're in the right directory (should contain package.json)
if [ ! -f "package.json" ]; then
    echo -e "${RED}Error: package.json not found in current directory${NC}"
    echo -e "${YELLOW}Please run this script from the ${APP_NAME} source directory${NC}"
    exit 1
fi

# Verify this is the correct app
APP_CHECK=$(grep -o '"name": "display-token-app"' package.json || true)
if [ -z "$APP_CHECK" ]; then
    echo -e "${RED}Error: This doesn't appear to be the ${APP_NAME} directory${NC}"
    exit 1
fi

echo -e "${YELLOW}Step 1: Running npm rebuild...${NC}"
npm run rebuild

if [ ! -d "dist" ]; then
    echo -e "${RED}Error: Build failed - dist directory not created${NC}"
    exit 1
fi

echo -e "${YELLOW}Step 2: Creating deployment directory...${NC}"
sudo mkdir -p "${DEPLOY_DIR}"

echo -e "${YELLOW}Step 3: Deploying to ${DEPLOY_DIR}...${NC}"
# Remove old files and copy new build
sudo rm -rf "${DEPLOY_DIR:?}"/*
sudo cp -r dist/* "${DEPLOY_DIR}/"

# Set appropriate permissions for nginx
sudo chown -R www-data:www-data "${DEPLOY_DIR}"
sudo chmod -R 755 "${DEPLOY_DIR}"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Deployment complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "Application deployed to: ${DEPLOY_DIR}"
echo ""
echo -e "${YELLOW}Note: You may need to reload nginx if this is a new deployment:${NC}"
echo "  sudo systemctl reload nginx"