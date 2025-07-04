#!/bin/bash

# Deployment Script for Deepseek GUI

# Resolve script directory absolute path
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Configuration Variables
APP_NAME="deepseek-gui"
DEPLOY_PATH="/var/www/deepseek-gui"
ENV_PROD="$SCRIPT_DIR/src/environments/environment.prod.ts"
GIT_TAG_PREFIX="v"

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

increment_version_in_env() {
    echo -e "${YELLOW}Incrementing version in environment.prod.ts...${NC}"

    if [ ! -f "$ENV_PROD" ]; then
        handle_error "File $ENV_PROD not found"
    fi

    # Extract current version (handles single quotes around the version)
    current_version=$(grep -oP "(?<=version:\s*')[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+" "$ENV_PROD" || echo "1.0.0.0")

    # Parse parts
    IFS='.' read -r major minor patch build <<< "$current_version"
    if [ -z "$build" ]; then
        build=0
    fi
    build=$((build + 1))

    new_version="${major}.${minor}.${patch}.${build}"

    # Replace version line in file
    sed -i -E "s/version:\s*'[^']+'/version: '$new_version'/" "$ENV_PROD"

    echo -e "${GREEN}Version updated: $current_version -> $new_version${NC}"
}

commit_version_and_tag() {
    echo -e "${YELLOW}Committing environment.prod.ts and tagging git...${NC}"

    git add "$ENV_PROD"

    # Commit only if there are staged changes (avoid failing on no changes)
    if ! git diff --cached --quiet; then
        git commit -m "Bump version to $new_version" || handle_error "Git commit failed"
        git push origin main || handle_error "Git push failed"

        tag_name="${GIT_TAG_PREFIX}${new_version}"
        git tag -a "$tag_name" -m "Version $new_version"
        git push origin "$tag_name" || handle_error "Git tag push failed"

        echo -e "${GREEN}Git tagged with $tag_name${NC}"
    else
        echo "No changes to commit."
    fi
}

build_application() {
    echo -e "${YELLOW}Building application...${NC}"
    cd "$SCRIPT_DIR/../.."
    ng build $APP_NAME --configuration=production --base-href=/intelligence/ || handle_error "Application build failed"
    cd "$SCRIPT_DIR" # back to script directory
}

generate_prod_index() {
    echo -e "${YELLOW}Generating dynamic production index.html...${NC}"

    BUILD_OUTPUT="$SCRIPT_DIR/../../dist/$APP_NAME/browser"
    PROD_INDEX="$SCRIPT_DIR/src/index.prod.html"

    shopt -s nullglob
    polyfills_files=($BUILD_OUTPUT/polyfills-*.js)
    main_files=($BUILD_OUTPUT/main-*.js)
    styles_files=($BUILD_OUTPUT/styles-*.css)
    shopt -u nullglob

    if [ ${#polyfills_files[@]} -eq 0 ] || [ ${#main_files[@]} -eq 0 ] || [ ${#styles_files[@]} -eq 0 ]; then
        handle_error "One or more bundles (polyfills, main, styles) not found in build output."
    fi

    POLYFILLS=$(basename "${polyfills_files[0]}")
    MAINJS=$(basename "${main_files[0]}")
    STYLESCSS=$(basename "${styles_files[0]}")

    echo "Detected bundles:"
    echo "Polyfills: $POLYFILLS"
    echo "Main JS: $MAINJS"
    echo "Styles CSS: $STYLESCSS"

    if [ ! -f "$PROD_INDEX" ]; then
        handle_error "Production index.html template not found at $PROD_INDEX"
    fi

    GENERATED_INDEX="$BUILD_OUTPUT/index.html"

    sed \
        -e "s|%%POLYFILLS_JS%%|$POLYFILLS|g" \
        -e "s|%%MAIN_JS%%|$MAINJS|g" \
        -e "s|%%STYLES_CSS%%|$STYLESCSS|g" \
        "$PROD_INDEX" > "$GENERATED_INDEX" || handle_error "Failed to generate index.html"

    echo -e "${GREEN}index.html generated successfully.${NC}"

    echo "Checking favicon line in generated index.html:"
    grep -i 'favicon.ico' "$GENERATED_INDEX" || echo "No favicon line found!"
}

deploy_application() {
    echo -e "${YELLOW}Deploying application...${NC}"
    sudo mkdir -p "$DEPLOY_PATH/browser"
    echo -e "${YELLOW}Cleaning target ${DEPLOY_PATH}/browser.${NC}"
    sudo rm -rf "$DEPLOY_PATH/browser"/*
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
    increment_version_in_env
    build_application
    generate_prod_index
    deploy_application
    commit_version_and_tag
    git checkout --force
    chmod 755 "$SCRIPT_DIR/deploy.sh"
    echo -e "${GREEN}Deployment completed successfully!${NC}"
}

main
