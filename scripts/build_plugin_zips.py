#!/usr/bin/env python3
"""Build the per-Revit-version host-plugin archives named in manifest.json.

`manifest.host_plugin.artifact` is `RevitMCPPlugin-{revit_version}.zip`. The gateway
substitutes `{revit_version}` for each Revit version it detects under
`%APPDATA%/Autodesk/Revit/Addins/`, so ONE connector package carries every supported
version and the user never has to choose. This produces those archives.

Source: the msbuild output dropped in `~/project/revit-addin-releases/`, one directory
per Revit version. Revit 2023/2024 are .NET Framework 4.8 and 2025/2026 are .NET 8, and
the plugin/SDK assemblies differ in every one of the four — they are genuinely not
interchangeable, so all four ship.

Archive layout matches what Revit expects when the archive is EXTRACTED into
`Addins/<ver>/`:

    mcp-servers-for-revit.addin              <- Revit scans for loose .addin files
    revit_mcp_plugin/
        RevitMCPPlugin.dll, RevitMCPSDK.dll, ...
        Commands/
            commandRegistry.json             <- generated here from command.json
            RevitMCPCommandSet.dll           <- MUST come from the build; see below

`.pdb` debug symbols and macOS `__MACOSX/` resource forks are excluded.

Deterministic: sorted entries, fixed 1980 timestamp, so two runs give identical bytes
and the connector package's sha256 stays reproducible.

Run: python3 scripts/build_plugin_zips.py [--src DIR]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SRC = Path.home() / "project" / "revit-addin-releases"
COMMAND_JSON = ROOT / "command.json"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)
SKIP_SUFFIX = (".pdb",)
SKIP_PARTS = {"__MACOSX"}
VERSION_RE = re.compile(r"\b(20\d{2})\b")


def build_registry() -> bytes:
    """commandRegistry.json, generated from the connector's own command.json.

    The plugin auto-creates this file when absent, but with an EMPTY commands array —
    so an add-in that ships without it registers nothing and every tool fails. It is
    generated rather than copied so it cannot drift from command.json.
    """
    data = json.loads(COMMAND_JSON.read_text(encoding="utf-8"))
    return (json.dumps({"commands": data["commands"]}, indent=2) + "\n").encode("utf-8")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(DEFAULT_SRC))
    args = ap.parse_args()
    src = Path(args.src).expanduser()
    if not src.is_dir():
        sys.exit(f"add-in build directory not found: {src}")

    builds: dict[str, Path] = {}
    for d in sorted(p for p in src.iterdir() if p.is_dir() and p.name not in SKIP_PARTS):
        m = VERSION_RE.search(d.name)
        if m:
            builds[m.group(1)] = d
    if not builds:
        sys.exit(f"no 'AddIn <year> ...' directories under {src}")

    registry = build_registry()
    n_cmds = len(json.loads(registry)["commands"])
    incomplete: list[str] = []

    for ver, d in sorted(builds.items()):
        out = ROOT / f"RevitMCPPlugin-{ver}.zip"
        files = sorted(
            p for p in d.rglob("*")
            if p.is_file()
            and p.suffix not in SKIP_SUFFIX
            and not (set(p.relative_to(d).parts) & SKIP_PARTS)
        )
        with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
            for f in files:
                info = zipfile.ZipInfo(f.relative_to(d).as_posix(), date_time=FIXED_TIME)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                z.writestr(info, f.read_bytes())
            info = zipfile.ZipInfo("revit_mcp_plugin/Commands/commandRegistry.json",
                                   date_time=FIXED_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, registry)

        names = {f.name for f in files}
        has_cmdset = "RevitMCPCommandSet.dll" in names
        if not has_cmdset:
            incomplete.append(ver)
        digest = hashlib.sha256(out.read_bytes()).hexdigest()
        print(f"{out.name:28} {len(files) + 1:2d} files  {out.stat().st_size / 1e6:5.1f} MB  "
              f"{digest[:16]}…  CommandSet={'yes' if has_cmdset else 'MISSING'}")

    print(f"\ncommandRegistry.json generated with {n_cmds} commands from command.json")
    if incomplete:
        print(f"\nWARNING: RevitMCPCommandSet.dll absent from {', '.join(incomplete)}.",
              file=sys.stderr)
        print("These archives install and start, but resolve ZERO of the "
              f"{n_cmds} commands — every MCP tool that routes through them fails. "
              "Rebuild with the CommandSet project output under "
              "revit_mcp_plugin/Commands/ before shipping.", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
