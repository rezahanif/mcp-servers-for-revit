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

// Authored intent phrasings. The gateway folds these into its BM25 haystack ONLY
// (ToolDoc::add_search_text) — never into the summary the model is shown, and never
// into the embedding text. Measured worth: +8.8 pt recall@1, +10.0 pt weighted for
// ~4.6 KB, negatives held. That is 2x the recall and 5x the coverage the entire
// ~104 MB ONNX embedding stack buys.
//
// Aliases apply to tier-1 tools too, which is why the entries below are emitted for
// LISTED tool names as well: the gateway skips a capability whose name collides with
// a real tool (the callable one wins) but now harvests its aliases onto that tool
// first. An older gateway simply skips them, so this stays backward compatible.
const aliasDoc = JSON.parse(readFileSync(TOOLS + "aiconnect_aliases.json", "utf-8"));
const aliases = aliasDoc.aliases ?? {};

const tier2 = new Set(Object.keys(tiers.tier2));
const byPath = new Map(registry.entries.map((e) => [e.path, e]));

const capabilities = [...tier2].sort().map((name) => {
  const e = byPath.get(name);
  return {
    name,
    description: e?.description ?? "",
    category: e?.category ?? null,
    ...(aliases[name] ? { aliases: aliases[name] } : {}),
  };
});

// Alias-only entries for tools that ARE listed. These carry no description — the
// real one arrives over tools/list — and exist purely so the gateway can attach
// phrasings to a tool it already knows.
const enriched = Object.keys(aliases)
  .filter((name) => !tier2.has(name))
  .sort()
  .map((name) => ({ name, aliases: aliases[name] }));
capabilities.push(...enriched);

const out = {
  // The gateway verifies this against the connector's real tools/list and indexes
  // nothing if it is absent — a capability with no way to run it is worse than none.
  exec_tool: "send_code_to_revit",
  capabilities,
};

writeFileSync(TOOLS + "aiconnect-capabilities.json", JSON.stringify(out, null, 1));
console.error(
  `capabilities: ${capabilities.length} entries ` +
  `(${tier2.size} tier-2, ${enriched.length} alias-only for listed tools) ` +
  `-> build/tools/aiconnect-capabilities.json`,
);
