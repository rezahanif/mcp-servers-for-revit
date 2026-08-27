import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSetMepSizesTool(server) {
    server.tool("set_mep_sizes", "Set sizes and insulation properties on MEP elements (ducts, pipes)", {
        data: z
            .array(z.object({
            elementId: z.number().describe("Element ID of the MEP element"),
            diameter: z.number().optional().describe("Outer diameter in mm"),
            width: z.number().optional().describe("Width in mm (rectangular ducts)"),
            height: z.number().optional().describe("Height in mm (rectangular ducts)"),
            insulationThickness: z.number().optional().describe("Insulation thickness in mm"),
            insulationType: z.string().optional().describe("Insulation type name"),
        }))
            .describe("Array of MEP elements to resize"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("set_mep_sizes", args);
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
