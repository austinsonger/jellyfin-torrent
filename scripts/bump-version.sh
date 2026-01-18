#!/bin/bash

# Version Bump Script for Jellyfin Plugin TorrentDownloader
# Updates version numbers consistently across all project files

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CSPROJ_FILE="$PROJECT_ROOT/Jellyfin.Plugin.TorrentDownloader/Jellyfin.Plugin.TorrentDownloader.csproj"
MANIFEST_FILE="$PROJECT_ROOT/manifest.json"

# Check for required tools
command -v jq >/dev/null 2>&1 || { echo -e "${RED}Error: jq is required but not installed. Install with: brew install jq${NC}" >&2; exit 1; }

# Function to display usage
usage() {
    cat << EOF
Usage: $0 [VERSION] [OPTIONS]

Update version numbers across all project files.

Arguments:
    VERSION         Version number in format X.Y.Z.W (e.g., 1.0.6.0)
                    If not provided with increment flags, current version is shown

Options:
    --major         Increment major version (X.0.0.0)
    --minor         Increment minor version (X.Y.0.0)
    --patch         Increment patch version (X.Y.Z.0)
    --build         Increment build version (X.Y.Z.W)
    --dry-run       Show what would change without modifying files
    --commit        Auto-commit changes with conventional commit message
    --tag           Create git tag for the new version (requires --commit)
    -h, --help      Display this help message

Examples:
    $0 1.0.6.0              # Set version to 1.0.6.0
    $0 --patch              # Increment patch: 1.0.5.0 -> 1.0.6.0
    $0 --minor              # Increment minor: 1.0.5.0 -> 1.1.0.0
    $0 2.0.0.0 --dry-run    # Preview changes for version 2.0.0.0
    $0 --patch --commit     # Bump patch and commit changes
    $0 --patch --commit --tag  # Bump, commit, and tag

EOF
    exit 1
}

# Parse arguments
NEW_VERSION=""
INCREMENT_TYPE=""
DRY_RUN=false
AUTO_COMMIT=false
AUTO_TAG=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --major)
            INCREMENT_TYPE="major"
            shift
            ;;
        --minor)
            INCREMENT_TYPE="minor"
            shift
            ;;
        --patch)
            INCREMENT_TYPE="patch"
            shift
            ;;
        --build)
            INCREMENT_TYPE="build"
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --commit)
            AUTO_COMMIT=true
            shift
            ;;
        --tag)
            AUTO_TAG=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            if [[ -z "$NEW_VERSION" ]]; then
                NEW_VERSION="$1"
            else
                echo -e "${RED}Error: Unknown option or multiple versions specified: $1${NC}"
                usage
            fi
            shift
            ;;
    esac
done

# Validate files exist
if [[ ! -f "$CSPROJ_FILE" ]]; then
    echo -e "${RED}Error: Project file not found: $CSPROJ_FILE${NC}"
    exit 1
fi

if [[ ! -f "$MANIFEST_FILE" ]]; then
    echo -e "${RED}Error: Manifest file not found: $MANIFEST_FILE${NC}"
    exit 1
fi

# Extract current version from project file
CURRENT_VERSION=$(grep -o '<Version>[^<]*</Version>' "$CSPROJ_FILE" | sed 's/<Version>\(.*\)<\/Version>/\1/')

if [[ -z "$CURRENT_VERSION" ]]; then
    echo -e "${RED}Error: Could not extract current version from project file${NC}"
    exit 1
fi

echo -e "${GREEN}Current version: $CURRENT_VERSION${NC}"

# Calculate new version if increment flag is used
if [[ -n "$INCREMENT_TYPE" ]]; then
    IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"
    MAJOR="${VERSION_PARTS[0]}"
    MINOR="${VERSION_PARTS[1]}"
    PATCH="${VERSION_PARTS[2]}"
    BUILD="${VERSION_PARTS[3]}"

    case "$INCREMENT_TYPE" in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            BUILD=0
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            BUILD=0
            ;;
        patch)
            PATCH=$((PATCH + 1))
            BUILD=0
            ;;
        build)
            BUILD=$((BUILD + 1))
            ;;
    esac

    NEW_VERSION="$MAJOR.$MINOR.$PATCH.$BUILD"
    echo -e "${YELLOW}Incrementing $INCREMENT_TYPE version${NC}"
fi

# If no version specified and no increment flag, show current and exit
if [[ -z "$NEW_VERSION" ]]; then
    echo -e "${YELLOW}No version specified. Use --patch, --minor, --major, or --build to increment${NC}"
    exit 0
fi

