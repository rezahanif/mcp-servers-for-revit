import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetScheduleDataTool(server) {
    server.tool("get_schedule_data", "Read cell data from a Revit schedule. Returns the schedule as a structured table with rows and columns.", {
        scheduleId: z.number().optional().describe("ElementId of the schedule to read"),
        scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
        maxRows: z.number().optional().describe("Maximum number of rows to return (default: 100)"),
        offset: z.number().optional().describe("Row offset for pagination (default: 0)"),
        fieldNames: z.array(z.string()).optional().describe("Limit to specific fields/columns"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_schedule_data", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and is readable." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
