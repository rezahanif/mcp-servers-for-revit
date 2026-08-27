import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportDfxTool(server) {
    server.tool("export_dfx", "Export a Revit view or sheet to DXF format. Similar to DWG export but uses the DXF interchange format.", {
        viewId: z.number().optional().describe("ElementId of the view/sheet to export (default: active view)"),
        viewName: z.string().optional().describe("Name of the view to export (alternative to viewId)"),
        outputPath: z.string().describe("Full path for the output DXF file"),
        exportOptions: z.object({
            exportUnits: z.string().optional().describe("Export units: 'Meter', 'Foot', 'Millimeter'"),
            layerNaming: z.string().optional().describe("Layer naming: 'ExportLayersByLevel', 'ExportLayersByCategory'"),
            exportFileFormat: z.string().optional().describe("DXF version: 'R2018', 'R2015', 'R2013'"),
            exportInActiveViewOnly: z.boolean().optional().describe("Export only elements visible in active view"),
        }).optional().describe("DXF export configuration options"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_dfx", args);
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
