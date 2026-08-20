#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist"
PKG_NAME="revit-mcp-1.0.0-windows-x64.acpkg"

echo "=== Packaging AiConnect Revit Connector (.acpkg) ==="
mkdir -p "$DIST_DIR"

STAGE_DIR="$(mktemp -d)"
trap 'rm -rf "$STAGE_DIR"' EXIT

# Copy manifest, marketplace, and tutorial
cp "$ROOT_DIR/manifest.json" "$STAGE_DIR/"
cp "$ROOT_DIR/marketplace.json" "$STAGE_DIR/"
cp "$ROOT_DIR/TUTORIAL.md" "$STAGE_DIR/"

# Copy assets
if [ -d "$ROOT_DIR/assets" ]; then
    mkdir -p "$STAGE_DIR/assets"
    cp -r "$ROOT_DIR/assets/"* "$STAGE_DIR/assets/" || true
fi

# Copy server build
if [ -d "$ROOT_DIR/server/build" ]; then
    mkdir -p "$STAGE_DIR/build"
    cp -r "$ROOT_DIR/server/build/"* "$STAGE_DIR/build/"
fi

# Copy plugins if present
if [ -d "$ROOT_DIR/plugins" ]; then
    mkdir -p "$STAGE_DIR/plugins"
    cp -r "$ROOT_DIR/plugins/"* "$STAGE_DIR/plugins/" || true
fi

# Create .acpkg (ZIP archive)
(cd "$STAGE_DIR" && zip -r "$DIST_DIR/$PKG_NAME" .)

echo "Package created at: $DIST_DIR/$PKG_NAME"
sha256sum "$DIST_DIR/$PKG_NAME"
