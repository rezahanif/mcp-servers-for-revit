import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateStructuralColumnTool(server) {
    server.tool("create_structural_column", "Place structural columns in Revit. All coordinates in millimeters.", {
        data: z
            .array(z.object({
            location: z.object({
                x: z.number().describe("X coordinate in mm"),
                y: z.number().describe("Y coordinate in mm"),
            }),
            columnTypeName: z.string().optional().describe("Structural column family type (e.g., 'W-Wide Flange: W10x12')"),
            baseLevelId: z.number().optional().describe("Base level ElementId"),
            topLevelId: z.number().optional().describe("Top level ElementId"),
            topLevelOffset: z.number().optional().describe("Top offset from top level in mm"),
            baseLevelOffset: z.number().optional().describe("Base offset from base level in mm"),
            rotation: z.number().optional().describe("Rotation angle in degrees"),
        }))
            .describe("Array of structural columns to place"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_structural_column", args);
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
