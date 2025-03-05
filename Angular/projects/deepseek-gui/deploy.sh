#!/bin/bash

set -e

# Build and deploy the library first
echo "Building and deploying deepseek-gui..."
npm run build-and-deploy || { echo "Error building and deploying library!"; exit 1; }

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r ../../dist/deepseek-gui/* /var/www/Intelligence/ 

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence

pwd