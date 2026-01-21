#!/bin/bash

# Script to remove backup folders created by MCP Server deployments
# Backup folders match pattern: *.backup.YYYYMMDD_HHMMSS

DEPLOY_ROOT="/srv/sfddevelopment/MCPServers"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo "======================================"
echo "  MCP Server Backup Cleanup"
echo "======================================"
echo ""

# Find all backup directories
BACKUP_DIRS=$(find "$DEPLOY_ROOT" -maxdepth 1 -type d -name "*.backup.*" 2>/dev/null)

if [ -z "$BACKUP_DIRS" ]; then
    echo -e "${GREEN}No backup directories found.${NC}"
    exit 0
fi

# Count and calculate total size
BACKUP_COUNT=$(echo "$BACKUP_DIRS" | wc -l)
TOTAL_SIZE=$(du -sh "$DEPLOY_ROOT"/*.backup.* 2>/dev/null | awk '{sum+=$1} END {print sum}')

echo -e "${YELLOW}Found $BACKUP_COUNT backup directories:${NC}"
echo ""
ls -lhd "$DEPLOY_ROOT"/*.backup.* 2>/dev/null
echo ""

# Show total size
TOTAL_SIZE_HUMAN=$(du -shc "$DEPLOY_ROOT"/*.backup.* 2>/dev/null | tail -1 | awk '{print $1}')
echo -e "${YELLOW}Total size: $TOTAL_SIZE_HUMAN${NC}"
echo ""

# Ask for confirmation
read -p "Do you want to delete these backup directories? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo -e "${GREEN}Cancelled. No directories were deleted.${NC}"
    exit 0
fi

# Delete backups
echo ""
echo -e "${YELLOW}Deleting backups...${NC}"

for dir in $BACKUP_DIRS; do
    echo "  Removing: $(basename "$dir")"
    sudo rm -rf "$dir"
    if [ $? -eq 0 ]; then
        echo -e "  ${GREEN}✓ Deleted${NC}"
    else
        echo -e "  ${RED}✗ Failed${NC}"
    fi
done

echo ""
echo -e "${GREEN}Cleanup complete!${NC}"
echo ""

# Show remaining backups if any
REMAINING=$(find "$DEPLOY_ROOT" -maxdepth 1 -type d -name "*.backup.*" 2>/dev/null | wc -l)
if [ $REMAINING -gt 0 ]; then
    echo -e "${YELLOW}Warning: $REMAINING backup directories still remain${NC}"
else
    echo -e "${GREEN}All backup directories removed successfully.${NC}"
fi
