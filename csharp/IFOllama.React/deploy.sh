#!/bin/bash

# IFOllama.React deployment script
set -e

APP_NAME="ifollama-react"
DEPLOY_PATH="/var/www/${APP_NAME}"
DIST_PATH="./dist"

echo "Building ${APP_NAME}..."
npm run build

echo "Deploying to ${DEPLOY_PATH}..."
sudo mkdir -p ${DEPLOY_PATH}
sudo rm -rf ${DEPLOY_PATH}/*
sudo cp -r ${DIST_PATH}/* ${DEPLOY_PATH}/

echo "Setting permissions..."
sudo chown -R www-data:www-data ${DEPLOY_PATH}

echo "Deployment complete!"
echo "Site deployed to: ${DEPLOY_PATH}"

# Optionally reload nginx
if command -v nginx &> /dev/null; then
    echo "Reloading nginx..."
    sudo nginx -t && sudo systemctl reload nginx
fi
