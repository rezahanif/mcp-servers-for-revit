#!/usr/bin/env python3
"""Vendor the MCP server's runtime dependencies into `server/node_modules/`.

WHY
---
The shipped connector package was source-only: `manifest.package.include` listed
`server/build`, so the user received 317 KB of compiled JavaScript whose very first
statement is `import ... from "@modelcontextprotocol/sdk/..."`, with no `node_modules`
anywhere in the archive. It could not start. The package looked healthy because the four
Revit plugin ZIPs made it 20 MB.

This stages the dependency tree that ships alongside it. It is NOT bundled into a single
file on purpose: every tool module locates its data with
`path.join(__dirname, "revit_api_index.json")` and `database/db.js` reaches
`join(__dirname, '..', '..', 'revit-data.db')`. Bundling rewrites `import.meta.url` to the
bundle's own location and silently breaks both, in different directions. Preserving the
directory layout costs a few MB and removes that whole class of failure.

`revit-data.db` is deliberately NOT shipped: `initializeDatabase()` runs
`CREATE TABLE IF NOT EXISTS` on module load, so the file self-creates with the right schema
next to the installed server.

NATIVE DEPENDENCY
-----------------
`better-sqlite3` is the one native module. The user's machine has no compiler, so the
win32-x64 prebuild is fetched and installed here, pinned by digest. It must match the ABI
of the Node that AI CONNECT bundles (`apps/desktop/stage-runtimes.py`): Node 22 -> ABI 127.
Changing either pin requires changing both.

Run:  python3 scripts/stage_server_deps.py [--force]
"""
from __future__ import annotations

import argparse
import hashlib
import io
import shutil
import subprocess
import sys
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "server"
NODE_MODULES = SERVER / "node_modules"

# Must track apps/desktop/stage-runtimes.py's NODE_VERSION (Node 22 -> module ABI 127).
NODE_ABI = "127"
BS3_VERSION = "12.8.0"
BS3_URL = (
    f"https://github.com/WiseLibs/better-sqlite3/releases/download/v{BS3_VERSION}/"
    f"better-sqlite3-v{BS3_VERSION}-node-v{NODE_ABI}-win32-x64.tar.gz"
)
BS3_SHA256 = "0ec6e77054733797153b290689a313c5b22aae3ff876199e0ea2d202ef4e4784"
BS3_MEMBER = "build/Release/better_sqlite3.node"

# Removed after install. All of it is build- or editor-time only; none of it is imported.
PRUNE_SUFFIXES = {".md", ".markdown", ".map", ".ts"}   # .ts covers .d.ts type declarations
PRUNE_TREES = ("better-sqlite3/deps", "better-sqlite3/src")  # C sources; we ship a prebuild


def npm_install() -> None:
    print("installing production dependencies (npm ci --omit=dev --ignore-scripts)")
    # --ignore-scripts: better-sqlite3's install script would try to COMPILE for this
    # host. We want the win32-x64 prebuild instead, installed below.
    r = subprocess.run(
        ["npm", "ci", "--omit=dev", "--ignore-scripts", "--no-audit", "--no-fund"],
        cwd=SERVER, capture_output=True, text=True,
    )
    if r.returncode != 0:
        sys.exit(f"npm ci failed:\n{(r.stderr or r.stdout)[-2000:]}")


def install_prebuild() -> None:
    dest = NODE_MODULES / "better-sqlite3" / BS3_MEMBER
    print(f"fetching better-sqlite3 {BS3_VERSION} prebuild (node ABI {NODE_ABI}, win32-x64)")
    with urllib.request.urlopen(BS3_URL, timeout=300) as r:  # noqa: S310 - pinned https
        blob = r.read()
    got = hashlib.sha256(blob).hexdigest()
    if got != BS3_SHA256:
        sys.exit(f"CHECKSUM MISMATCH for {BS3_URL}\n  expected {BS3_SHA256}\n  got      {got}")
    with tarfile.open(fileobj=io.BytesIO(blob), mode="r:gz") as t:
        member = t.extractfile(BS3_MEMBER)
        if member is None:
            sys.exit(f"'{BS3_MEMBER}' not in the prebuild archive — layout changed")
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(member.read())
    print(f"  installed {dest.relative_to(ROOT)} ({dest.stat().st_size / 1e6:.1f} MB, win32-x64)")


def prune() -> None:
    for rel in PRUNE_TREES:
        p = NODE_MODULES / rel
        if p.exists():
            shutil.rmtree(p)
    n = 0
    for p in NODE_MODULES.rglob("*"):
        if p.is_file() and p.suffix.lower() in PRUNE_SUFFIXES:
            p.unlink()
            n += 1
    print(f"  pruned {n} docs/sourcemap/type-declaration file(s) and {len(PRUNE_TREES)} C source tree(s)")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true", help="reinstall even if node_modules exists")
    args = ap.parse_args()
    if not (SERVER / "package-lock.json").is_file():
        sys.exit(f"no package-lock.json under {SERVER} — cannot install reproducibly")
    if NODE_MODULES.exists() and not args.force:
        print(f"{NODE_MODULES.relative_to(ROOT)} exists — use --force to rebuild")
    else:
        if NODE_MODULES.exists():
            shutil.rmtree(NODE_MODULES)
        npm_install()
        install_prebuild()
        prune()

    files = [p for p in NODE_MODULES.rglob("*") if p.is_file()]
    total = sum(p.stat().st_size for p in files)
    print(f"\nvendored: {len(files)} file(s), {total / 1e6:.1f} MB")
    native = list(NODE_MODULES.rglob("*.node"))
    print(f"native modules: {[str(p.relative_to(NODE_MODULES)) for p in native] or 'none'}")
    if not native:
        sys.exit("no .node binary staged — better-sqlite3 would fail to load on the user's machine")
    return 0


if __name__ == "__main__":
    sys.exit(main())
