# Changelog

All notable changes to this project will be documented in this file.

## [1.2.3] - 2026-09-07

### Added
- Clash detection (`check_clashes`, `resolve_geometry_clash`)
- Warning modal suppression and query (`get_document_warnings`)
- Family management (`load_family`, `place_family_instance`, `create_family_type`, `set_family_parameter`)
- Civil DirectShape geometry creation (`create_direct_shape`)
- Navisworks export support (`export_navisworks`)
- User-oriented connection tutorial with Mermaid flowchart in `TUTORIAL.md`

## [1.2.2] - 2026-09-07

### Changed
- Refreshed connection tutorial and marketplace metadata.

## [1.2.1] - 2026-09-07

### Fixed
- Corrected misleading `[Info]` failure log in `CommandManager.cs` on successful command instance creation.
- Updated `RevitMCPPlugin.csproj` copy target so Release configurations deploy to `%AppData%` correctly without getting skipped by Debug-only conditions.
- Re-packaged connector archive with verified port 8088 registry, ping probe, and updated deployment targets.

## [1.2.0] - 2026-09-07

### Added
- Port discovery and ping probe handshake between server and Revit add-in
- Connector-wide reliability notes appended to tool descriptions with warning surfacing
- Structured warnings and failures surfaced in `AIResult` for CommandSet

### Changed
- Migrated default socket communication port to 8088
- Support `REVIT_SOCKET_PORT` environment variable and fail-fast when `commandRegistry.json` is missing
- Rebuilt `RevitMCPPlugin-*.zip` archives with port 8088 in `commandRegistry.json`

## [1.1.0] - 2026-09-05

### Changed
- Set entitlement tier to free
- Port conflict resolution with OS-assigned fallback

## [1.0.2] - 2026-09-03

### Fixed
- R25/R26 (net8.0/net10.0): `send_code_to_revit`'s Roslyn dependency
  (`Microsoft.CodeAnalysis.CSharp`) was missing `System.Collections.Immutable.dll` at
  runtime — MSBuild resolves it as framework-provided and never copies it next to the
  add-in, but Revit loads add-ins through its own `AssemblyDependencyResolver`, not the
  shared framework, so the assumption doesn't hold. `Microsoft.CodeAnalysis.dll`/
  `Microsoft.CodeAnalysis.CSharp.dll` had the same gap. `CopyLocalLockFileAssemblies` plus
  a new `CopyFrameworkOnlyRoslynDeps` build target now vendor and copy all three next to
  the add-in on R25/R26; R20-R24 (net48) were never affected.
- Removed the SQLite-backed `store_project_data`/`store_room_data`/`query_stored_data`
  tools and their `better-sqlite3` dependency (superseded by the AiConnect gateway's
  `connector_progress.write`/`.read`), eliminating the Node-ABI-version crash class that
  came with a native module pinned to one Node build.

## [1.0.0] - 2026-08-24

### Added
- CHANGELOG.md

### Fixed
- Tool loader now validates all expected tools register successfully; throws on missing tools instead of silently swallowing errors
- Removed demo `say_hello` tool and cleaned up all references
- Updated `DEFINITION_QUALITY.md` score to reflect accurate tool registration status

### Removed
- `say_hello` demo tool (not production-useful, was a connection test stub)
