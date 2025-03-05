
#!/bin/bash

# Build the library first
cd ../..
pwd

npm install

sudo rm -rf ../dist/*

cd ./projects/ifauth-lib
pwd
npm install angular-auth-oidc-client
ng build ifauth-lib --configuration production --verbose

cd ../deepseek-gui
pwd
npm install ifauth-lib
ng build deepseek-gui --configuration production --verbose

sudo mkdir -p /var/www/Intelligence/
sudo rm -rf /var/www/Intelligence/*

sudo cp -r /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/ifauth-lib/* /var/www/Intelligence/
sudo cp -r /home/hugh/repos/Infoforum/Angular/projects/deepseek-gui/dist/deepseek-gui/* /var/www/Intelligence/

sudo chown -R nginx:nginx /var/www/Intelligence
sudo chmod -R 755 /var/www/Intelligence

cd ./projects/deepseek-gui
pwd