# Validate version format
if ! [[ "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}Error: Invalid version format. Expected X.Y.Z.W (e.g., 1.0.6.0)${NC}"
    exit 1
fi

echo -e "${GREEN}New version: $NEW_VERSION${NC}"

# Dry run mode
if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY RUN] Would update:${NC}"
    echo "  - $CSPROJ_FILE: $CURRENT_VERSION -> $NEW_VERSION"
    echo "  - $MANIFEST_FILE: version and URL fields -> $NEW_VERSION"
    exit 0
fi

# Create backups
CSPROJ_BACKUP="${CSPROJ_FILE}.bak"
MANIFEST_BACKUP="${MANIFEST_FILE}.bak"

cp "$CSPROJ_FILE" "$CSPROJ_BACKUP"
cp "$MANIFEST_FILE" "$MANIFEST_BACKUP"

# Update project file
echo -e "${YELLOW}Updating $CSPROJ_FILE...${NC}"
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS sed syntax
    sed -i '' "s|<Version>$CURRENT_VERSION</Version>|<Version>$NEW_VERSION</Version>|g" "$CSPROJ_FILE"
else
    # Linux sed syntax
    sed -i "s|<Version>$CURRENT_VERSION</Version>|<Version>$NEW_VERSION</Version>|g" "$CSPROJ_FILE"
fi

# Update manifest.json
echo -e "${YELLOW}Updating $MANIFEST_FILE...${NC}"
TEMP_MANIFEST="${MANIFEST_FILE}.tmp"

jq --arg newver "$NEW_VERSION" '
  .version = $newver |
  .versions[].version = $newver |
  .versions[].sourceUrl |= gsub("v[0-9]+\\.[0-9]+\\.[0-9]+(\\.[0-9]+)?"; "v" + $newver)
' "$MANIFEST_FILE" > "$TEMP_MANIFEST"

mv "$TEMP_MANIFEST" "$MANIFEST_FILE"

# Verify changes
VERIFY_CSPROJ=$(grep -o '<Version>[^<]*</Version>' "$CSPROJ_FILE" | sed 's/<Version>\(.*\)<\/Version>/\1/')
VERIFY_MANIFEST=$(jq -r '.version' "$MANIFEST_FILE")
VERIFY_MANIFEST_VERSIONS=$(jq -r '.versions[0].version' "$MANIFEST_FILE")

if [[ "$VERIFY_CSPROJ" != "$NEW_VERSION" ]] || [[ "$VERIFY_MANIFEST" != "$NEW_VERSION" ]] || [[ "$VERIFY_MANIFEST_VERSIONS" != "$NEW_VERSION" ]]; then
    echo -e "${RED}Error: Version verification failed!${NC}"
    echo "  Project file: $VERIFY_CSPROJ (expected: $NEW_VERSION)"
    echo "  Manifest root: $VERIFY_MANIFEST (expected: $NEW_VERSION)"
    echo "  Manifest versions: $VERIFY_MANIFEST_VERSIONS (expected: $NEW_VERSION)"
    
    # Restore backups
    echo -e "${YELLOW}Restoring backups...${NC}"
    mv "$CSPROJ_BACKUP" "$CSPROJ_FILE"
    mv "$MANIFEST_BACKUP" "$MANIFEST_FILE"
    exit 1
fi

# Remove backups on success
rm -f "$CSPROJ_BACKUP" "$MANIFEST_BACKUP"

echo -e "${GREEN}✓ Successfully updated version to $NEW_VERSION${NC}"
echo -e "${GREEN}Files modified:${NC}"
echo "  - $CSPROJ_FILE"
echo "  - $MANIFEST_FILE"

# Git integration
if [[ "$AUTO_COMMIT" == true ]]; then
    if ! command -v git >/dev/null 2>&1; then
        echo -e "${YELLOW}Warning: git not found, skipping commit${NC}"
    else
        echo -e "${YELLOW}Committing changes...${NC}"
        git add "$CSPROJ_FILE" "$MANIFEST_FILE"
        git commit -m "chore: bump version to $NEW_VERSION"
        echo -e "${GREEN}✓ Changes committed${NC}"

        if [[ "$AUTO_TAG" == true ]]; then
            TAG_NAME="v$NEW_VERSION"
            echo -e "${YELLOW}Creating git tag: $TAG_NAME${NC}"
            git tag "$TAG_NAME"
            echo -e "${GREEN}✓ Tag created: $TAG_NAME${NC}"
            echo -e "${YELLOW}Push with: git push origin main --tags${NC}"
        fi
    fi
fi

echo ""
echo -e "${GREEN}Next steps:${NC}"
if [[ "$AUTO_COMMIT" == false ]]; then
    echo "  1. Review changes: git diff"
    echo "  2. Commit: git add . && git commit -m 'chore: bump version to $NEW_VERSION'"
    echo "  3. Tag: git tag v$NEW_VERSION"
    echo "  4. Push: git push origin main --tags"
elif [[ "$AUTO_TAG" == false ]]; then
    echo "  1. Tag: git tag v$NEW_VERSION"
    echo "  2. Push: git push origin main --tags"
fi
echo "  - CI/CD will build and create release automatically"
echo "  - After release, update manifest.json checksum with the actual MD5"
