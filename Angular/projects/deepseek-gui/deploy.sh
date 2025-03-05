
#!/bin/bash

# Build the library first
cd ../..

npm install

sudo rm -rf ../dist/*

cd ifauth-lib
npm install angular-auth-oidc-client
ng build ifauth-lib --configuration production --verbose

cd ../deepseek-gui
npm install ifauth-lib
ng build deepseek-gui --configuration production --verbose

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/ifauth-lib/* /var/www/Intelligence/
sudo cp -r /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/deepseek-gui/* /var/www/Intelligence/

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence

cd ./projects/deepseek-gui
