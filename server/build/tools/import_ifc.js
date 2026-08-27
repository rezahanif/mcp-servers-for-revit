import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerImportIfcTool(server) {
    server.tool("import_ifc", "Import an IFC file into Revit. Supports IFC2x3 and IFC4 with automatic linking or direct import.", {
        filePath: z.string().describe("Full path to the IFC file to import"),
        placement: z.string().describe("Placement method: 'Link', 'Attach', 'Import'"),
        positioning: z.string().optional().describe("Positioning: 'OriginToOrigin', 'CenterToCenter'"),
        levelId: z.number().optional().describe("Level ElementId for placement"),
        location: z.object({
            x: z.number().describe("X coordinate in mm"),
            y: z.number().describe("Y coordinate in mm"),
            z: z.number().describe("Z coordinate in mm"),
        }).optional().describe("Manual placement location"),
        viewId: z.number().optional().describe("Target view ElementId"),
        moveTo: z.string().optional().describe("Destination folder for linked IFC elements"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("import_ifc", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check IFC file exists and Revit is running." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
