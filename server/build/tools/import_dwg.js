import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerImportDwgTool(server) {
    server.tool("import_dwg", "Import a DWG file into Revit. Supports positioning, scaling, and layer mapping options.", {
        filePath: z.string().describe("Full path to the DWG file to import"),
        placement: z.string().describe("Placement method: 'Link', 'Attach', 'Import'"),
        importUnits: z.string().optional().describe("Import units: 'Meter', 'Foot', 'Millimeter', 'AutoDetect'"),
        positioning: z.string().optional().describe("Positioning: 'OriginToOrigin', 'CenterToCenter', 'Manual'"),
        levelId: z.number().optional().describe("Level ElementId to place imported geometry"),
        location: z.object({
            x: z.number().describe("X coordinate in mm"),
            y: z.number().describe("Y coordinate in mm"),
            z: z.number().describe("Z coordinate in mm"),
        }).optional().describe("Manual placement location (used when positioning='Manual')"),
        viewId: z.number().optional().describe("Target view ElementId"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("import_dwg", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check DWG file exists and Revit is running." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
