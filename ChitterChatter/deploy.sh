#!/bin/bash
#===============================================================================
# ChitterChatter Client Build & Deploy Script
#
# Builds the Windows client, creates an installer using Wine + Inno Setup,
# and deploys to the distribution server.
#
# Prerequisites:
#   - .NET SDK 10.0+
#   - Wine installed (sudo dnf install wine)
#   - Inno Setup installed under Wine (see setup instructions below)
#
# Inno Setup Installation (one-time):
#   1. Download from https://jrsoftware.org/isdl.php
#   2. Run: wine ~/Downloads/innosetup-6.x.x.exe
#   3. Install to default location
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
ISS_FILE="${SCRIPT_DIR}/ChitterChatter-Setup.iss"
DIST_DIR="/var/www/chitterchatter-dist/dist"

# Wine paths - adjust if your Inno Setup is installed elsewhere
WINE_PREFIX="${WINEPREFIX:-$HOME/.wine}"
INNO_SETUP="${WINE_PREFIX}/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe"

# Alternative path if installed to Program Files (not x86)
if [ ! -f "$INNO_SETUP" ]; then
    INNO_SETUP="${WINE_PREFIX}/drive_c/Program Files/Inno Setup 6/ISCC.exe"
fi

echo -e "${GREEN}==========================================${NC}"
echo -e "${GREEN}ChitterChatter Client Build & Deploy${NC}"
echo -e "${GREEN}Version: ${VERSION}${NC}"
echo -e "${GREEN}==========================================${NC}"

# Verify prerequisites
echo -e "${BLUE}Checking prerequisites...${NC}"

if [ ! -d "$CLIENT_DIR" ]; then
    echo -e "${RED}Error: Client directory not found at: ${CLIENT_DIR}${NC}"
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
    echo -e "  2. Run: wine ~/Downloads/innosetup-6.x.x.exe"
    exit 1
fi

echo -e "${GREEN}✓ All prerequisites met${NC}"

# Step 1: Clean previous build
echo ""
echo -e "${YELLOW}Step 1: Cleaning previous build...${NC}"
rm -rf "${CLIENT_DIR}/bin/Release"
rm -rf "${CLIENT_DIR}/obj/Release"
rm -rf "${SCRIPT_DIR}/Output"

# Step 2: Build the client
echo ""
echo -e "${YELLOW}Step 2: Building ChitterChatter client for Windows...${NC}"
cd "$CLIENT_DIR"

dotnet publish -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=false \
    -p:IncludeNativeLibrariesForSelfExtract=false \
    -p:Version="${VERSION}" \
    -p:AssemblyVersion="${VERSION}" \
    -p:FileVersion="${VERSION}"

# Find the publish directory - try various possible paths
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
    echo -e "${RED}Error: Publish directory not found${NC}"
    echo -e "${YELLOW}Searched in: ${CLIENT_DIR}/bin/Release/*/win-x64/publish${NC}"
    ls -la "${CLIENT_DIR}/bin/Release/" 2>/dev/null || true
    exit 1
fi

echo -e "${GREEN}✓ Client built successfully${NC}"
echo -e "  Published to: ${PUBLISH_DIR}"

# Step 3: Create installer with Inno Setup
echo ""
echo -e "${YELLOW}Step 3: Creating Windows installer with Inno Setup...${NC}"
cd "$SCRIPT_DIR"
mkdir -p Output

# Convert Linux paths to Windows paths for Wine
PUBLISH_DIR_WIN=$(winepath -w "$PUBLISH_DIR" 2>/dev/null || echo "Z:${PUBLISH_DIR}")
ISS_FILE_WIN=$(winepath -w "$ISS_FILE" 2>/dev/null || echo "Z:${ISS_FILE}")
OUTPUT_DIR_WIN=$(winepath -w "${SCRIPT_DIR}/Output" 2>/dev/null || echo "Z:${SCRIPT_DIR}/Output")

# Set environment variables for Inno Setup script
export CHITTERCHATTER_VERSION="$VERSION"
export CHITTERCHATTER_SOURCE="$PUBLISH_DIR_WIN"

# Run Inno Setup compiler
echo -e "  Running Inno Setup..."
wine "$INNO_SETUP" \
    "/DMyAppVersion=${VERSION}" \
    "/DSourcePath=${PUBLISH_DIR_WIN}" \
    "/O${OUTPUT_DIR_WIN}" \
    "$ISS_FILE_WIN"

INSTALLER_FILE="${SCRIPT_DIR}/Output/ChitterChatter-Setup-${VERSION}.exe"

if [ ! -f "$INSTALLER_FILE" ]; then
    echo -e "${RED}Error: Installer was not created${NC}"
    exit 1
fi

INSTALLER_SIZE=$(du -h "$INSTALLER_FILE" | cut -f1)
echo -e "${GREEN}✓ Installer created: ChitterChatter-Setup-${VERSION}.exe (${INSTALLER_SIZE})${NC}"

# Step 4: Deploy to distribution folder
echo ""
echo -e "${YELLOW}Step 4: Deploying to distribution folder...${NC}"

# Create dist directory if it doesn't exist
sudo mkdir -p "$DIST_DIR"

# Remove old installers (keep last 3 versions for rollback)
echo -e "  Cleaning old installers (keeping last 3)..."
cd "$DIST_DIR"
ls -t ChitterChatter-Setup-*.exe 2>/dev/null | tail -n +4 | xargs -r sudo rm -f

# Copy new installer
sudo cp "$INSTALLER_FILE" "$DIST_DIR/"
sudo chown hugh:hugh "${DIST_DIR}/ChitterChatter-Setup-${VERSION}.exe"
sudo chmod 644 "${DIST_DIR}/ChitterChatter-Setup-${VERSION}.exe"

# Update version file
echo "$VERSION" | sudo tee "${DIST_DIR}/version.txt" > /dev/null
sudo chown hugh:hugh "${DIST_DIR}/version.txt"

echo -e "${GREEN}✓ Deployed to: ${DIST_DIR}/ChitterChatter-Setup-${VERSION}.exe${NC}"

# Step 5: Restart distribution service (if running)
echo ""
echo -e "${YELLOW}Step 5: Restarting distribution service...${NC}"
if systemctl is-active --quiet chitterchatter-dist; then
    sudo systemctl restart chitterchatter-dist
    echo -e "${GREEN}✓ Distribution service restarted${NC}"
else
    echo -e "${YELLOW}Distribution service not running - skipping restart${NC}"
fi

# Summary
echo ""
echo -e "${GREEN}==========================================${NC}"
echo -e "${GREEN}Build & Deploy Complete!${NC}"
echo -e "${GREEN}==========================================${NC}"
echo ""
echo -e "Version:    ${VERSION}"
echo -e "Installer:  ChitterChatter-Setup-${VERSION}.exe"
echo -e "Size:       ${INSTALLER_SIZE}"
echo -e "Location:   ${DIST_DIR}/"
echo ""
echo -e "Download URL: ${BLUE}https://longmanrd.net/infoforum/downloads/${NC}"
echo ""

# List current installers
echo -e "Available installers:"
ls -lh "${DIST_DIR}"/ChitterChatter-Setup-*.exe 2>/dev/null | awk '{print "  " $9 " (" $5 ")"}'