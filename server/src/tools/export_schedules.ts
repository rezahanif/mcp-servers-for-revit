import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerExportSchedulesTool(server: McpServer) {
  server.tool(
    "export_schedules",
    "Export all (or selected) schedules from Revit to Excel or CSV. Produces a multi-sheet workbook or multi-file CSV output.",
    {
      outputPath: z.string().describe("Output folder path for exported files"),
      format: z.string().describe("Export format: 'Excel' (.xlsx) or 'CSV'"),
      scheduleNames: z.array(z.string()).optional().describe("Specific schedule names to export (default: all schedules)"),
      includeScheduleNames: z.boolean().optional().describe("Include schedule name as header in output"),
      delimiter: z.string().optional().describe("CSV delimiter (default: ',') — ignored for Excel format"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("export_schedules", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and output path is writable." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
