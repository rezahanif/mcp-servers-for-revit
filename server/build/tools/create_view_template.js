import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateViewTemplateTool(server) {
    server.tool("create_view_template", "Create a new view template from an existing view in Revit. View templates capture display settings (visibility, graphics, scale, detail level) that can be applied to other views.", {
        name: z.string().describe("Name for the new view template (e.g., 'Floor Plan - Architectural')"),
        sourceViewId: z.number().optional().describe("ElementId of the view to copy settings from (omit for current active view)"),
        viewType: z.string().optional().describe("View type filter for the source (e.g., 'FloorPlan', 'Section', 'CeilingPlan')"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_view_template", args);
            });
            return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
        }
        catch (error) {
            if (error instanceof RevitError) {
                return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
            }
            const msg = error instanceof Error ? error.message : String(error);
            const e = msg.includes("connection") || msg.includes("refused")
                ? new ConnectionError(msg)
                : new RevitError(msg, { error_code: "tool_error", hint: "Check source view exists and template name is unique." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
