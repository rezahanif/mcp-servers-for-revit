import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportIfcTool(server) {
    server.tool("export_ifc", "Export a Revit model to IFC format. Supports IFC2x3 and IFC4 with configurable export configurations.", {
        outputPath: z.string().describe("Full path for the output IFC file"),
        exportConfiguration: z.string().optional().describe("IFC export configuration name (e.g., 'IFC2x3 Coordination View', 'IFC4 Reference View')"),
        ifcVersion: z.string().optional().describe("IFC version: 'IFC2x3', 'IFC4'"),
        spaceBoundary: z.string().optional().describe("Space boundary export: 'None', 'ByAutomatic', 'ByFace'"),
        levelOfDetail: z.string().optional().describe("Level of detail: 'Low', 'Medium', 'High'"),
        activeViewOnly: z.boolean().optional().describe("Export only elements visible in active view"),
        exportPartAssemblies: z.boolean().optional().describe("Export part assemblies"),
        exportSchedulesAsPsets: z.boolean().optional().describe("Export schedules as property sets"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_ifc", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and output path is valid." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
