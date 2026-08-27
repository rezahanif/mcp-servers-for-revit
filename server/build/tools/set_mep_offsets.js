import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSetMepOffsetsTool(server) {
    server.tool("set_mep_offsets", "Set vertical offsets for MEP elements (ducts, pipes) relative to their level", {
        data: z
            .array(z.object({
            elementId: z.number().describe("Element ID of the MEP element"),
            offset: z.number().describe("Offset from level in mm (positive = above, negative = below)"),
        }))
            .describe("Array of MEP elements to offset"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("set_mep_offsets", args);
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
