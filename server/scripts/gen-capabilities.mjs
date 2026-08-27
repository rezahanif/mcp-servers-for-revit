// Emit build/tools/aiconnect-capabilities.json — the tier-2 capability index the
// AiConnect gateway reads (manifest field `tool_index`).
//
// WHY: quarantining a tool out of tools/list also removes it from the gateway's
// tools.find corpus, which is built solely from tools/list. Measured on this
// connector, that left 0 of 25 expansion-area capabilities discoverable — a model
// asking to "run a duct from the AHU" got nothing back, because create_duct's name
// existed nowhere the gateway could search.
//
// These entries cost ZERO tool-surface tokens: they never enter tools/list, only the
// gateway's in-memory search index, and only appear in a tools.find RESULT when they
// actually match. Measured effect: weighted coverage 50.0% -> 80.7%.
//
// Derived from tool_tiers.json + revit_function_registry.json so there is one source
// of truth; a tool promoted to tier 1 drops out of here automatically.
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const TOOLS = fileURLToPath(new URL("../build/tools/", import.meta.url));

const tiers = JSON.parse(readFileSync(TOOLS + "tool_tiers.json", "utf-8"));
const registry = JSON.parse(readFileSync(TOOLS + "revit_function_registry.json", "utf-8"));

const tier2 = new Set(Object.keys(tiers.tier2));
const byPath = new Map(registry.entries.map((e) => [e.path, e]));

const capabilities = [...tier2].sort().map((name) => {
  const e = byPath.get(name);
  return {
    name,
    description: e?.description ?? "",
    category: e?.category ?? null,
  };
});

const out = {
  // The gateway verifies this against the connector's real tools/list and indexes
  // nothing if it is absent — a capability with no way to run it is worse than none.
  exec_tool: "send_code_to_revit",
  capabilities,
};

writeFileSync(TOOLS + "aiconnect-capabilities.json", JSON.stringify(out, null, 1));
console.error(`capabilities: ${capabilities.length} tier-2 entries -> build/tools/aiconnect-capabilities.json`);
