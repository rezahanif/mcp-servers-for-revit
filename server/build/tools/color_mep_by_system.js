import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerColorMepBySystemTool(server) {
    server.tool("color_mep_by_system", "Color MEP elements by system type using OverrideGraphicsSettings for visual differentiation", {
        viewId: z.number().optional().describe("View ElementId (uses active view if omitted)"),
        category: z.enum(["ducts", "pipes", "all"]).describe("MEP category to color"),
        colorMap: z
            .record(z.string(), z.object({
            red: z.number().min(0).max(255).describe("Red component (0-255)"),
            green: z.number().min(0).max(255).describe("Green component (0-255)"),
            blue: z.number().min(0).max(255).describe("Blue component (0-255)"),
        }))
            .optional()
            .describe("System name to color mapping. Default uses standard HVAC/piping colors."),
        applyToAllViews: z.boolean().default(false).describe("Apply color overrides to all views"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("color_mep_by_system", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and parameters are valid." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
