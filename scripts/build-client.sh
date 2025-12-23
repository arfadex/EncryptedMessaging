#!/bin/bash
set -e

SCRIPT_DIR="$(dirname "$0")"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

echo "Building EncryptedMessaging client binaries..."

rm -rf dist
mkdir -p dist

echo "Building Linux x64..."
dotnet publish client/EncryptedMessaging.Client.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o dist/linux-x64

echo "Building Windows x64..."
dotnet publish client/EncryptedMessaging.Client.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o dist/win-x64

echo ""
echo "Done! Binaries:"
echo "  Linux:   dist/linux-x64/EncryptedMessaging"
echo "  Windows: dist/win-x64/EncryptedMessaging.exe"
echo ""
echo "Or run directly with .NET SDK:"
echo "  cd client && dotnet run"
