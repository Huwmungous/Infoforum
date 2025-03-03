#!/bin/bash

# Navigate to the workspace root where IFAuthModule is located
cd ../..

# Build the IFAuthModule
sudo ng build IFAuthModule --configuration production

# Change directory back to deepseek-gui
cd ./projects/deepseek-gui

# Build the deepseek-gui application, making sure it includes IFAuthModule
ng build deepseek-gui --base-href /intelligence/ --configuration production

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r  /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/deepseek-gui/* /var/www/Intelligence/

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence
