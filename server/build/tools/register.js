import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { ensureLicensed, envelope } from "../aioconnect.js";
export async function registerTools(server) {
    // AiConnect: startup license gate — the server refuses to register any
    // tool (and therefore to serve) without a valid MCP_LICENSE_TOKEN.
    const license = await ensureLicensed();
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = path.dirname(__filename);
    const tiers = JSON.parse(fs.readFileSync(path.join(__dirname, "tool_tiers.json"), "utf-8"));
    const TIER1 = new Set(tiers.tier1);
    const TIER2 = new Set(Object.keys(tiers.tier2));
    const notes = JSON.parse(fs.readFileSync(path.join(__dirname, "tool_notes.json"), "utf-8"));
    if (!notes.shared?.trim()) {
        // Same failure class as N21 (assets never copied to build/): a missing or
        // empty asset is silent, and the connector would ship 31 descriptions
        // quietly stripped of the reliability contract.
        throw new Error("tool_notes.json has no `shared` text — refusing to register.");
    }
    const registry = JSON.parse(fs.readFileSync(path.join(__dirname, "revit_function_registry.json"), "utf-8"));
    const categoryByName = new Map(registry.entries
        .filter((e) => e.kind === "Tool" && e.category)
        .map((e) => [e.path, e.category]));
    const profileConfig = JSON.parse(fs.readFileSync(path.join(__dirname, "tool_profiles.json"), "utf-8"));
    const requestedProfile = process.env.MCP_PROFILE || profileConfig.default;
    const activeProfileDef = profileConfig.profiles[requestedProfile];
    if (!activeProfileDef) {
        console.error(`Unknown MCP_PROFILE="${requestedProfile}", falling back to "${profileConfig.default}".`);
    }
    const resolvedProfileName = activeProfileDef ? requestedProfile : profileConfig.default;
    const resolvedProfile = activeProfileDef ?? profileConfig.profiles[profileConfig.default];
    const allowAllCategories = resolvedProfile.categories === "*";
    const allowedCategories = allowAllCategories
        ? null
        : new Set([...profileConfig.base_categories, ...resolvedProfile.categories]);
    const alwaysNames = new Set(profileConfig.always);
    const passesProfile = (name) => {
        if (allowAllCategories || alwaysNames.has(name))
            return true;
        const cat = categoryByName.get(name);
        return !!cat && allowedCategories.has(cat);
    };
    const registeredNames = [];
    const suppressedTier2 = [];
    const suppressedByProfile = [];
    // AiConnect: wrap EVERY tool's handler — per-call license recheck + response
    // envelope. Generic monkey-patch of server.tool, so the tool files need zero
    // edits. This is also where tiering is enforced, for the same reason: it is
    // the one place every tool registration passes through, so a tool file cannot
    // opt out of the manifest by accident.
    const origTool = server.tool.bind(server);
    server.tool = (name, desc, schema, handler) => {
        if (TIER2.has(name)) {
            suppressedTier2.push(name);
            return;
        }
        if (!TIER1.has(name)) {
            // Fail loud rather than silently advertising an unclassified tool. A new
            // tool file must declare its tier, which forces the author to answer
            // "does this have a working Revit command handler?" before it can cost
            // anyone context.
            throw new Error(`Tool "${name}" appears in neither tier1 nor tier2 of tool_tiers.json. ` +
                `Add it to tier1 if commandset/ implements its command, otherwise tier2.`);
        }
        if (!passesProfile(name)) {
            // Implemented, but out of scope for the active discipline profile.
            // Still tier-1 — still reachable if the profile changes, and still
            // discoverable via the tier-2 search path regardless of profile.
            suppressedByProfile.push(name);
            return;
        }
        registeredNames.push(name);
        // Two-arity form (name, schema, handler) carries no description, so there
        // is nothing to append to; every tier-1 tool uses the four-arity form.
        const describedAs = handler ? `${desc}\n\n${notes.shared}` : desc;
        const cb = handler ?? schema;
        const wrapped = async (args, extra) => {
            // Optional chain, not a bare call: ensureLicensed() returns null when
            // AICONNECT_ENABLE != 1 (the documented standalone/upstream mode), so an
            // unconditional call threw "Cannot read properties of null" on EVERY tool
            // call outside the gateway — including pure-local discovery tools.
            license?.ensureLicensed(); // per-call recheck (cheap HS256)
            const result = await cb(args, extra);
            if (result && Array.isArray(result.content)) {
                result.content = await Promise.all(result.content.map(async (c) => c.type === "text" ? { ...c, text: await envelope(c.text) } : c));
            }
            return result;
        };
        if (handler)
            return origTool(name, describedAs, schema, wrapped);
        return origTool(name, schema, wrapped);
    };
    const files = fs.readdirSync(__dirname);
    const KNOWN_NON_TOOLS = new Set([
        "errors.ts", "register.ts", "index.ts",
        "errors.js", "register.js", "index.js",
    ]);
    const toolFiles = files.filter((file) => (file.endsWith(".ts") || file.endsWith(".js")) &&
        !KNOWN_NON_TOOLS.has(file));
    for (const file of toolFiles) {
        const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
        const module = await import(importPath);
        // ALL register* exports, not just the first. revit_templates.ts exports two
        // (registerListRevitTemplatesTool + registerLoadRevitTemplateTool); the old
        // `.find()` called only one, so load_revit_template silently never reached
        // the tool surface. Same silent-skip family as the 0-byte-file bug — a file
        // present and importable is not proof its tools registered.
        const registerFunctionNames = Object.keys(module).filter((key) => key.startsWith("register") && typeof module[key] === "function");
        if (registerFunctionNames.length > 0) {
            for (const fn of registerFunctionNames)
                module[fn](server);
        }
        else {
            console.warn(`Warning: no register function in ${file}`);
        }
    }
    // The loader assertion that caught the 0-byte-file bug, now keyed to the
    // profile-adjusted expected set instead of the flat tier1 count: under a
    // non-"full" profile, fewer than TIER1.size tools are expected to register,
    // so the raw count would otherwise trip this check on every profiled run.
    // Under "full" (categories: "*"), allowedTier1 degenerates to exactly
    // TIER1, so behavior is byte-identical to before profiles existed.
    const allowedTier1 = new Set(tiers.tier1.filter((t) => passesProfile(t)));
    if (registeredNames.length !== allowedTier1.size) {
        const got = new Set(registeredNames);
        const missing = [...allowedTier1].filter((t) => !got.has(t));
        throw new Error(`Tool registration incomplete for profile "${resolvedProfileName}": expected ` +
            `${allowedTier1.size} tools, got ${registeredNames.length}. Missing: ${missing.join(", ")}`);
    }
    console.error(`Registered ${registeredNames.length} tier-1 tools for profile "${resolvedProfileName}"; ` +
        `${suppressedTier2.length} tier-2 tools withheld (discoverable via search_revit_api, ` +
        `executable via send_code_to_revit); ${suppressedByProfile.length} tier-1 tools withheld ` +
        `by profile filtering (still discoverable — profile does not affect the tier-2 escape hatch).`);
}
