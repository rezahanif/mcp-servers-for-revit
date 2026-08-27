import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerAddScheduleFieldTool(server: McpServer) {
  server.tool(
    "add_schedule_field",
    "Add one or more fields to an existing Revit schedule. Fields are Revit built-in or shared parameter names.",
    {
      scheduleId: z.number().optional().describe("ElementId of the schedule to modify"),
      scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
      fields: z.array(
        z.object({
          name: z.string().describe("Field name (e.g., 'Mark', 'Type Comments', 'Cost', 'Level')"),
          nameOrFieldName: z.string().optional().describe("Parameter name or BuiltInParameter name"),
          unitType: z.string().optional().describe("Unit type override (e.g., 'Length', 'Area')"),
          calculationType: z.string().optional().describe("Calculation type: 'NoCalculation', 'Count', 'Formula'"),
          formula: z.string().optional().describe("Formula for calculated fields"),
        })
      ).describe("Fields to add to the schedule"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("add_schedule_field", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and field names are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
