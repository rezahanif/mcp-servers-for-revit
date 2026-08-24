# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-08-24

### Added
- CHANGELOG.md

### Fixed
- Tool loader now validates all expected tools register successfully; throws on missing tools instead of silently swallowing errors
- Removed demo `say_hello` tool and cleaned up all references
- Updated `DEFINITION_QUALITY.md` score to reflect accurate tool registration status

### Removed
- `say_hello` demo tool (not production-useful, was a connection test stub)
