#!/bin/bash
#===============================================================================
# ChitterChatter Build & Deploy Script
#
# Builds BOTH:
#   1. Distribution backend service (ASP.NET on Linux)
#   2. Windows client installer (via Wine + Inno Setup)
#
# Prerequisites:
#   - .NET SDK 10.0+
#   - Wine installed (sudo dnf install wine)
#   - Inno Setup installed under Wine
#
# Inno Setup Installation (one-time):
#   1. Download from https://jrsoftware.org/isdl.php
#   2. Run: xvfb-run wine ~/Downloads/innosetup.exe /VERYSILENT
#
# Usage:
#   ./deploy.sh <version>
#
# Examples:
#   ./deploy.sh 1.0.0
#   ./deploy.sh 1.2.3
#===============================================================================

set -e  # Exit on any error

# Colours for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Colour

# Check for required argument
if [ -z "$1" ]; then
    echo -e "${RED}Error: Version number required${NC}"
    echo ""
    echo "Usage: $0 <version>"
    echo "Example: $0 1.0.0"
    exit 1
fi

VERSION="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLIENT_DIR="${SCRIPT_DIR}/ChitterChatterClient"
DIST_SRC_DIR="${SCRIPT_DIR}/Distribution"
ISS_FILE="${SCRIPT_DIR}/ChitterChatter-Setup.iss"
DIST_DEPLOY_DIR="/var/www/chitterchatter-dist"
DIST_FILES_DIR="${DIST_DEPLOY_DIR}/dist"

# Wine paths - adjust if your Inno Setup is installed elsewhere
WINE_PREFIX="${WINEPREFIX:-$HOME/.wine}"
INNO_SETUP="${WINE_PREFIX}/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe"

# Alternative path if installed to Program Files (not x86)
if [ ! -f "$INNO_SETUP" ]; then
    INNO_SETUP="${WINE_PREFIX}/drive_c/Program Files/Inno Setup 6/ISCC.exe"
fi

echo -e "${GREEN}==========================================${NC}"
echo -e "${GREEN}ChitterChatter Build & Deploy${NC}"
echo -e "${GREEN}Version: ${VERSION}${NC}"
echo -e "${GREEN}==========================================${NC}"

# Verify prerequisites
echo -e "${BLUE}Checking prerequisites...${NC}"

if [ ! -d "$CLIENT_DIR" ]; then
    echo -e "${RED}Error: Client directory not found at: ${CLIENT_DIR}${NC}"
    exit 1
fi

if [ ! -d "$DIST_SRC_DIR" ]; then
    echo -e "${RED}Error: Distribution directory not found at: ${DIST_SRC_DIR}${NC}"
    exit 1
fi

if [ ! -f "$ISS_FILE" ]; then
    echo -e "${RED}Error: Inno Setup script not found at: ${ISS_FILE}${NC}"
    echo -e "${YELLOW}Please ensure ChitterChatter-Setup.iss is in the ChitterChatter directory${NC}"
    exit 1
fi

if ! command -v wine &> /dev/null; then
    echo -e "${RED}Error: Wine is not installed${NC}"
    echo -e "${YELLOW}Install with: sudo dnf install wine${NC}"
    exit 1
fi

if [ ! -f "$INNO_SETUP" ]; then
    echo -e "${RED}Error: Inno Setup not found under Wine${NC}"
    echo -e "${YELLOW}Install Inno Setup:${NC}"
    echo -e "  1. Download from https://jrsoftware.org/isdl.php"
    echo -e "  2. Run: xvfb-run wine /tmp/innosetup.exe /VERYSILENT"
    exit 1
fi

echo -e "${GREEN}✓ All prerequisites met${NC}"

#===============================================================================
# PART 1: Build Distribution Backend Service
#===============================================================================
echo ""
echo -e "${YELLOW}Step 1: Building Distribution backend service...${NC}"
cd "$DIST_SRC_DIR"

rm -rf bin/Release obj/Release

dotnet publish -c Release -r linux-x64 --self-contained \
    -p:Version="${VERSION}" \
    -p:AssemblyVersion="${VERSION}" \
    -p:FileVersion="${VERSION}"

