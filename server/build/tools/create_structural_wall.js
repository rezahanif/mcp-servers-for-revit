import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateStructuralWallTool(server) {
    server.tool("create_structural_wall", "Create structural walls in Revit with specified type, level, and constraints. All coordinates in millimeters.", {
        data: z
            .array(z.object({
            startPoint: z.object({
                x: z.number().describe("Start X coordinate in mm"),
                y: z.number().describe("Start Y coordinate in mm"),
                z: z.number().describe("Start Z coordinate in mm"),
            }),
            endPoint: z.object({
                x: z.number().describe("End X coordinate in mm"),
                y: z.number().describe("End Y coordinate in mm"),
                z: z.number().describe("End Z coordinate in mm"),
            }),
            wallTypeName: z.string().optional().describe("Structural wall type name"),
            levelId: z.number().optional().describe("Level ElementId"),
            height: z.number().optional().describe("Wall height in mm"),
            baseOffset: z.number().optional().describe("Base offset from level in mm"),
            structuralUsage: z.enum(["bearing", "shear", "curtain", "generic"]).default("bearing").describe("Structural usage"),
        }))
            .describe("Array of structural walls to create"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_structural_wall", args);
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
