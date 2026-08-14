# AiConnect Revit Connector 1.0.0 — Adaptation Package

## Source provenance

- Upstream: `https://github.com/mcp-servers-for-revit/mcp-servers-for-revit` (fork of `mcp-servers-for-revit/revit-mcp`)
- Upstream commit: `86cf7057785f2423fc0d0892c9a5d9c93deb1aa3` (2026-04-05)
- AiConnect adaptation source: `/project/aiconnector/connectors/civil/revit-mcp`
- License: MIT (upstream LICENSE — Copyright 2026 sparx-fire, mcp-servers-for-revit)

## What this branch adds

- `manifest.json` — AiConnect connector manifest (node stdio, entry `build/index.js`, host plugin `RevitMCPPlugin.zip`, Revit 2023/2024/2025)
- `scripts/package.sh` — deterministic package build (server build → RevitMCPPlugin.zip → `dist/revit-mcp-1.0.0.zip` + `.sha256`)
- `scripts/package-check.mjs` — verifies manifest/entry/artifact/digest/security, fails non-zero
- `docs/AICONNECT-ADAPTATION.md` — this document
- `command.json` — restored from upstream (missing in the adapted working copy; required by the commandset DeployCommandSet target)

## Package

- `dist/revit-mcp-1.0.0.zip` (built on Linux; 52 MB — node_modules included for stdio launch)
- SHA-256: `08388212f0d32ad589733e663d1b4004aaec441625c16c0440a8571f1ab60d98`
- Contents: `manifest.json`, `build/index.js`, `node_modules/`, `package.json` + lock, `src/` (adapted server), `LICENSE`, `README.md`, `command.json`, `RevitMCPPlugin.zip` (layouts for 2023/2024/2025: `.addin` + `revit_mcp_plugin/RevitMCPPlugin.dll` + commandset)
- Artifact storage: `dist/` is NOT tracked in git (large binary); rebuilt via `scripts/package.sh`

## Build evidence (Linux)

- Server: `npm ci && npm run build` → `server/build/index.js` ✓ (license-gated: requires `MCP_LICENSE_TOKEN`)
- Plugin: `dotnet build -c "Release R23|R24|R25" -p:EnableWindowsTargeting=true -p:AppData=<writable>` ✓ (Nice3point RevitAPI NuGets, net48 for R23/R24, net8 for R25)
- `.addin` copied per layout (upstream Debug-only copy target did not produce it for R23/R24)

## Windows validation

NOT YET RUN — no Windows/Revit environment available. All rows in the matrix remain UNVERIFIED until a Windows VM (Revit 2023/2024/2025) executes:

- Process Manager discovery → host plugin install → Revit loads add-in → `waiting_for_host` → `connected` → MCP `initialize`/`tools/list`/read/write → external Revit model verification → crash/restart → stop/no-orphans

## Known limitations / release gate

1. **Process Manager capability gap (BLOCKER for R2):** `install_host_plugin` currently COPIES `RevitMCPPlugin.zip` into the addins directory; it does not EXTRACT it. Revit loads `.addin` files, not zips — the plugin cannot load until extraction (per `{revit_version}` layout selection) is added to the installer. Per the adaptation contract, Process Manager was NOT modified for this connector.
2. Package size 52 MB is dominated by `node_modules` (better-sqlite3 native prebuild); a slimmer runtime install (npm-based) is a future optimization.
3. License gate: PASS (MIT, attribution retained).
