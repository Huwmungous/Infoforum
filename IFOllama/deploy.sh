#!/bin/bash

sudo systemctl stop ifollama

dotnet publish --configuration Release -r linux-x64  

sudo mkdir -p /var/www/IFOllama/

sudo rm -rf /var/www/IFOllama/*

sudo cp /mnt/ai-data/repos/infoforum/IFOllama/bin/Release/net8.0/linux-x64/publish/* /var/www/IFOllama -r --self-contained true

# sudo ln -s /usr/lib64/libldap-2.4.so.2 /usr/lib64/dotnet/shared/Microsoft.NETCore.App/8.0.3

# sudo ln -s /usr/lib64/libldap.so.2.0.200 /var/www/IFOllama/libldap.so.2    

sudo systemctl start ifollama

sudo systemctl status ifollama
