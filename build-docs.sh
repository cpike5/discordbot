#!/bin/bash
# Build documentation using DocFX

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Discord Bot Documentation Builder"
echo "================================="

SERVE=false
CLEAN=false

while [[ "$#" -gt 0 ]]; do
    case $1 in
        --serve) SERVE=true ;;
        --clean) CLEAN=true ;;
        *) echo "Unknown parameter: $1"; exit 1 ;;
    esac
    shift
done

echo -e "\nRestoring .NET tools..."
dotnet tool restore

if [ "$CLEAN" = true ]; then
    echo -e "\nCleaning previous build..."
    rm -rf _site
    rm -f api/*.yml 2>/dev/null || true
fi

echo -e "\nBuilding solution..."
dotnet build --configuration Release

if [ "$SERVE" = true ]; then
    echo -e "\nBuilding and serving documentation..."
    dotnet docfx docfx.json --serve
else
    echo -e "\nBuilding documentation..."
    dotnet docfx docfx.json

    echo -e "\nDocumentation built successfully!"
    echo "Output: $SCRIPT_DIR/_site"
    echo -e "\nTo serve locally, run: ./build-docs.sh --serve"
fi
