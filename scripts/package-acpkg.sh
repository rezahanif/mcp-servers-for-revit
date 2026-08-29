#!/usr/bin/env bash
# Shim to the shared AiConnect packager. Do NOT add connector-specific logic here:
# seven divergent copies of this script is what produced N27 (sap2000 silently shipped
# without scripts/, leaving five tools inert) and the revit entry-path mismatch.
# Declare payload in this connector's manifest.json under "package":
#     "package": { "include": ["run_server.py", "mcp_server"], "exclude": [] }
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
AICONNECT_ROOT="${AICONNECT_ROOT:-$(cd "$ROOT_DIR/../aiconnector" 2>/dev/null && pwd || true)}"
SHARED="$AICONNECT_ROOT/scripts/release/package-acpkg.py"
if [ ! -f "$SHARED" ]; then
  echo "error: shared packager not found at '$SHARED'." >&2
  echo "       Set AICONNECT_ROOT to the aiconnector checkout." >&2
  exit 1
fi
exec python3 "$SHARED" "$ROOT_DIR" "$@"
