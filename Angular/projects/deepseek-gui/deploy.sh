#!/bin/bash

# Build the library first
cd ../..

ng build ifauth-lib --configuration production

# Now build the application
ng build deepseek-gui --configuration production

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/deepseek-gui/* /var/www/Intelligence/

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence

cd ./projects/deepseek-gui

