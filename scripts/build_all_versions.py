#!/usr/bin/env python3
"""Unified build and packaging script for Revit MCP Plugin.

Compiles and packages per-Revit-version addin archives:
- Revit 2024 (net48)
- Revit 2025 (net8.0-windows)
- Revit 2026 (net10.0-windows)

Produces self-contained archives: RevitMCPPlugin-{revit_version}.zip
with guaranteed structure:
  mcp-servers-for-revit.addin
  revit_mcp_plugin/
    RevitMCPPlugin.dll, RevitMCPSDK.dll, ...
    Commands/
      commandRegistry.json
      RevitMCPCommandSet.dll                       (flat discovery)
      RevitMCPCommandSet/{version}/RevitMCPCommandSet.dll (versioned discovery)
      Microsoft.CodeAnalysis.CSharp.dll, ...

Usage:
  python scripts/build_all_versions.py [--versions 2025 2026] [--deploy]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
COMMAND_JSON = ROOT / "command.json"
PLUGIN_CSPROJ = ROOT / "plugin" / "RevitMCPPlugin.csproj"
COMMANDSET_CSPROJ = ROOT / "commandset" / "RevitMCPCommandSet.csproj"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)
SKIP_SUFFIXES = {".pdb"}

CONFIG_MAP = {
    "2020": "Release R20",
    "2021": "Release R21",
    "2022": "Release R22",
    "2023": "Release R23",
    "2024": "Release R24",
    "2025": "Release R25",
    "2026": "Release R26",
}


def build_command_registry() -> bytes:
    """Generate commandRegistry.json with default port 8088 and all commands."""
    if not COMMAND_JSON.exists():
        raise FileNotFoundError(f"command.json not found at {COMMAND_JSON}")
    data = json.loads(COMMAND_JSON.read_text(encoding="utf-8"))
    payload = {
        "settings": {
            "port": 8088,
            "logLevel": "Info",
        },
        "commands": data.get("commands", []),
    }
    return (json.dumps(payload, indent=2) + "\n").encode("utf-8")


def run_dotnet_build(project: Path, config: str) -> bool:
    print(f"--> Building {project.name} [{config}]...")
    cmd = ["dotnet", "build", str(project), "-c", config]
    result = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, text=True)
    if result.returncode != 0:
        print(f"FAILED: dotnet build {project.name} [{config}]", file=sys.stderr)
        print(result.stderr or result.stdout, file=sys.stderr)
        return False
    print(f"    Built {project.name} [{config}] successfully.")
    return True


def package_version(ver: str, out_dir: Path) -> Path | None:
    config = CONFIG_MAP.get(ver)
    if not config:
        print(f"Unknown Revit version: {ver}", file=sys.stderr)
        return None

    plugin_bin = ROOT / "plugin" / "bin" / "Release" / ver
    if not plugin_bin.exists():
        # Fallback check for alternate path
        plugin_bin = ROOT / "plugin" / "bin" / config / ver
    if not plugin_bin.exists():
        print(f"Plugin output directory not found for {ver}: {plugin_bin}", file=sys.stderr)
        return None

    # Locate publish folder for commandset
    publish_base = ROOT / "commandset" / "bin" / config / "publish"
    cmdset_source_dir: Path | None = None
    if publish_base.exists():
        for sub in publish_base.rglob("RevitMCPCommandSet"):
            if sub.is_dir() and (sub / "RevitMCPCommandSet.dll").exists():
                cmdset_source_dir = sub
                break

    if not cmdset_source_dir:
        # Fallback to direct bin folder
        direct_bin = ROOT / "commandset" / "bin" / config
        if (direct_bin / "RevitMCPCommandSet.dll").exists():
            cmdset_source_dir = direct_bin

    if not cmdset_source_dir:
        print(f"CommandSet output not found for {ver} in {publish_base}", file=sys.stderr)
        return None

    registry_bytes = build_command_registry()
    out_zip = out_dir / f"RevitMCPPlugin-{ver}.zip"

    manifest_file = plugin_bin / "mcp-servers-for-revit.addin"
    if not manifest_file.exists():
        manifest_file = ROOT / "plugin" / "mcp-servers-for-revit.addin"

    with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as z:
        # 1. Addin manifest at root
        if manifest_file.exists():
            info = zipfile.ZipInfo("mcp-servers-for-revit.addin", date_time=FIXED_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, manifest_file.read_bytes())

        # 2. Plugin runtime assemblies
        for f in plugin_bin.iterdir():
            if f.is_file() and f.suffix not in SKIP_SUFFIXES and f.name != "mcp-servers-for-revit.addin":
                entry_name = f"revit_mcp_plugin/{f.name}"
                info = zipfile.ZipInfo(entry_name, date_time=FIXED_TIME)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                z.writestr(info, f.read_bytes())

        # 3. Command registry
        info = zipfile.ZipInfo("revit_mcp_plugin/Commands/commandRegistry.json", date_time=FIXED_TIME)
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o644 << 16
        z.writestr(info, registry_bytes)

        # 4. CommandSet assemblies: both flat & versioned layout
        for f in cmdset_source_dir.rglob("*"):
            if f.is_file() and f.suffix not in SKIP_SUFFIXES and f.name != "commandRegistry.json":
                rel = f.relative_to(cmdset_source_dir)
                # Flat path under Commands/
                info = zipfile.ZipInfo(f"revit_mcp_plugin/Commands/{rel.as_posix()}", date_time=FIXED_TIME)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o644 << 16
                z.writestr(info, f.read_bytes())

        # Also store versioned copy under Commands/RevitMCPCommandSet/{ver}/
        cmdset_dll = cmdset_source_dir / "RevitMCPCommandSet.dll"
        if cmdset_dll.exists():
            info = zipfile.ZipInfo(f"revit_mcp_plugin/Commands/RevitMCPCommandSet/{ver}/RevitMCPCommandSet.dll", date_time=FIXED_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, cmdset_dll.read_bytes())

    return out_zip


def deploy_to_appdata(ver: str, zip_path: Path):
    appdata = os.environ.get("APPDATA")
    if not appdata:
        print("APPDATA environment variable not set, skipping deployment.", file=sys.stderr)
        return

    dest_dir = Path(appdata) / "Autodesk" / "Revit" / "Addins" / ver
    if not dest_dir.exists():
        print(f"Revit {ver} Addins directory does not exist: {dest_dir}")
        return

    print(f"Deploying {zip_path.name} to {dest_dir}...")
    with zipfile.ZipFile(zip_path, "r") as z:
        for member in z.infolist():
            target_path = dest_dir / member.filename
            if member.is_dir():
                target_path.mkdir(parents=True, exist_ok=True)
                continue
            target_path.parent.mkdir(parents=True, exist_ok=True)
            data = z.read(member)
            try:
                target_path.write_bytes(data)
            except PermissionError:
                # Windows locked file workaround: rename locked file and write new file
                old_path = target_path.with_name(target_path.name + f".old_{os.getpid()}")
                try:
                    if old_path.exists():
                        old_path.unlink()
                    target_path.rename(old_path)
                    target_path.write_bytes(data)
                    print(f"    Notice: {target_path.name} was locked by Revit. Replaced via hot-swap.")
                except Exception as ex:
                    print(f"    Warning: Could not overwrite locked file {target_path.name}: {ex}", file=sys.stderr)

    print(f"Successfully deployed to {dest_dir}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Build and package Revit MCP Plugin for multiple versions")
    parser.add_argument("--versions", nargs="+", default=["2025", "2026"],
                        help="Revit versions to build (default: 2025 2026)")
    parser.add_argument("--no-build", action="store_true", help="Skip compilation, only package")
    parser.add_argument("--out-dir", default=str(ROOT), help="Output directory for zip archives")
    parser.add_argument("--deploy", action="store_true", help="Extract built zip directly into %APPDATA% Revit Addins folder")
    args = parser.parse_args()

    out_dir = Path(args.out_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)

    versions = args.versions
    print(f"Target versions: {', '.join(versions)}")

    # Step 1: Build projects
    if not args.no_build:
        for ver in versions:
            config = CONFIG_MAP.get(ver)
            if not config:
                print(f"Skipping unsupported version: {ver}")
                continue

            print(f"\n=================== Building Revit {ver} ({config}) ===================")
            ok = run_dotnet_build(PLUGIN_CSPROJ, config)
            if not ok:
                return 1
            ok = run_dotnet_build(COMMANDSET_CSPROJ, config)
            if not ok:
                return 1

    # Step 2: Package ZIPs
    print("\n=================== Packaging Archives ===================")
    packages: list[Path] = []
    for ver in versions:
        zip_path = package_version(ver, out_dir)
        if zip_path and zip_path.exists():
            packages.append(zip_path)
            digest = hashlib.sha256(zip_path.read_bytes()).hexdigest()
            print(f"Package: {zip_path.name:28} Size: {zip_path.stat().st_size / 1e6:5.1f} MB  SHA256: {digest[:16]}...")
            if args.deploy:
                deploy_to_appdata(ver, zip_path)

    print(f"\nSuccessfully built and packaged {len(packages)} Revit MCP plugin archive(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
