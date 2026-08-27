import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { ensureLicensed, envelope } from "../aioconnect.js";

/**
 * Tool tiering — see tool_tiers.json for the full rationale.
 *
 * Only tier-1 tools are registered as typed MCP tools. Tier-2 tools stay in the
 * source tree and in revit_function_registry.json, but are NOT advertised on the
 * wire: their Revit-side command has no C# handler, so registering them would
 * spend tool-surface tokens in every session to advertise a capability that
 * fails at the bridge. They remain reachable through search_revit_api /
 * query_revit_registry and executable through send_code_to_revit.
 */
type ToolTiers = {
  tier1: string[];
  tier2: Record<string, { command: string | null; reason: string }>;
};

export async function registerTools(server: McpServer) {
  // AiConnect: startup license gate — the server refuses to register any
  // tool (and therefore to serve) without a valid MCP_LICENSE_TOKEN.
  const license = await ensureLicensed();

  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);

  const tiers: ToolTiers = JSON.parse(
    fs.readFileSync(path.join(__dirname, "tool_tiers.json"), "utf-8")
  );
  const TIER1 = new Set(tiers.tier1);
  const TIER2 = new Set(Object.keys(tiers.tier2));

  const registeredNames: string[] = [];
  const suppressed: string[] = [];

  // AiConnect: wrap EVERY tool's handler — per-call license recheck + response
  // envelope. Generic monkey-patch of server.tool, so the tool files need zero
  // edits. This is also where tiering is enforced, for the same reason: it is
  // the one place every tool registration passes through, so a tool file cannot
  // opt out of the manifest by accident.
  const origTool = (server as any).tool.bind(server);
  (server as any).tool = (name: string, desc: string, schema: any, handler?: any) => {
    if (TIER2.has(name)) {
      suppressed.push(name);
      return;
    }
    if (!TIER1.has(name)) {
      // Fail loud rather than silently advertising an unclassified tool. A new
      // tool file must declare its tier, which forces the author to answer
      // "does this have a working Revit command handler?" before it can cost
      // anyone context.
      throw new Error(
        `Tool "${name}" appears in neither tier1 nor tier2 of tool_tiers.json. ` +
          `Add it to tier1 if commandset/ implements its command, otherwise tier2.`
      );
    }
    registeredNames.push(name);
    const cb = handler ?? schema;
    const wrapped = async (args: any, extra: any) => {
      // Optional chain, not a bare call: ensureLicensed() returns null when
      // AICONNECT_ENABLE != 1 (the documented standalone/upstream mode), so an
      // unconditional call threw "Cannot read properties of null" on EVERY tool
      // call outside the gateway — including pure-local discovery tools.
      license?.ensureLicensed(); // per-call recheck (cheap HS256)
      const result = await cb(args, extra);
      if (result && Array.isArray(result.content)) {
        result.content = await Promise.all(
          result.content.map(async (c: any) =>
            c.type === "text" ? { ...c, text: await envelope(c.text) } : c
          )
        );
      }
      return result;
    };
    if (handler) return origTool(name, desc, schema, wrapped);
    return origTool(name, schema, wrapped);
  };

  const files = fs.readdirSync(__dirname);

  const KNOWN_NON_TOOLS = new Set([
    "errors.ts", "register.ts", "index.ts",
    "errors.js", "register.js", "index.js",
  ]);

  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      !KNOWN_NON_TOOLS.has(file)
  );

  for (const file of toolFiles) {
    const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
    const module = await import(importPath);

    // ALL register* exports, not just the first. revit_templates.ts exports two
    // (registerListRevitTemplatesTool + registerLoadRevitTemplateTool); the old
    // `.find()` called only one, so load_revit_template silently never reached
    // the tool surface. Same silent-skip family as the 0-byte-file bug — a file
    // present and importable is not proof its tools registered.
    const registerFunctionNames = Object.keys(module).filter(
      (key) => key.startsWith("register") && typeof module[key] === "function"
    );

    if (registerFunctionNames.length > 0) {
      for (const fn of registerFunctionNames) module[fn](server);
    } else {
      console.warn(`Warning: no register function in ${file}`);
    }
  }

  // The loader assertion that caught the 0-byte-file bug, now keyed to the
  // manifest instead of a hand-synced filename list: a tool file that fails to
  // load, or is deleted, shows up here as a missing tier-1 name.
  if (registeredNames.length !== TIER1.size) {
    const got = new Set(registeredNames);
    const missing = tiers.tier1.filter((t) => !got.has(t));
    throw new Error(
      `Tool registration incomplete: expected ${TIER1.size} tier-1 tools, got ` +
        `${registeredNames.length}. Missing: ${missing.join(", ")}`
    );
  }

  console.error(
    `Registered ${registeredNames.length} tier-1 tools; ` +
      `${suppressed.length} tier-2 tools withheld from the tool surface ` +
      `(discoverable via search_revit_api, executable via send_code_to_revit).`
  );
}
