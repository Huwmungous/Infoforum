#!/bin/bash

sudo systemctl stop if.ollama

dotnet publish --configuration Release --runtime linux-x64 --self-contained true

sudo mkdir -p /var/www/IFOllama/

sudo rm -rf /var/www/IFOllama/*

sudo cp /mnt/ai-data/repos/Infoforum/csharp/IFOllama/bin/Release/net8.0/linux-x64/publish/* /var/www/IFOllama -r 

sudo ln -s /mnt/ai-data/Conversations /var/www/IFOllama/Conversations

sudo systemctl start ifollama

sudo systemctl status if.ollama
