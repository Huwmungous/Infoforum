#Requires -Version 5.1
<#
.SYNOPSIS
    Creates deployment scripts for all MCP servers.

.DESCRIPTION
    Generates deploy.sh scripts for each MCP server to deploy to Fedora at /srv/sfddevelopment/MCPServers

.PARAMETER RootPath
    The root directory containing the MCP server projects.

.EXAMPLE
    .\Create-DeployScripts.ps1
    .\Create-DeployScripts.ps1 -RootPath "D:\repos\SfD\MCP Servers"
#>

param(
    [string]$RootPath = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Write-Success { param([string]$Message); Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Step { param([string]$Message); Write-Host ">>> $Message" -ForegroundColor Cyan }

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Creating Deployment Scripts" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Master deployment script
Write-Step "Creating deploy-all.sh..."
$deployAllContent = @'
#!/bin/bash
set -e

DEPLOY_ROOT="/srv/sfddevelopment/MCPServers"
BUILD_CONFIG="Release"

echo "======================================"
echo "  MCP Servers Deployment"
echo "======================================"

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

deploy_server() {
    local SERVER_NAME=$1
    local SERVER_PATH=$2
    
    echo -e "${YELLOW}>>> Deploying $SERVER_NAME...${NC}"
    cd "$SERVER_PATH"
    
    dotnet clean --configuration $BUILD_CONFIG > /dev/null 2>&1
    dotnet publish --configuration $BUILD_CONFIG --output "./publish" --self-contained false --runtime linux-x64
    
    if [ $? -ne 0 ]; then
        echo -e "${RED}[ERROR] Build failed for $SERVER_NAME${NC}"
        return 1
    fi
    
    DEPLOY_PATH="$DEPLOY_ROOT/$SERVER_NAME"
    sudo mkdir -p "$DEPLOY_PATH"
    
    if [ -d "$DEPLOY_PATH" ] && [ "$(ls -A $DEPLOY_PATH)" ]; then
        BACKUP_PATH="${DEPLOY_PATH}.backup.$(date +%Y%m%d_%H%M%S)"
        sudo mv "$DEPLOY_PATH" "$BACKUP_PATH"
        sudo mkdir -p "$DEPLOY_PATH"
    fi
    
    sudo cp -r ./publish/* "$DEPLOY_PATH/"
    sudo chown -R $USER:$USER "$DEPLOY_PATH"
    
    cat > "$DEPLOY_PATH/run.sh" << EOF
#!/bin/bash
cd "\$(dirname "\$0")"
exec dotnet $SERVER_NAME.dll "\$@"
EOF
    chmod +x "$DEPLOY_PATH/run.sh"
    
    rm -rf ./publish
    echo -e "${GREEN}[OK] $SERVER_NAME deployed${NC}"
    cd - > /dev/null
}

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
sudo mkdir -p "$DEPLOY_ROOT"
sudo chown -R $USER:$USER "$DEPLOY_ROOT"

deploy_server "FileSystemMcpServer" "$SCRIPT_DIR/FileSystemMcpServer"
deploy_server "DotNetBuildMcpServer" "$SCRIPT_DIR/DotNetBuildMcpServer"
deploy_server "FirebirdMcpServer" "$SCRIPT_DIR/FirebirdMcpServer"
deploy_server "CodeAnalysisMcpServer" "$SCRIPT_DIR/CodeAnalysisMcpServer"

echo ""
echo "======================================"
echo -e "${GREEN}  Deployment Complete!${NC}"
echo "======================================"
echo ""
echo "Servers deployed to: $DEPLOY_ROOT"
'@

Set-Content -Path (Join-Path $RootPath "deploy-all.sh") -Value $deployAllContent -Encoding UTF8
Write-Success "Created deploy-all.sh"

# Individual server deployment scripts
$servers = @(
    @{Name="FileSystemMcpServer"; DllName="FileSystemMcpServer.dll"; ServiceName="filesystem-mcp"},
    @{Name="DotNetBuildMcpServer"; DllName="DotNetBuildMcpServer.dll"; ServiceName="dotnetbuild-mcp"},
    @{Name="FirebirdMcpServer"; DllName="FirebirdMcpServer.dll"; ServiceName="firebird-mcp"},
    @{Name="CodeAnalysisMcpServer"; DllName="CodeAnalysisMcpServer.dll"; ServiceName="codeanalysis-mcp"}
)

foreach ($server in $servers) {
    $serverPath = Join-Path $RootPath $server.Name
    if (!(Test-Path $serverPath)) {
        Write-Host "[SKIP] $($server.Name) directory not found" -ForegroundColor Yellow
        continue
    }
    
    Write-Step "Creating deploy.sh for $($server.Name)..."
    
    $deployContent = @"
#!/bin/bash
set -e

SERVER_NAME="$($server.Name)"
DEPLOY_ROOT="/srv/sfddevelopment/MCPServers"
DEPLOY_PATH="`$DEPLOY_ROOT/`$SERVER_NAME"
BUILD_CONFIG="Release"

echo "======================================"
echo "  `$SERVER_NAME Deployment"
echo "======================================"

SCRIPT_DIR="`$( cd "`$( dirname "`${BASH_SOURCE[0]}" )" && pwd )"
cd "`$SCRIPT_DIR"

echo ">>> Cleaning..."
dotnet clean --configuration `$BUILD_CONFIG > /dev/null 2>&1

echo ">>> Building..."
dotnet publish --configuration `$BUILD_CONFIG --output "./publish" --self-contained false --runtime linux-x64

if [ `$? -ne 0 ]; then
    echo "[ERROR] Build failed!"
    exit 1
fi

echo ">>> Creating deployment directory..."
sudo mkdir -p "`$DEPLOY_PATH"

if [ -d "`$DEPLOY_PATH" ] && [ "`$(ls -A `$DEPLOY_PATH)" ]; then
    BACKUP_PATH="`${DEPLOY_PATH}.backup.`$(date +%Y%m%d_%H%M%S)"
    sudo mv "`$DEPLOY_PATH" "`$BACKUP_PATH"
    sudo mkdir -p "`$DEPLOY_PATH"
    echo "    Backup created: `$BACKUP_PATH"
fi

echo ">>> Deploying files..."
sudo cp -r ./publish/* "`$DEPLOY_PATH/"
sudo chown -R `$USER:`$USER "`$DEPLOY_PATH"

echo ">>> Creating run script..."
cat > "`$DEPLOY_PATH/run.sh" << 'EOF'
#!/bin/bash
cd "`$(dirname "`$0")"
exec dotnet $($server.DllName) "`$@"
EOF
chmod +x "`$DEPLOY_PATH/run.sh"

echo ">>> Creating systemd service..."
sudo tee "/etc/systemd/system/$($server.ServiceName).service" > /dev/null << EOF
[Unit]
Description=$($server.Name)
After=network.target

[Service]
Type=simple
User=`$USER
WorkingDirectory=`$DEPLOY_PATH
ExecStart=/usr/bin/dotnet `$DEPLOY_PATH/$($server.DllName)
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

echo ">>> Creating MCP config snippet..."
cat > "`$DEPLOY_PATH/mcp-config.json" << EOF
{
  "mcpServers": {
    "$($server.ServiceName -replace '-mcp$','')": {
      "command": "dotnet",
      "args": ["`$DEPLOY_PATH/$($server.DllName)"]
    }
  }
}
EOF

rm -rf ./publish

echo ""
echo "======================================"
echo "  Deployment Complete!"
echo "======================================"
echo ""
echo "Deployed to: `$DEPLOY_PATH"
echo ""
echo "To run manually:"
echo "  cd `$DEPLOY_PATH && ./run.sh"
echo ""
echo "To run as systemd service:"
echo "  sudo systemctl daemon-reload"
echo "  sudo systemctl enable $($server.ServiceName)"
echo "  sudo systemctl start $($server.ServiceName)"
echo "  sudo systemctl status $($server.ServiceName)"
echo ""
echo "To view logs:"
echo "  sudo journalctl -u $($server.ServiceName) -f"
"@
    
    $deployPath = Join-Path $serverPath "deploy.sh"
    Set-Content -Path $deployPath -Value $deployContent -Encoding UTF8
    Write-Success "Created $($server.Name)/deploy.sh"
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  All Deployment Scripts Created!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Scripts created:"
Write-Host "  - deploy-all.sh (master deployment script)"
Write-Host "  - FileSystemMcpServer/deploy.sh"
Write-Host "  - DotNetBuildMcpServer/deploy.sh"
Write-Host "  - FirebirdMcpServer/deploy.sh"
Write-Host "  - CodeAnalysisMcpServer/deploy.sh"
Write-Host ""
Write-Host "To deploy all servers to Fedora:" -ForegroundColor Cyan
Write-Host "  1. Copy the entire MCP Servers folder to Fedora"
Write-Host "  2. On Fedora, run: chmod +x deploy-all.sh && ./deploy-all.sh"
Write-Host ""
Write-Host "Or deploy individually:" -ForegroundColor Cyan
Write-Host "  cd <ServerName> && chmod +x deploy.sh && ./deploy.sh"