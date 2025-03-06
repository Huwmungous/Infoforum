#!/bin/bash

# Deployment Script for Deepseek GUI

# Configuration Variables
APP_NAME="deepseek-gui"
LIB_NAME="ifauth-lib"
DEPLOY_PATH="/var/www/deepseek-gui"
LIB_PACKAGE_JSON="projects/ifauth-lib/package.json"
LIB_PATH="projects/ifauth-lib"

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Error handling function
handle_error() {
    echo -e "${RED}Error: $1${NC}"
    exit 1
}

# Git pull from origin main
git_pull() {
    echo -e "${YELLOW}Pulling latest changes from origin main...${NC}"
    
    # Fetch and pull
    git fetch origin main
    git pull origin main || handle_error "Git pull failed"
}

# Check for library changes
check_library_changes() {
    echo -e "${YELLOW}Checking for library changes...${NC}"
    
    # Get the latest commit hash that touched the library
    local last_commit=$(git log -n 1 --pretty=format:%H -- "$LIB_PATH")
    
    # Get the last commit hash from a marker file (create if doesn't exist)
    local marker_file=".last_lib_commit"
    [ -f "$marker_file" ] || echo "" > "$marker_file"
    local previous_commit=$(cat "$marker_file")
    
    # Compare commits
    if [ "$last_commit" != "$previous_commit" ]; then
        echo -e "${GREEN}Library changes detected. Version update needed.${NC}"
        # Update the marker file with the latest commit hash
        echo "$last_commit" > "$marker_file"
        return 0  # Changes detected
    else
        echo -e "${YELLOW}No library changes detected. Version update not needed.${NC}"
        return 1  # No changes detected
    fi
}

# Increment library version
increment_library_version() {
    echo -e "${YELLOW}Incrementing library version...${NC}"
    
    # Get current version
    current_version=$(grep -m1 '"version":' "$LIB_PACKAGE_JSON" | sed -E 's/.*"version": "(.*)".*/\1/')

    # Increment version
    new_version=$(echo $current_version | awk -F. '{$NF = $NF + 1;} 1' OFS=.)

    # Update package.json
    sed -i "s/\"version\": \"$current_version\"/\"version\": \"$new_version\"/" "$LIB_PACKAGE_JSON"

    echo -e "${GREEN}Updated library version from $current_version to $new_version${NC}"
}

# Git commit and push
git_commit_and_push() {
    echo -e "${YELLOW}Committing and pushing changes...${NC}"
    
    # Stage changes
    git add "$LIB_PACKAGE_JSON" ".last_lib_commit"
    
    # Commit with version increment message
    git commit -m "Increment library version for deployment" || handle_error "Git commit failed"
    
    # Push changes
    git push origin main || handle_error "Git push failed"
}

# Rebuild library to ensure latest changes
rebuild_library() {
    echo -e "${YELLOW}Rebuilding library to ensure latest changes...${NC}"
    
    # Clean library dist and rebuild
    ng build $LIB_NAME --configuration=production || handle_error "Library rebuild failed"
}

# Build application
build_application() {
    echo -e "${YELLOW}Building application...${NC}"
    ng build $APP_NAME --configuration=production || handle_error "Application build failed"
}

# Deploy application
deploy_application() {
    echo -e "${YELLOW}Deploying application...${NC}"

    # Create deployment directory if it doesn't exist
    sudo mkdir -p $DEPLOY_PATH

    echo -e "${YELLOW}Cleaning target ${DEPLOY_PATH}.${NC}"
    sudo rm -rf $DEPLOY_PATH/*

    # Copy build artifacts
    sudo cp -R ../../dist/$APP_NAME/* $DEPLOY_PATH/

    # Set correct permissions
    sudo chown -R nginx:nginx $DEPLOY_PATH
    sudo chmod -R 755 $DEPLOY_PATH
}

# Main deployment workflow
main() {
    git_pull
    
    # Only increment version if library has changed
    if check_library_changes; then
        increment_library_version
        git_commit_and_push
    fi
    
    rebuild_library
    build_application
    deploy_application
    echo -e "${GREEN}Deployment completed successfully!${NC}"
}

# Run the deployment
main
