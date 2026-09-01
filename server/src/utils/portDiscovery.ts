/**
 * Resolves the Revit add-in's TCP socket port.
 *
 * The add-in (SocketService.cs) binds a preferred port and, if that's already
 * taken by something else, falls back to an OS-assigned free port — then
 * publishes whatever it actually bound to a local file (see
 * PathManager.GetPortFilePath()). This reads that file so the connector never
 * hardcodes a port that might be wrong.
 *
 * Fallback order: port file -> REVIT_SOCKET_PORT env var (explicit override)
 * -> 8080 (matches the add-in's own default when nothing else applies).
 */
import fs from "fs";
import path from "path";

const PORT_FILE_DIR = "aiconnect-revit-mcp";
const PORT_FILE_NAME = "port.txt";
const DEFAULT_PORT = 8080;

function portFilePath(): string | null {
  const localAppData = process.env.LOCALAPPDATA;
  if (!localAppData) return null;
  return path.join(localAppData, PORT_FILE_DIR, PORT_FILE_NAME);
}

function readPortFile(): number | null {
  const filePath = portFilePath();
  if (!filePath) return null;

  try {
    const raw = fs.readFileSync(filePath, "utf-8").trim();
    const parsed = Number(raw);
    return Number.isInteger(parsed) && parsed > 0 && parsed < 65536 ? parsed : null;
  } catch {
    return null;
  }
}

export function getRevitSocketPort(): number {
  const fromFile = readPortFile();
  if (fromFile !== null) return fromFile;

  const fromEnv = Number(process.env.REVIT_SOCKET_PORT);
  if (Number.isInteger(fromEnv) && fromEnv > 0) return fromEnv;

  return DEFAULT_PORT;
}
