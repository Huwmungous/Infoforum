#!/bin/bash

sudo ng build --configuration production --base-href /testapp/

sudo mkdir -p /var/www/testapp/
sudo rm -rf /var/www/testapp/*

sudo cp -r /home/hugh/repos/Infoforum/testapp/dist/testapp/* /var/www/testapp/

sudo chown -R nginx:nginx /var/www/testapp
sudo chmod -R 755 /var/www/testapp