# Find the publish directory
DIST_PUBLISH_DIR=""
for path in \
    "${DIST_SRC_DIR}/bin/Release/net10.0/linux-x64/publish" \
    "${DIST_SRC_DIR}/bin/Release/net9.0/linux-x64/publish" \
    "${DIST_SRC_DIR}/bin/Release/net8.0/linux-x64/publish"
do
    if [ -d "$path" ]; then
        DIST_PUBLISH_DIR="$path"
        break
    fi
done

if [ -z "$DIST_PUBLISH_DIR" ]; then
    echo -e "${RED}Error: Distribution publish directory not found${NC}"
    ls -la "${DIST_SRC_DIR}/bin/Release/" 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ Distribution backend built successfully${NC}"

#===============================================================================
# PART 2: Deploy Distribution Backend
#===============================================================================
echo ""
echo -e "${YELLOW}Step 2: Deploying Distribution backend...${NC}"

# Stop service before deploying
if systemctl is-active --quiet chitterchatter-dist; then
    echo -e "  Stopping chitterchatter-dist service..."
    sudo systemctl stop chitterchatter-dist
fi

# Deploy backend files (preserve dist/ folder with installers)
sudo mkdir -p "$DIST_DEPLOY_DIR"
sudo mkdir -p "$DIST_FILES_DIR"
sudo cp -r "${DIST_PUBLISH_DIR}"/* "$DIST_DEPLOY_DIR/"

echo -e "${GREEN}✓ Distribution backend deployed to: ${DIST_DEPLOY_DIR}${NC}"

#===============================================================================
# PART 3: Build Windows Client
#===============================================================================
echo ""
echo -e "${YELLOW}Step 3: Building ChitterChatter client for Windows...${NC}"
cd "$CLIENT_DIR"

rm -rf bin/Release obj/Release
rm -rf "${SCRIPT_DIR}/Output"

dotnet publish -c Release -r win-x64 --self-contained \
    -p:EnableWindowsTargeting=true \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=false \
    -p:Version="${VERSION}" \
    -p:AssemblyVersion="${VERSION}" \
    -p:FileVersion="${VERSION}"

# Find the publish directory
PUBLISH_DIR=""
for path in \
    "${CLIENT_DIR}/bin/Release/net10.0-windows/win-x64/publish" \
    "${CLIENT_DIR}/bin/Release/net10.0/win-x64/publish" \
    "${CLIENT_DIR}/bin/Release/net9.0-windows/win-x64/publish" \
    "${CLIENT_DIR}/bin/Release/net9.0/win-x64/publish" \
    "${CLIENT_DIR}/bin/Release/net8.0-windows/win-x64/publish" \
    "${CLIENT_DIR}/bin/Release/net8.0/win-x64/publish"
do
    if [ -d "$path" ]; then
        PUBLISH_DIR="$path"
        break
    fi
done

if [ -z "$PUBLISH_DIR" ]; then
    echo -e "${RED}Error: Client publish directory not found${NC}"
    ls -la "${CLIENT_DIR}/bin/Release/" 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ Client built successfully${NC}"

#===============================================================================
# PART 4: Create Installer with Inno Setup
#===============================================================================
echo ""
echo -e "${YELLOW}Step 4: Creating Windows installer with Inno Setup...${NC}"
cd "$SCRIPT_DIR"
mkdir -p Output

# Convert Linux paths to Windows paths for Wine
PUBLISH_DIR_WIN=$(winepath -w "$PUBLISH_DIR" 2>/dev/null || echo "Z:${PUBLISH_DIR}")
ISS_FILE_WIN=$(winepath -w "$ISS_FILE" 2>/dev/null || echo "Z:${ISS_FILE}")
OUTPUT_DIR_WIN=$(winepath -w "${SCRIPT_DIR}/Output" 2>/dev/null || echo "Z:${SCRIPT_DIR}/Output")

# Set environment variables for Inno Setup script
export CHITTERCHATTER_VERSION="$VERSION"
export CHITTERCHATTER_SOURCE="$PUBLISH_DIR_WIN"

# Run Inno Setup compiler (use xvfb-run if no display available)
echo -e "  Running Inno Setup..."
if [ -z "$DISPLAY" ] && command -v xvfb-run &> /dev/null; then
    xvfb-run wine "$INNO_SETUP" \
        "/DMyAppVersion=${VERSION}" \
        "/DSourcePath=${PUBLISH_DIR_WIN}" \
        "/O${OUTPUT_DIR_WIN}" \
        "$ISS_FILE_WIN"
else
    wine "$INNO_SETUP" \
        "/DMyAppVersion=${VERSION}" \
        "/DSourcePath=${PUBLISH_DIR_WIN}" \
        "/O${OUTPUT_DIR_WIN}" \
        "$ISS_FILE_WIN"
fi

INSTALLER_FILE="${SCRIPT_DIR}/Output/ChitterChatter-Setup-${VERSION}.exe"

if [ ! -f "$INSTALLER_FILE" ]; then
    echo -e "${RED}Error: Installer was not created${NC}"
    exit 1
fi

INSTALLER_SIZE=$(du -h "$INSTALLER_FILE" | cut -f1)
echo -e "${GREEN}✓ Installer created: ChitterChatter-Setup-${VERSION}.exe (${INSTALLER_SIZE})${NC}"

#===============================================================================
# PART 5: Deploy Installer
#===============================================================================
echo ""
echo -e "${YELLOW}Step 5: Deploying installer to distribution folder...${NC}"

# Remove old installers (keep last 3 versions for rollback)
echo -e "  Cleaning old installers (keeping last 3)..."
cd "$DIST_FILES_DIR"
ls -t ChitterChatter-Setup-*.exe 2>/dev/null | tail -n +4 | xargs -r sudo rm -f

# Copy new installer
sudo cp "$INSTALLER_FILE" "$DIST_FILES_DIR/"
sudo chown hugh:hugh "${DIST_FILES_DIR}/ChitterChatter-Setup-${VERSION}.exe"
sudo chmod 644 "${DIST_FILES_DIR}/ChitterChatter-Setup-${VERSION}.exe"

# Update version file
echo "$VERSION" | sudo tee "${DIST_FILES_DIR}/version.txt" > /dev/null
sudo chown hugh:hugh "${DIST_FILES_DIR}/version.txt"

echo -e "${GREEN}✓ Installer deployed to: ${DIST_FILES_DIR}/${NC}"

#===============================================================================
# PART 6: Start Distribution Service
#===============================================================================
echo ""
echo -e "${YELLOW}Step 6: Starting distribution service...${NC}"
sudo systemctl start chitterchatter-dist

# Wait briefly and verify
sleep 2
if systemctl is-active --quiet chitterchatter-dist; then
    echo -e "${GREEN}✓ Distribution service started${NC}"
else
    echo -e "${RED}✗ Distribution service failed to start${NC}"
    journalctl -u chitterchatter-dist --no-pager | tail -10
    exit 1
fi

# Verify API is responding
API_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5004/api/health 2>/dev/null || echo "000")
if [ "$API_HEALTH" = "200" ]; then
    echo -e "${GREEN}✓ API health check passed${NC}"
else
    echo -e "${YELLOW}⚠ API health check returned: ${API_HEALTH}${NC}"
fi

#===============================================================================
# Summary
#===============================================================================
echo ""
echo -e "${GREEN}==========================================${NC}"
echo -e "${GREEN}Build & Deploy Complete!${NC}"
echo -e "${GREEN}==========================================${NC}"
echo ""
echo -e "Version:    ${VERSION}"
echo -e "Installer:  ChitterChatter-Setup-${VERSION}.exe (${INSTALLER_SIZE})"
echo -e "Backend:    ${DIST_DEPLOY_DIR}/"
echo -e "Downloads:  ${DIST_FILES_DIR}/"
echo ""
echo -e "Download URL: ${BLUE}https://longmanrd.net/infoforum/downloads/${NC}"
echo ""

# List current installers
echo -e "Available installers:"
ls -lh "${DIST_FILES_DIR}"/ChitterChatter-Setup-*.exe 2>/dev/null | awk '{print "  " $9 " (" $5 ")"}'