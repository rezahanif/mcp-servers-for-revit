import { z } from "zod";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
let _index = null;
function loadIndex() {
    if (_index)
        return _index;
    _index = JSON.parse(fs.readFileSync(path.join(__dirname, "revit_api_index.json"), "utf-8"));
    return _index;
}
export function registerListRevitApiCategoriesTool(server) {
    server.tool("list_revit_api_categories", "Browse Revit API namespaces by category. Returns all 30 namespaces grouped by domain (core, architecture, structural, mep, etc.).", {
        category: z
            .string()
            .optional()
            .describe("Filter by relevance category (core, architecture, structural, mep, analysis, visualization, events, advanced, interoperability, infrastructure, lighting, reality-capture, fabrication, errors)"),
    }, async (args, extra) => {
        try {
            const index = loadIndex();
            let namespaces = index.namespaces;
            if (args.category) {
                namespaces = namespaces.filter((ns) => ns.relevance === args.category);
            }
            const result = namespaces.map((ns) => ({
                name: ns.name,
                description: ns.description,
                class_count: ns.classes.length,
                relevance: ns.relevance,
            }));
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify({
                            total: result.length,
                            filter: args.category || "all",
                            namespaces: result,
                            hint: "Use search_revit_api to find specific classes within a namespace.",
                        }, null, 2),
                    },
                ],
            };
        }
        catch (error) {
            const msg = error instanceof Error ? error.message : String(error);
            return {
                content: [{ type: "text", text: JSON.stringify({ error: "list_failed", message: msg }) }],
            };
        }
    });
}
