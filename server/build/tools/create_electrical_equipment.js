import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateElectricalEquipmentTool(server) {
    server.tool("create_electrical_equipment", "Place electrical equipment families in Revit (e.g., panels, transformers, switchgear). All coordinates in millimeters.", {
        data: z
            .array(z.object({
            location: z.object({
                x: z.number().describe("X coordinate in mm"),
                y: z.number().describe("Y coordinate in mm"),
                z: z.number().describe("Z coordinate in mm"),
            }),
            familyTypeName: z.string().describe("Electrical equipment family type name"),
            levelId: z.number().optional().describe("Level ElementId"),
            rotation: z.number().optional().describe("Rotation angle in degrees"),
        }))
            .describe("Array of electrical equipment to place"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_electrical_equipment", args);
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
