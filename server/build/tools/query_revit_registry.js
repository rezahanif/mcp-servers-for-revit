import { z } from "zod";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
let _registry = null;
function loadRegistry() {
    if (_registry)
        return _registry;
    _registry = JSON.parse(fs.readFileSync(path.join(__dirname, "revit_function_registry.json"), "utf-8"));
    return _registry;
}
export function registerQueryRevitRegistryTool(server) {
    server.tool("query_revit_registry", "Query the Revit function registry — the tier-2 index of every known Revit capability, including ones with no typed tool. Each entry carries verification_status: 'seeded' means a typed MCP tool exists and you can call it by name; 'unimplemented' means there is no Revit-side handler, so drive it with send_code_to_revit instead. Search by keyword or filter by category/kind.", {
        query: z
            .string()
            .optional()
            .describe("Search keyword to match function path or description"),
        category: z
            .string()
            .optional()
            .describe("Filter by category (creation, query, modification, annotation, data, visualization, system, execution, interoperability, interaction, errors, events, advanced, mep, structural, architecture, core)"),
        kind: z
            .string()
            .optional()
            .describe("Filter by kind: 'Tool' (MCP tool) or 'CSharp' (C# API call)"),
    }, async (args, extra) => {
        try {
            const reg = loadRegistry();
            let entries = reg.entries;
            if (args.query) {
                const q = args.query.toLowerCase();
                entries = entries.filter((e) => e.path.toLowerCase().includes(q) ||
                    e.description.toLowerCase().includes(q));
            }
            if (args.category) {
                entries = entries.filter((e) => e.category === args.category);
            }
            if (args.kind) {
                entries = entries.filter((e) => e.kind === args.kind);
            }
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify({
                            total: entries.length,
                            filters: { query: args.query, category: args.category, kind: args.kind },
                            entries: entries.slice(0, 20),
                            hint: entries.length > 20
                                ? `Showing 20 of ${entries.length}. Narrow with query/category/kind.`
                                : `Found ${entries.length} entries. Entries marked verification_status:'unimplemented' have no typed tool and no Revit command handler — reach them with send_code_to_revit, do not call them by name.`,
                        }, null, 2),
                    },
                ],
            };
        }
        catch (error) {
            const msg = error instanceof Error ? error.message : String(error);
            return {
                content: [{ type: "text", text: JSON.stringify({ error: "registry_query_failed", message: msg }) }],
            };
        }
    });
}
