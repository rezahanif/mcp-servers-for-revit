import { z } from "zod";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
let _templates = null;
function loadTemplates() {
    if (_templates)
        return _templates;
    _templates = JSON.parse(fs.readFileSync(path.join(__dirname, "revit_templates.json"), "utf-8"));
    return _templates;
}
export function registerListRevitTemplatesTool(server) {
    server.tool("list_revit_templates", "Browse available Revit workflow templates — tested step-by-step guides for common BIM operations.", {
        stage: z
            .string()
            .optional()
            .describe("Filter by workflow stage (creation, data, modification, execution)"),
    }, async (args, extra) => {
        try {
            const data = loadTemplates();
            let templates = data.templates;
            if (args.stage) {
                templates = templates.filter((t) => t.stage === args.stage);
            }
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify({
                            total: templates.length,
                            filter: args.stage || "all",
                            templates: templates.map((t) => ({
                                id: t.id,
                                name: t.name,
                                description: t.description,
                                stage: t.stage,
                                tools: t.tools_used,
                                time: t.estimated_time,
                            })),
                            hint: "Use load_revit_template(id) to get full step-by-step instructions.",
                        }, null, 2),
                    },
                ],
            };
        }
        catch (error) {
            const msg = error instanceof Error ? error.message : String(error);
            return {
                content: [{ type: "text", text: JSON.stringify({ error: "list_templates_failed", message: msg }) }],
            };
        }
    });
}
export function registerLoadRevitTemplateTool(server) {
    server.tool("load_revit_template", "Load a full Revit workflow template — returns step-by-step instructions with tool calls and parameters.", {
        id: z
            .string()
            .describe("Template ID (e.g. 'create_building_layout', 'query_and_export', 'structural_framing')"),
    }, async (args, extra) => {
        try {
            const data = loadTemplates();
            const template = data.templates.find((t) => t.id === args.id);
            if (!template) {
                const available = data.templates.map((t) => t.id);
                return {
                    content: [
                        {
                            type: "text",
                            text: JSON.stringify({
                                error: "template_not_found",
                                id: args.id,
                                available,
                                hint: `Template "${args.id}" not found. Available: ${available.join(", ")}`,
                            }),
                        },
                    ],
                };
            }
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify({
                            id: template.id,
                            name: template.name,
                            description: template.description,
                            stage: template.stage,
                            steps: template.steps,
                            tools_used: template.tools_used,
                            estimated_time: template.estimated_time,
                            hint: "Follow the steps in order. Each step maps to a tool call.",
                        }, null, 2),
                    },
                ],
            };
        }
        catch (error) {
            const msg = error instanceof Error ? error.message : String(error);
            return {
                content: [{ type: "text", text: JSON.stringify({ error: "load_template_failed", message: msg }) }],
            };
        }
    });
}
