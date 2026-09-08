// Profile invariant: MCP_PROFILE filtering (tool_profiles.json) is an
// orthogonal second axis to tiering (tool_tiers.json) — see register.ts's
// top comment. This test protects the composition, not the tiering
// invariant itself (tool_tiers.test.mjs already owns that).
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { strict as assert } from "node:assert";
import test from "node:test";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const TOOLS = join(ROOT, "server/src/tools");

const tiers = JSON.parse(readFileSync(join(TOOLS, "tool_tiers.json"), "utf-8"));
const tier1 = new Set(tiers.tier1);
const profiles = JSON.parse(readFileSync(join(TOOLS, "tool_profiles.json"), "utf-8"));
const registry = JSON.parse(readFileSync(join(TOOLS, "revit_function_registry.json"), "utf-8"));
const manifest = JSON.parse(readFileSync(join(ROOT, "manifest.json"), "utf-8"));

const categoryByName = new Map(
  registry.entries.filter((e) => e.kind === "Tool" && e.category).map((e) => [e.path, e.category])
);
const registryCategories = new Set(categoryByName.values());

function passesProfile(name, profileDef) {
  if (profileDef.categories === "*" || profiles.always.includes(name)) return true;
  const cat = categoryByName.get(name);
  const allowed = new Set([...profiles.base_categories, ...profileDef.categories]);
  return !!cat && allowed.has(cat);
}

test("every category referenced in tool_profiles.json exists in the registry", () => {
  const referenced = new Set(profiles.base_categories);
  for (const def of Object.values(profiles.profiles)) {
    if (def.categories !== "*") for (const c of def.categories) referenced.add(c);
  }
  const unknown = [...referenced].filter((c) => !registryCategories.has(c));
  assert.deepEqual(unknown, [], `tool_profiles.json references categories absent from revit_function_registry.json: ${unknown.join(", ")}`);
});

test("every category in the registry is covered by some profile", () => {
  const referenced = new Set(profiles.base_categories);
  for (const def of Object.values(profiles.profiles)) {
    if (def.categories !== "*") for (const c of def.categories) referenced.add(c);
  }
  const uncovered = [...registryCategories].filter((c) => !referenced.has(c));
  assert.deepEqual(uncovered, [], `registry categories not reachable under any profile: ${uncovered.join(", ")}`);
});

test("tool_profiles.json.always is a subset of tier1", () => {
  const notTier1 = profiles.always.filter((n) => !tier1.has(n));
  assert.deepEqual(notTier1, [], `always-on tools must stay tier1: ${notTier1.join(", ")}`);
});

test("manifest.json's tool_tiers.core is a subset of tier1", () => {
  // manifest.json's list is a separate, externally-consumed capability-highlight
  // concept (see tool_profiles.json's _doc) — not read by any code in this repo,
  // but it must not silently drift to advertise a name that no longer registers.
  const core = manifest.tool_tiers?.core ?? [];
  const notTier1 = core.filter((n) => !tier1.has(n));
  assert.deepEqual(notTier1, [], `manifest.json tool_tiers.core names not in tier1: ${notTier1.join(", ")}`);
});

test("the 'full' profile registers exactly today's tier1 set (no-op guarantee)", () => {
  const full = profiles.profiles[profiles.default];
  assert.ok(full, `profiles.profiles must contain the default profile "${profiles.default}"`);
  const allowed = [...tier1].filter((n) => passesProfile(n, full));
  assert.deepEqual(new Set(allowed), tier1, "the default profile must be a no-op over tier1");
});

test("every named profile's registration is a subset of tier1", () => {
  for (const [name, def] of Object.entries(profiles.profiles)) {
    const allowed = [...tier1].filter((n) => passesProfile(n, def));
    const extra = allowed.filter((n) => !tier1.has(n));
    assert.deepEqual(extra, [], `profile "${name}" would register a non-tier1 name: ${extra.join(", ")}`);
  }
});
