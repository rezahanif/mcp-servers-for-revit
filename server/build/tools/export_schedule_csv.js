import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportScheduleCsvTool(server) {
    server.tool("export_schedule_csv", "Export a Revit schedule to CSV format. Returns the file path of the exported CSV.", {
        scheduleId: z.number().optional().describe("ElementId of the schedule to export"),
        scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
        outputPath: z.string().optional().describe("Output file path for the CSV (defaults to project folder)"),
        delimiter: z.string().optional().describe("CSV delimiter character (default: ',')"),
        includeHeaders: z.boolean().optional().describe("Include column headers (default: true)"),
        exportView: z.boolean().optional().describe("Export visible view only vs all data (default: true)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_schedule_csv", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and output path is writable." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
