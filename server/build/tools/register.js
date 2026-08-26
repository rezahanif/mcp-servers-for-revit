import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { ensureLicensed, envelope } from "../aioconnect.js";
export async function registerTools(server) {
    // AiConnect: startup license gate — the server refuses to register any
    // tool (and therefore to serve) without a valid MCP_LICENSE_TOKEN.
    const license = await ensureLicensed();
    // AiConnect: wrap EVERY tool's handler — per-call license recheck + response
    // envelope. Generic monkey-patch of server.tool, so the 24 tool files need
    // zero edits (Phase 5 contract: structured envelope everywhere).
    const origTool = server.tool.bind(server);
    server.tool = (name, desc, schema, handler) => {
        const cb = handler ?? schema;
        const wrapped = async (args, extra) => {
            license.ensureLicensed(); // per-call recheck (cheap HS256)
            const result = await cb(args, extra);
            if (result && Array.isArray(result.content)) {
                result.content = await Promise.all(result.content.map(async (c) => c.type === "text" ? { ...c, text: await envelope(c.text) } : c));
            }
            return result;
        };
        if (handler)
            return origTool(name, desc, schema, wrapped);
        return origTool(name, schema, wrapped);
    };
    // All expected tool files — keep in sync with actual .ts files in this directory.
    // Excludes: errors.ts (typed errors), register.ts (this file), index.ts (barrel).
    const EXPECTED_TOOLS = [
        "ai_element_filter.ts",
        "analyze_model_statistics.ts",
        "color_elements.ts",
        "create_dimensions.ts",
        "create_grid.ts",
        "create_level.ts",
        "create_line_based_element.ts",
        "create_point_based_element.ts",
        "create_room.ts",
        "create_structural_framing_system.ts",
        "create_surface_based_element.ts",
        "delete_element.ts",
        "export_room_data.ts",
        "get_available_family_types.ts",
        "get_current_view_elements.ts",
        "get_current_view_info.ts",
        "get_material_quantities.ts",
        "get_selected_elements.ts",
        "list_revit_api_categories.ts",
        "operate_element.ts",
        "query_revit_registry.ts",
        "query_stored_data.ts",
        "revit_templates.ts",
        "search_revit_api.ts",
        "send_code_to_revit.ts",
        "store_project_data.ts",
        "store_room_data.ts",
        "tag_all_rooms.ts",
        "tag_all_walls.ts",
    ];
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = path.dirname(__filename);
    const files = fs.readdirSync(__dirname);
    const KNOWN_NON_TOOLS = new Set(["errors.ts", "register.ts", "index.ts", "errors.js", "register.js", "index.js"]);
    const toolFiles = files.filter((file) => (file.endsWith(".ts") || file.endsWith(".js")) &&
        !KNOWN_NON_TOOLS.has(file));
    let registered = 0;
    for (const file of toolFiles) {
        const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
        const module = await import(importPath);
        const registerFunctionName = Object.keys(module).find((key) => key.startsWith("register") && typeof module[key] === "function");
        if (registerFunctionName) {
            module[registerFunctionName](server);
            registered++;
            console.error(`Registered tool: ${file}`);
        }
        else {
            console.warn(`Warning: no register function in ${file}`);
        }
    }
    if (registered < EXPECTED_TOOLS.length) {
        const missing = EXPECTED_TOOLS.filter((t) => !toolFiles.some((f) => f.replace(/\.(ts|js)$/, ".js") === t.replace(/\.ts$/, ".js")));
        throw new Error(`Tool registration incomplete: expected ${EXPECTED_TOOLS.length}, got ${registered}. Missing: ${missing.join(", ")}`);
    }
}
