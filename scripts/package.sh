#!/usr/bin/env bash
# Deterministic connector package build for revit-mcp 1.0.0.
# Produces dist/revit-mcp-1.0.0.zip + dist/revit-mcp-1.0.0.zip.sha256.
set -euo pipefail
cd "$(dirname "$0")/.."

VER="1.0.0"
DIST="dist"
ROOT="$DIST/revit-mcp"
VERSIONS="2023 2024 2025"

rm -rf "$DIST"
mkdir -p "$ROOT/build" "$ROOT/node_modules" "$ROOT/src" "$ROOT/plugin-layout"

# 1. server build (adapted AiConnect server: hoststate + license gate)
(cd server && npm ci --silent && npm run build)

# 2. copy runtime surface
cp manifest.json "$ROOT/manifest.json"
cp server/build/index.js "$ROOT/build/index.js"
cp -r server/node_modules/. "$ROOT/node_modules/"
cp server/package.json server/package-lock.json "$ROOT/"
cp server/tsconfig.json "$ROOT/"
cp -r server/src/. "$ROOT/src/"
cp LICENSE README.md command.json "$ROOT/"

# 3. Revit host plugin zip (one layout per supported version)
for V in $VERSIONS; do
  RV="R${V: -2}"
  mkdir -p "$ROOT/plugin-layout/$V"
  cp -r "plugin/bin/AddIn $V Release $RV"/* "$ROOT/plugin-layout/$V/"
done
(cd "$ROOT/plugin-layout" && python3 -m zipfile -c ../RevitMCPPlugin.zip .)
rm -rf "$ROOT/plugin-layout"

# 4. final zip + digest
(cd "$DIST" && python3 -m zipfile -c revit-mcp-$VER.zip revit-mcp)
shasum -a 256 "$DIST/revit-mcp-$VER.zip" | awk '{print $1}' > "$DIST/revit-mcp-$VER.zip.sha256"

echo "built $DIST/revit-mcp-$VER.zip ($(du -h "$DIST/revit-mcp-$VER.zip" | cut -f1))"
echo "sha256: $(cat "$DIST/revit-mcp-$VER.zip.sha256")"
