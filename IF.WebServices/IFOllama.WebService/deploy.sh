#!/bin/bash

# IFOllama.WebService deployment script
set -e

SERVICE_NAME="ifollama-webservice"
DEPLOY_PATH="/opt/sfd/${SERVICE_NAME}"
PUBLISH_PATH="./IFOllama.WebService/bin/Release/net10.0/publish"

echo "Building ${SERVICE_NAME}..."
dotnet publish ./IFOllama.WebService/IFOllama.WebService.csproj -c Release

echo "Stopping service..."
sudo systemctl stop ${SERVICE_NAME} || true

echo "Deploying to ${DEPLOY_PATH}..."
sudo mkdir -p ${DEPLOY_PATH}
sudo cp -r ${PUBLISH_PATH}/* ${DEPLOY_PATH}/

echo "Setting permissions..."
sudo chown -R www-data:www-data ${DEPLOY_PATH}

echo "Starting service..."
sudo systemctl start ${SERVICE_NAME}

echo "Deployment complete!"
sudo systemctl status ${SERVICE_NAME}
