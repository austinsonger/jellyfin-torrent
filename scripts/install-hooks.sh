#!/bin/bash

# Git Hooks Installation Script
# Installs pre-commit hook for version validation

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HOOK_SOURCE="$SCRIPT_DIR/pre-commit"
HOOK_DEST="$GIT_ROOT/.git/hooks/pre-commit"

echo -e "${GREEN}Installing Git hooks for Jellyfin Plugin TorrentDownloader${NC}"

# Check if we're in a git repository
if [[ ! -d "$GIT_ROOT/.git" ]]; then
    echo -e "${RED}Error: Not in a git repository${NC}"
    exit 1
fi

# Check if hook source exists
if [[ ! -f "$HOOK_SOURCE" ]]; then
    echo -e "${RED}Error: Hook source not found: $HOOK_SOURCE${NC}"
    exit 1
fi

# Check if hook already exists
if [[ -f "$HOOK_DEST" ]]; then
    echo -e "${YELLOW}Pre-commit hook already exists at $HOOK_DEST${NC}"
    read -p "Overwrite? (y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Installation cancelled${NC}"
        exit 0
    fi
    
    # Backup existing hook
    BACKUP_PATH="${HOOK_DEST}.backup.$(date +%Y%m%d%H%M%S)"
    echo -e "${YELLOW}Backing up existing hook to: $BACKUP_PATH${NC}"
    cp "$HOOK_DEST" "$BACKUP_PATH"
fi

# Create hooks directory if it doesn't exist
mkdir -p "$GIT_ROOT/.git/hooks"

# Install hook
echo -e "${YELLOW}Installing pre-commit hook...${NC}"
cp "$HOOK_SOURCE" "$HOOK_DEST"
chmod +x "$HOOK_DEST"

# Verify installation
if [[ -x "$HOOK_DEST" ]]; then
    echo -e "${GREEN}✓ Pre-commit hook installed successfully${NC}"
    echo -e "${GREEN}Location: $HOOK_DEST${NC}"
    echo ""
    echo -e "${GREEN}The hook will:${NC}"
    echo "  - Validate version consistency before each commit"
    echo "  - Block commits with mismatched versions"
    echo "  - Guide you to fix version issues"
    echo ""
    echo -e "${YELLOW}To bypass the hook (not recommended):${NC}"
    echo "  git commit --no-verify"
else
    echo -e "${RED}Error: Failed to install hook${NC}"
    exit 1
fi
