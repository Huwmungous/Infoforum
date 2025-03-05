
#!/bin/bash

set -e

# Build the library first
cd ../..
pwd

rm -rf ./dist/*
rm -rf ./node_modules/*

npm install

cd ./projects/deepseek-gui
pwd
npm install angular-auth-oidc-client
ng build deepseek-gui --configuration production

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r ../../dist/deepseek-gui/* /var/www/Intelligence/ 

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence

pwd
