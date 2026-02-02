# ChitterChatter Distribution System

This system allows you to distribute ChitterChatter to users via a Keycloak-protected web page.

## Overview

1. **Build-Distribution.ps1** - Creates a self-contained installer package
2. **ChitterChatterDistribution** - Web service that hosts the protected download page

## Setup Instructions

### Step 1: Build the Distribution Package

On your development machine:

```powershell
cd C:\repos\Infoforum\ChitterChatter
.\Build-Distribution.ps1 -Version "1.0.0"
```

This creates `ChitterChatter-Setup.zip` (~80-100 MB) containing:
- Self-contained executable (no .NET runtime needed)
- Install.ps1 script
- All dependencies

### Step 2: Deploy the Distribution Web Service

1. **Build the web service:**
   ```powershell
   cd C:\repos\Infoforum\ChitterChatterDistribution
   dotnet publish -c Release -o publish
   ```

2. **Deploy to gambit:**
   ```bash
   # Create directories
   sudo mkdir -p /var/www/chitterchatter-dist/dist
   
   # Copy published files
   scp -r publish/* user@gambit:/var/www/chitterchatter-dist/
   ```

3. **Copy the distribution zip:**
   ```bash
   scp ChitterChatter-Setup.zip user@gambit:/var/www/chitterchatter-dist/dist/
   ```

4. **Create version.txt:**
   ```bash
   echo "1.0.0" > /var/www/chitterchatter-dist/dist/version.txt
   ```

5. **Copy the IF logo** (optional, for branding):
   ```bash
   cp IF-Logo.png /var/www/chitterchatter-dist/wwwroot/images/
   ```

### Step 3: Configure the Service

1. **Register the port** in your ConfigWebService:
   - Key: `chitterchatterdistribution`
   - Value: The port number (e.g., `5004`)

2. **Add to nginx** (example config):
   ```nginx
   location /chitterchatter-download/ {
       proxy_pass http://localhost:5004/;
       proxy_http_version 1.1;
       proxy_set_header Upgrade $http_upgrade;
       proxy_set_header Connection keep-alive;
       proxy_set_header Host $host;
       proxy_set_header X-Real-IP $remote_addr;
       proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
       proxy_set_header X-Forwarded-Proto $scheme;
       proxy_cache_bypass $http_upgrade;
   }
   ```

3. **Create systemd service:**
   ```ini
   # /etc/systemd/system/chitterchatter-dist.service
   [Unit]
   Description=ChitterChatter Distribution Service
   After=network.target

   [Service]
   WorkingDirectory=/var/www/chitterchatter-dist
   ExecStart=/usr/bin/dotnet /var/www/chitterchatter-dist/ChitterChatterDistribution.dll
   Restart=always
   RestartSec=10
   SyslogIdentifier=chitterchatter-dist
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

4. **Start the service:**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable chitterchatter-dist
   sudo systemctl start chitterchatter-dist
   ```

### Step 4: Tell Users

Send users to:
```
https://longmanrd.net/chitterchatter-download/
```

They will:
1. Be redirected to Keycloak to sign in
2. See the download page with version info
3. Click "Download Installer"
4. Extract the zip and run `Install.ps1` as Administrator

## Updating ChitterChatter

When you release a new version:

1. **Build new package:**
   ```powershell
   .\Build-Distribution.ps1 -Version "1.1.0"
   ```

2. **Replace on server:**
   ```bash
   scp ChitterChatter-Setup.zip user@gambit:/var/www/chitterchatter-dist/dist/
   echo "1.1.0" > /var/www/chitterchatter-dist/dist/version.txt
   ```

Users will see the new version on the download page.

## Directory Structure

```
/var/www/chitterchatter-dist/
├── ChitterChatterDistribution.dll
├── appsettings.json
├── wwwroot/
│   └── images/
│       └── IF-Logo.png
└── dist/
    ├── ChitterChatter-Setup.zip
    └── version.txt
```

## Troubleshooting

### "Download not available"
- Check that `ChitterChatter-Setup.zip` exists in the `dist` folder
- Check file permissions

### Authentication not working
- Verify Keycloak configuration in ConfigWebService
- Check service logs: `journalctl -u chitterchatter-dist -f`

### Users can't install
- Ensure they're running Install.ps1 as Administrator
- Check if antivirus is blocking the download
