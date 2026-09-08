// Re-runnable version of the manual efficiency measurement from the
// profile-bundling plan: for each profile in tool_profiles.json, register
// tools against a fake McpServer and sum a token-cost proxy
// (name + description + JSON-stringified schema length) over what actually
// gets registered. Run after every tier2->tier1 promotion to see how the
// profile split's payoff is growing.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const TOOLS_JSON = join(__dirname, "..", "src", "tools");

const profiles = JSON.parse(readFileSync(join(TOOLS_JSON, "tool_profiles.json"), "utf-8"));

async function registeredCostForProfile(profileName) {
  const rows = [];
  class CostServer {
    tool(name, descOrSchema, schemaOrHandler, maybeHandler) {
      const hasHandler = maybeHandler !== undefined;
      const desc = hasHandler ? descOrSchema : "";
      const schema = hasHandler ? schemaOrHandler : descOrSchema;
      let schemaLen = 0;
      try {
        schemaLen = JSON.stringify(schema, (_, v) => (typeof v === "function" ? undefined : v)).length;
      } catch {
        /* zod objects can be circular-ish; best effort */
      }
      rows.push({ name, cost: name.length + desc.length + schemaLen });
    }
  }

  process.env.MCP_PROFILE = profileName;
  // Bust the module cache isn't possible for a static import across calls in
  // one process, so this script must be invoked once per profile (see main()).
  const { registerTools } = await import(
    "file://" + join(__dirname, "..", "build", "tools", "register.js") + `?p=${profileName}`
  );
  await registerTools(new CostServer());
  return rows;
}

async function main() {
  const profileName = process.argv[2];
  if (!profileName) {
    console.error("Usage: node measure-tool-cost.mjs <profile-name>");
    console.error(`Known profiles: ${Object.keys(profiles.profiles).join(", ")}`);
    process.exit(1);
  }
  const rows = await registeredCostForProfile(profileName);
  const total = rows.reduce((s, r) => s + r.cost, 0);
  console.log(`profile=${profileName} registeredCount=${rows.length} costUnits=${total}`);
}

main();
