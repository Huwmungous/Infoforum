#!/bin/bash

sudo mkdir -p /var/www/RagIndexer/

sudo rm -rf /var/www/RagIndexer/*

dotnet publish --configuration Release --runtime linux-x64 --self-contained true

sudo cp /mnt/ai-data/repos/Infoforum/csharp/RagIndexer/bin/Release/net9.0/linux-x64/* /var/www/RagIndexer -r 

sudo /var/www/RagIndexer/RagIndexer
