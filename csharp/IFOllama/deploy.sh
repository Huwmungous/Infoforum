#!/bin/bash

sudo systemctl stop ifollama

dotnet publish --configuration Release --runtime linux-x64 --self-contained true

sudo mkdir -p /var/www/IFOllama/

sudo rm -rf /var/www/IFOllama/*

sudo cp /mnt/ai-data/repos/Infoforum/csharp/IFOllama/bin/Release/net8.0/linux-x64/publish/* /var/www/IFOllama -r 

sudo systemctl start ifollama

sudo systemctl status ifollama
