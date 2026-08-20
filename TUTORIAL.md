# Autodesk Revit Connection Setup Guide

### 1. Prerequisites
- Autodesk Revit 2023, 2024, 2025, or 2026 installed on your Windows machine.
- AiConnect Gateway running on loopback port `8788`.

### 2. Install & Connect the Revit Add-In
1. The AiConnect Installer automatically places the Revit Add-in manifest and DLL into your Revit Addins directory:
   `%APPDATA%\Autodesk\Revit\Addins\{version}\`
2. Launch or restart **Autodesk Revit**.
3. When prompted by Revit with the security dialog:
   - Click **"Always Load"** for the **AiConnect Revit MCP Plugin**.
4. Open any Revit project or active model (`.rvt`).
5. The add-in automatically establishes a local IPC socket connection with the connector bridge.

### 3. Verify Connection in AiConnect Desktop
1. Return to **AiConnect Desktop**.
2. The **Revit Connector** card status will change from `Waiting for Host` to `● Connected`.
3. Your AI agent (Antigravity AGY, Claude Code, Cursor) can now query and automate Revit BIM models in real-time.
