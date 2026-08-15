# Revit MCP Connector

Fork of [mcp-servers-for-revit](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit)
(MIT License, see `LICENSE` in this directory).

**Two parts, different lifecycles (see vault: phase-6-revit-connector.md):**

| Part | Location | Lifecycle owner |
|---|---|---|
| Revit Add-in (C#) | `plugin/` + `commandset/` | Revit itself — loaded from `%APPDATA%\Autodesk\Revit\Addins\<version>\` at Revit startup. **Our Process Manager never spawns this.** |
| MCP Server (Node/TS) | `server/` | Our Process Manager — the spawnable, licensable, health-checkable process. |

## AiConnect adaptations (delta vs upstream)
- `server/src/revit_license.ts` — license gate (startup + per-call recheck) via `connectors/sdk/node`
- `server/src/revit_envelope.ts` — wraps every tool's response through the tool-response envelope schema (generic wrapper in `register.ts`, no per-tool edits)
- `manifest.json` — includes the `host_plugin` block (add-in install metadata)

## Build & run (server)
```
cd server && npm install && npm run build
MCP_LICENSE_TOKEN=<token> node build/index.js   # refuses to start without a valid token
```
