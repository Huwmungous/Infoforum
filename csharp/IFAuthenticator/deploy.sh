#!/bin/bash

USERNAME="IFServices"

# Check if the user already exists
if id "$USERNAME" &>/dev/null; then
    echo "srevice account is OK"
else
    echo "Adding user $USERNAME."
    sudo useradd -r -s /usr/sbin/nologin -m $USERNAME
    echoUSERBA "User $USERNAME has been added successfully."
fi

sudo systemctl stop ifauth

rm -rf ./bin/*

dotnet publish --configuration Release -r linux-x64 --self-contained=true

sudo mkdir -p /var/www/IFAuth

sudo rm -rf /var/www/IFAuth/*

sudo cp /home/hugh/repos/Infoforum/IFAuthenticator/bin/Release/net8.0/linux-x64/publish/* /var/www/IFAuth -r

sudo  chown -R $USERNAME:$USERNAME  /var/www/IFAuth
sudo  chmod -R 700 /var/www/IFAuth

sudo systemctl start ifauth

sudo systemctl status ifauth
