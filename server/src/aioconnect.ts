/**
 * AiConnect integration for the forked Revit MCP server.
 *
 * Loads the shared Node SDK (connectors/sdk/node) at runtime — the license
 * gate (startup + per-call recheck) and the response-envelope wrapping live
 * in the SDK, not in this fork, so every Node connector gets the same
 * enforcement. The relative import is resolved at runtime (tsc does not
 * typecheck it — the SDK is plain ESM JS outside this package's rootDir).
 *
 * Env-gated (AICONNECT_ENABLE=1): standalone/upstream runs stay plain —
 * no token required, no envelope. Managed (Process Manager) spawns inject
 * AICONNECT_ENABLE=1 + MCP_LICENSE_TOKEN, so the gate + binding activate.
 * SDK resolution: AICONNECT_SDK_PATH env wins (installed AiConnect SDK,
 * keeps this public fork IP-free); else monorepo-relative fallback.
 */

import path from "node:path";
import { pathToFileURL } from "node:url";

let _sdk: any = null;

export async function sdk(): Promise<any> {
  if (!_sdk) {
    const envPath = process.env.AICONNECT_SDK_PATH;
    const sdkUrl = envPath
      ? pathToFileURL(path.resolve(envPath, "index.js")).href
      : new URL("../../../../sdk/node/index.js", import.meta.url).href;
    _sdk = await import(sdkUrl);
  }
  return _sdk;
}

const CONNECTOR_ID = "revit-mcp";

/**
 * Startup license gate — throws (LicenseError) → server refuses to start.
 * No-op (returns null) when AICONNECT_ENABLE != 1 (standalone/upstream).
 * Validates the PM-minted token AND its connector binding: the Process
 * Manager mints sub=`connector:<id>`, entitlements=[<id>] (auth.rs::mint),
 * so a token minted for another connector must never authorize this one.
 */
export async function ensureLicensed(): Promise<any> {
  if (process.env.AICONNECT_ENABLE !== "1") return null;
  const mod = await sdk();
  const validator = new mod.LicenseValidator(
    process.env.JWT_SECRET ?? "dev-secret-change-me"
  );
  const claims = validator.ensureLicensed();
  if (claims.sub !== `connector:${CONNECTOR_ID}`) {
    throw new mod.LicenseError(`token not bound to ${CONNECTOR_ID}`);
  }
  const scopes = claims.entitlements ?? claims.scopes ?? [];
  if (!scopes.includes(CONNECTOR_ID)) {
    throw new mod.LicenseError(`token lacks scope ${CONNECTOR_ID}`);
  }
  return validator;
}

/** Wrap a tool's text content through the response envelope. */
export async function envelope(text: string): Promise<string> {
  const mod = await sdk();
  return mod.envelopeText(text);
}
