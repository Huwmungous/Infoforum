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
ENV_PROD_PATH="$SCRIPT_DIR/src/environments/environment.prod.ts"

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
    echo "$new_version"  # return new version
}

git_commit_and_push() {
    echo -e "${YELLOW}Committing and pushing changes...${NC}"
    git add "$LIB_PACKAGE_JSON" "$SCRIPT_DIR/.last_lib_commit"
    git commit -m "Increment library version for deployment" || handle_error "Git commit failed"
    git push origin main || handle_error "Git push failed"
}

update_environment_version() {
    local version="$1"
    echo -e "${YELLOW}Updating environment.prod.ts with version $version ...${NC}"

    cat > "$ENV_PROD_PATH" << EOF
export const environment = {
  production: true,
  apiUrl: 'https://longmanrd.net/aiapi',
  consoleLog: true,
  appName: '/intelligence/',
  realm: 'LongmanRd',
  clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7',
  version: '$version'
};
EOF
    git add "$ENV_PROD_PATH"
    git commit -m "Update environment.prod.ts with version $version" || handle_error "Git commit failed for environment.prod.ts"
    git push origin main || handle_error "Git push failed for environment.prod.ts"
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

    local POLYFILLS=$(basename "${polyfills_files[0]}")
    local MAINJS=$(basename "${main_files[0]}")
    local STYLESCSS=$(basename "${styles_files[0]}")

    echo "Detected bundles:"
    echo "Polyfills: $POLYFILLS"
    echo "Main JS: $MAINJS"
    echo "Styles CSS: $STYLESCSS"

    if [ ! -f "$PROD_INDEX" ]; then
        handle_error "Production index.html template not found at $PROD_INDEX"
    fi

    local GENERATED_INDEX="$BUILD_OUTPUT/index.html"

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

    # Optionally, rebuild the library if needed:
    # check_library_changes && {
    #    increment_library_version
    #    rebuild_library
    # }

    build_application
    generate_prod_index
    deploy_application

    # Increment version and commit/tag after successful deploy
    NEW_VERSION=$(increment_library_version)
    git_commit_and_push

    update_environment_version "$NEW_VERSION"

    # Tag the commit with the version
    git tag -a "v$NEW_VERSION" -m "Deployment version $NEW_VERSION" || handle_error "Git tag failed"
    git push origin "v$NEW_VERSION" || handle_error "Git push tag failed"

    git checkout --force
    chmod 755 "$SCRIPT_DIR/deploy.sh"
    echo -e "${GREEN}Deployment completed successfully! Version: $NEW_VERSION${NC}"
}

main
