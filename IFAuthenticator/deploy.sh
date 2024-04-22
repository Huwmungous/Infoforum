#!/bin/bash

sudo systemctl stop ifauthenticator

dotnet publish --configuration Release  

sudo mkdir -p /var/www/IFAuthenticator/

sudo rm -rf /var/www/IFAuthenticator/*

sudo cp /home/hugh/repos/ifauthenticator/bin/Release/net8.0/linux-x64/publish/* /var/www/IFAuthenticator -r

# sudo ln -s /usr/lib64/libldap-2.4.so.2 /usr/lib64/dotnet/shared/Microsoft.NETCore.App/8.0.3

# sudo ln -s /usr/lib64/libldap.so.2.0.200 /var/www/IFAuthenticator/libldap.so.2    

sudo systemctl start ifauthenticator

sudo systemctl status ifauthenticator
