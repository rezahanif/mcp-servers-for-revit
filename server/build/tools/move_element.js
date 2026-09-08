import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerMoveElementTool(server) {
    server.tool("move_element", "Move the specified Revit element by a dx, dy, dz displacement (mm).", {
        elementId: z.number().describe("The Element ID to move"),
        dx: z.number().optional().default(0).describe("X-axis displacement (mm)"),
        dy: z.number().optional().default(0).describe("Y-axis displacement (mm)"),
        dz: z.number().optional().default(0).describe("Z-axis displacement (mm)"),
    }, async (args, _extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("move_element", args);
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
