#!/bin/bash

ng build testapp --base-href /testapp/ --configuration production

sudo mkdir -p /var/www/testapp/
sudo rm -rf /var/www/testapp/*

sudo cp -r /home/hugh/repos/Infoforum/Angular/dist/testapp/* /var/www/testapp/

sudo chown -R nginx:nginx /var/www/testapp
sudo chmod -R 755 /var/www/testapp
