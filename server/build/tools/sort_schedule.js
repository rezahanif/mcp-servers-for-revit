import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSortScheduleTool(server) {
    server.tool("sort_schedule", "Set sort and grouping criteria for a Revit schedule. Controls row ordering and grouping/blank lines.", {
        scheduleId: z.number().optional().describe("ElementId of the schedule"),
        scheduleName: z.string().optional().describe("Name of the schedule (alternative to scheduleId)"),
        sortFields: z.array(z.object({
            fieldName: z.string().describe("Field name to sort by"),
            ascending: z.boolean().describe("Sort order: true for ascending, false for descending"),
            groupHeader: z.boolean().optional().describe("Show group header for this field"),
            groupFooter: z.boolean().optional().describe("Show group footer/subtotals for this field"),
            insertBlank: z.boolean().optional().describe("Insert blank line between groups"),
        })).describe("Sort criteria applied in order (first field is primary sort)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("sort_schedule", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check schedule exists and sort fields are valid." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
