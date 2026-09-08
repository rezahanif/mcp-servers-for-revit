import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateGreenMaterialTool(server) {
    server.tool("create_green_material", "Create a standalone Revit Material with a specified RGB color (used for green building assessment or visual verification).", {
        materialName: z.string().describe("Name of the material to create"),
        r: z.number().int().min(0).max(255).optional().default(235).describe("Red component (0-255)"),
        g: z.number().int().min(0).max(255).optional().default(245).describe("Green component (0-255)"),
        b: z.number().int().min(0).max(255).optional().default(240).describe("Blue component (0-255)"),
        dryRun: z.boolean().optional().default(false).describe("If true, simulate material creation without modifying the model"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_green_material", args);
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
