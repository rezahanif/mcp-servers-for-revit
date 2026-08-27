import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerModifyScheduleCellTool(server: McpServer) {
  server.tool(
    "modify_schedule_cell",
    "Edit cell values in a Revit schedule. Modify text/numeric values for specific cells identified by row and column.",
    {
      scheduleId: z.number().optional().describe("ElementId of the schedule to modify"),
      scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
      cells: z.array(
        z.object({
          row: z.number().describe("Row index (0-based, excluding header)"),
          column: z.string().describe("Column/field name"),
          value: z.string().describe("New value for the cell"),
        })
      ).describe("Cells to modify"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("modify_schedule_cell", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and cell coordinates are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
