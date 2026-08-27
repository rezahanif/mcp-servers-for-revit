import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerSetScheduleFilterTool(server: McpServer) {
  server.tool(
    "set_schedule_filter",
    "Apply or update filters on a Revit schedule. Filters restrict which rows are displayed based on field values.",
    {
      scheduleId: z.number().optional().describe("ElementId of the schedule"),
      scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
      filters: z.array(
        z.object({
          fieldName: z.string().describe("Field name to filter on"),
          filterType: z.string().describe("Filter type: 'Equals', 'NotEquals', 'Greater', 'Less', 'GreaterOrEqual', 'LessOrEqual', 'Contains', 'BeginsWith', 'EndsWith'"),
          value: z.string().describe("Filter value"),
        })
      ).describe("Filters to apply (AND logic between multiple filters)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_schedule_filter", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and filter parameters are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
