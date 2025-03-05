#!/bin/bash

# Deployment Script for Deepseek GUI

# Configuration Variables
APP_NAME="deepseek-gui"
LIB_NAME="ifauth-lib"
DEPLOY_PATH="/var/www/deepseek-gui"

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Error handling function
handle_error() {
    echo -e "${RED}Error: $1${NC}"
    exit 1
}

# Build library and application
build_application() {
    echo -e "${YELLOW}Building library...${NC}"
    ng build $LIB_NAME --configuration=production --force || handle_error "Library build failed"

    echo -e "${YELLOW}Building application...${NC}"
    ng build $APP_NAME --configuration=production || handle_error "Application build failed"
}

# Deploy application
deploy_application() {
    echo -e "${YELLOW}Deploying application...${NC}"

    # Create deployment directory if it doesn't exist
    sudo mkdir -p $DEPLOY_PATH

    # Copy build artifacts
    sudo cp -R dist/$APP_NAME/* $DEPLOY_PATH/

    # Set correct permissions
    sudo chown -R nginx:nginx $DEPLOY_PATH
    sudo chmod -R 755 $DEPLOY_PATH
}

# Main deployment workflow
main() {
    sudo rm -r $DEPLOY_PATH/*
    build_application
    deploy_application
    echo -e "${GREEN}Deployment completed successfully!${NC}"
}

# Run the deployment
main
