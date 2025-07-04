#!/bin/bash

# Deployment Script for Deepseek GUI

# Resolve script directory absolute path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Configuration Variables
APP_NAME="deepseek-gui"
LIB_NAME="ifauth-lib"
DEPLOY_PATH="/var/www/deepseek-gui"
LIB_PACKAGE_JSON="$SCRIPT_DIR/../ifauth-lib/package.json"
LIB_PATH="$SCRIPT_DIR/../ifauth-lib"

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

handle_error() {
    echo -e "${RED}Error: $1${NC}"
    exit 1
}

git_pull() {
    echo -e "${YELLOW}Pulling latest changes from origin main...${NC}"
    git fetch origin main
    git pull origin main || handle_error "Git pull failed"
}

check_library_changes() {
    echo -e "${YELLOW}Checking for library changes...${NC}"
    local last_commit=$(git log -n 1 --pretty=format:%H -- "$LIB_PATH")
    local marker_file="$SCRIPT_DIR/.last_lib_commit"
    [ -f "$marker_file" ] || echo "" > "$marker_file"
    local previous_commit=$(cat "$marker_file")

    if [ "$last_commit" != "$previous_commit" ]; then
        echo -e "${GREEN}Library changes detected. Version update needed.${NC}"
        echo "$last_commit" > "$marker_file"
        return 0
    else
        echo -e "${YELLOW}No library changes detected. Version update not needed.${NC}"
        return 1
    fi
}

increment_library_version() {
    echo -e "${YELLOW}Incrementing library version...${NC}"
    current_version=$(grep -m1 '"version":' "$LIB_PACKAGE_JSON" | sed -E 's/.*"version": "(.*)".*/\1/')
    new_version=$(echo $current_version | awk -F. '{$NF = $NF + 1;} 1' OFS=.)
    sed -i "s/\"version\": \"$current_version\"/\"version\": \"$new_version\"/" "$LIB_PACKAGE_JSON"
    echo -e "${GREEN}Updated library version from $current_version to $new_version${NC}"
}

git_commit_and_push() {
    echo -e "${YELLOW}Committing and pushing changes...${NC}"
    git add "$LIB_PACKAGE_JSON" "$SCRIPT_DIR/.last_lib_commit"
    git commit -m "Increment library version for deployment" || handle_error "Git commit failed"
    git push origin main || handle_error "Git push failed"
}

rebuild_library() {
    echo -e "${YELLOW}Rebuilding library to ensure latest changes...${NC}"
    ng build $LIB_NAME --configuration=production || handle_error "Library rebuild failed"
}

build_application() {
    echo -e "${YELLOW}Building application...${NC}"
    cd "$SCRIPT_DIR/../.."
    ng build $APP_NAME --configuration=production --base-href=/intelligence/ || handle_error "Application build failed"
    cd "$SCRIPT_DIR" # back to script directory (projects/deepseek-gui)
}

replace_index_html() {
    echo -e "${YELLOW}Replacing index.html with production version...${NC}"
    PROD_INDEX="$SCRIPT_DIR/src/index.prod.html"
    BUILD_OUTPUT="$SCRIPT_DIR/../../dist/$APP_NAME/browser"

    echo "Prod index path: $PROD_INDEX"
    echo "Build output path: $BUILD_OUTPUT"

    if [ -f "$PROD_INDEX" ]; then
        cp "$PROD_INDEX" "$BUILD_OUTPUT/index.html" || handle_error "Failed to replace index.html"
        echo -e "${GREEN}index.html replaced successfully.${NC}"
        echo "Checking favicon line in replaced index.html:"
        grep -i 'favicon.ico' "$BUILD_OUTPUT/index.html" || echo "No favicon line found!"
    else
        echo -e "${RED}Production index.html not found at $PROD_INDEX${NC}"
        exit 1
    fi
}

deploy_application() {
    echo -e "${YELLOW}Deploying application...${NC}"
    sudo mkdir -p "$DEPLOY_PATH/browser"
    echo -e "${YELLOW}Cleaning target ${DEPLOY_PATH}/browser.${NC}"
    sudo rm -rf "$DEPLOY_PATH/browser/*"
    sudo cp -R "$SCRIPT_DIR/../../dist/$APP_NAME/browser/"* "$DEPLOY_PATH/browser/"
    sudo chown -R nginx:nginx "$DEPLOY_PATH/browser"
    sudo chmod -R 755 "$DEPLOY_PATH/browser"
}

clean_build() {
    echo -e "${YELLOW}Cleaning build artifacts...${NC}"
    sudo rm -rf "$SCRIPT_DIR/../../dist"
}

main() {
    git_pull
    clean_build
    npm install || handle_error "NPM install failed"
    build_application
    replace_index_html
    deploy_application
    git checkout --force
    chmod 755 "$SCRIPT_DIR/deploy.sh"
    echo -e "${GREEN}Deployment completed successfully!${NC}"
}

main
