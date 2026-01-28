#!/bin/bash
set -euo pipefail

# ----------------------------
# Configuration
# ----------------------------
APP_NAME="log-viewer-app"
APP_DIR="/home/hugh/repos/dotnet/Tools/IFLogViewer"
LIB_DIR="/home/hugh/repos/dotnet/IF.Web.Common"
DEPLOY_DIR="/srv/sfddevelopment/WebApps/$APP_NAME"

# ----------------------------
# Environment variables
# ----------------------------
IF_CONFIG_SERVICE="${IF_CONFIG_SERVICE:-https://sfddevelopment.com/config}"
IF_REALM="${IF_REALM:-SfdDevelopment_Dev}"
IF_CLIENT="${IF_CLIENT:-dev-login}"

echo ">>> Deploying $APP_NAME..."
echo "[INFO] Working directory: $APP_DIR"
echo "[INFO] Library directory: $LIB_DIR"
echo "[INFO] Deployment target: $DEPLOY_DIR"

# ----------------------------
# Sanity checks
# ----------------------------
if [ ! -d "$APP_DIR" ]; then
    echo "[ERROR] App directory not found: $APP_DIR"
    exit 1
fi

if [ ! -d "$LIB_DIR" ]; then
    echo "[ERROR] Library path not found: $LIB_DIR"
    exit 1
fi

# ----------------------------
# Build the library
# ----------------------------
echo "[INFO] Building @if/web-common library..."
cd "$LIB_DIR"
npm install
npm run build

# ----------------------------
# Install app dependencies
# ----------------------------
echo "[INFO] Installing $APP_NAME dependencies..."
cd "$APP_DIR"
npm install

# ----------------------------
# Build app
# ----------------------------
echo "[INFO] Building $APP_NAME..."
npm run build || {
    echo "[ERROR] App build failed."
    exit 1
}

# ----------------------------
# Deploy to web server
# ----------------------------
echo "[INFO] Deploying files to $DEPLOY_DIR..."
sudo mkdir -p "$DEPLOY_DIR"
sudo rm -rf "$DEPLOY_DIR"/*
sudo cp -r "$APP_DIR/dist/"* "$DEPLOY_DIR/"

# ----------------------------
# Generate runtime config from environment variables
# ----------------------------
echo "[INFO] Generating if-config.js from environment variables..."
sudo tee "$DEPLOY_DIR/if-config.js" > /dev/null << EOF
window.__IF_CONFIG__ = {
  configService: "${IF_CONFIG_SERVICE}",
  realm: "${IF_REALM}",
  client: "${IF_CLIENT}"
};
EOF
echo "[INFO] if-config.js generated with:"
echo "  configService: ${IF_CONFIG_SERVICE}"
echo "  realm: ${IF_REALM}"
echo "  client: ${IF_CLIENT}"

# ----------------------------
# Apply SELinux labels if needed
# ----------------------------
if command -v chcon &>/dev/null; then
    echo "[INFO] Applying SELinux context for nginx access..."
    sudo chcon -R -t httpd_sys_content_t "$DEPLOY_DIR"
fi

echo "[OK] $APP_NAME deployed successfully!"
echo "======================================"
echo "Deployment Complete"
echo "The app is now available at: https://sfddevelopment.com/logs"
