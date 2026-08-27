// Tier invariant: a tool may be tier-1 (advertised on the MCP tool surface)
// ONLY if the Revit command it sends actually has a C# handler in commandset/.
//
// WHY THIS TEST EXISTS: three commits added 69 typed tools whose Revit-side
// commands were never implemented. On the wire they looked like capability; at
// the bridge every one of them would fail "command not found". They cost
// ~14k tokens of tool surface in every session to advertise nothing. Nothing in
// the repo could catch that, because the TypeScript side and the C# side have no
// compile-time relationship. This test is that relationship.
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { strict as assert } from "node:assert";
import test from "node:test";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const TOOLS = join(ROOT, "server/src/tools");

const tiers = JSON.parse(readFileSync(join(TOOLS, "tool_tiers.json"), "utf-8"));
const tier1 = new Set(tiers.tier1);
const tier2 = new Set(Object.keys(tiers.tier2));

function walk(dir, out = []) {
  for (const e of readdirSync(dir)) {
    const p = join(dir, e);
    if (statSync(p).isDirectory()) walk(p, out);
    else if (e.endsWith(".cs")) out.push(p);
  }
  return out;
}

/** Every CommandName implemented on the Revit side. */
function implementedCommands() {
  const found = new Set();
  for (const f of walk(join(ROOT, "commandset"))) {
    const src = readFileSync(f, "utf-8");
    for (const m of src.matchAll(/CommandName\s*(?:=>|=)\s*"([a-z0-9_]+)"/g)) found.add(m[1]);
  }
  return found;
}

/** tool name -> the Revit command it sends (null for local-only tools). */
function declaredTools() {
  const out = new Map();
  for (const f of readdirSync(TOOLS)) {
    if (!f.endsWith(".ts") || f === "register.ts" || f === "errors.ts") continue;
    const src = readFileSync(join(TOOLS, f), "utf-8");
    const names = [...src.matchAll(/server\.tool\(\s*\n?\s*"([a-z0-9_]+)"/g)].map((m) => m[1]);
    const cmds = [...src.matchAll(/sendCommand\(\s*"([a-z0-9_]+)"/g)].map((m) => m[1]);
    for (const n of names) out.set(n, cmds.length ? cmds[0] : null);
  }
  return out;
}

test("every tool is classified in exactly one tier", () => {
  const tools = declaredTools();
  const unclassified = [...tools.keys()].filter((n) => !tier1.has(n) && !tier2.has(n));
  assert.deepEqual(unclassified, [], `unclassified tools: ${unclassified.join(", ")}`);

  const both = [...tier1].filter((n) => tier2.has(n));
  assert.deepEqual(both, [], `tools in both tiers: ${both.join(", ")}`);

  const ghosts = [...tier1, ...tier2].filter((n) => !tools.has(n));
  assert.deepEqual(ghosts, [], `manifest names no such tool: ${ghosts.join(", ")}`);
});

test("no tier-1 tool depends on an unimplemented Revit command", () => {
  const tools = declaredTools();
  const impl = implementedCommands();
  const broken = [...tier1]
    .map((n) => [n, tools.get(n)])
    .filter(([, cmd]) => cmd !== null && !impl.has(cmd))
    .map(([n, cmd]) => `${n} -> ${cmd}`);
  assert.deepEqual(
    broken,
    [],
    "tier-1 tools whose Revit command has no C# handler (implement the handler, " +
      "or move the tool to tier2):\n  " + broken.join("\n  ")
  );
});

test("tier-2 tools are all discoverable in the function registry", () => {
  const reg = JSON.parse(readFileSync(join(TOOLS, "revit_function_registry.json"), "utf-8"));
  const byPath = new Map(reg.entries.map((e) => [e.path, e]));
  const missing = [...tier2].filter((n) => !byPath.has(n));
  assert.deepEqual(missing, [], `tier-2 tools absent from the registry (invisible to search_revit_api): ${missing.join(", ")}`);

  const mislabelled = [...tier2].filter((n) => byPath.get(n).verification_status !== "unimplemented");
  assert.deepEqual(mislabelled, [], `tier-2 registry entries not marked unimplemented: ${mislabelled.join(", ")}`);
});

test("the discovery + escape-hatch tools are always tier-1", () => {
  // The safety argument for hiding tools is that the model can always find and
  // execute what is hidden. That argument fails the moment one of these drops
  // off the surface.
  for (const n of ["search_revit_api", "query_revit_registry", "list_revit_api_categories", "send_code_to_revit"]) {
    assert.ok(tier1.has(n), `${n} must stay tier-1: it is how hidden tools are reached`);
  }
});
