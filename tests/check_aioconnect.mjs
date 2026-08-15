// AiConnect adapter unit checks (plain node, no test framework).
// Exercises build/aioconnect.js: env-gate, token binding, envelope.
// Run: AICONNECT_SDK_PATH=/project/aiconnector/connectors/sdk/node node tests/check_aioconnect.mjs
import { createHmac } from "node:crypto";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const SDK = process.env.AICONNECT_SDK_PATH;
if (!SDK) {
  console.error("FAIL: AICONNECT_SDK_PATH required");
  process.exit(1);
}

const { ensureLicensed, envelope } = await import("../server/build/aioconnect.js");

const SECRET = "0123456789abcdef0123456789abcdef";
process.env.JWT_SECRET = SECRET;

const b64 = (b) => Buffer.from(b).toString("base64url");
function mint(entitlements, subject, ttl = 600) {
  const header = b64(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const now = Math.floor(Date.now() / 1000);
  const payload = b64(JSON.stringify({
    sub: subject, iat: now, exp: now + ttl, entitlements,
  }));
  const sig = createHmac("sha256", SECRET).update(`${header}.${payload}`).digest("base64url");
  return `${header}.${payload}.${sig}`;
}

const results = [];
const check = (name, cond) => { results.push([name, !!cond]); console.log((cond ? "PASS" : "FAIL"), name); };

// 1. env-gate: disabled → no-op
delete process.env.AICONNECT_ENABLE;
delete process.env.MCP_LICENSE_TOKEN;
const none = await ensureLicensed();
check("disabled: ensureLicensed no-op (returns null)", none === null);

// 2. enabled + missing token → refuse
process.env.AICONNECT_ENABLE = "1";
delete process.env.MCP_LICENSE_TOKEN;
let threw = false;
try { await ensureLicensed(); } catch { threw = true; }
check("enabled: missing token refuses", threw);

// 3. enabled + wrong subject → refuse
process.env.MCP_LICENSE_TOKEN = mint(["revit-mcp"], "connector:other-mcp");
threw = false;
try { await ensureLicensed(); } catch (e) { threw = e.message.includes("not bound"); }
check("enabled: wrong subject refuses", threw);

// 4. enabled + wrong entitlement → refuse
process.env.MCP_LICENSE_TOKEN = mint(["other-mcp"], "connector:revit-mcp");
threw = false;
try { await ensureLicensed(); } catch (e) { threw = e.message.includes("lacks scope"); }
check("enabled: missing entitlement refuses", threw);

// 5. enabled + valid bound token → passes
process.env.MCP_LICENSE_TOKEN = mint(["revit-mcp"], "connector:revit-mcp");
const validator = await ensureLicensed();
check("enabled: valid bound token passes", validator !== null);

// 6. envelope: JSON text → ok envelope
const env = await envelope('{"a":1}');
check("envelope: JSON → ok(data)", typeof env === "string" && env.includes('"success":true'));

const failed = results.filter(([, ok]) => !ok);
console.log(`\n${results.length - failed.length}/${results.length} adapter checks passed`);
process.exit(failed.length ? 1 : 0);
