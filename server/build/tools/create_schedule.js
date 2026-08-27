import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateScheduleTool(server) {
    server.tool("create_schedule", "Create a new schedule/view in Revit. Specify category (e.g., Walls, Doors, Rooms, Furniture) and schedule name. Optionally set fields, filters, sorting, and formatting.", {
        scheduleName: z.string().describe("Name for the new schedule"),
        category: z.string().describe("Revit category to schedule (e.g., 'Walls', 'Doors', 'Rooms', 'Furniture', 'Windows')"),
        sheetId: z.number().optional().describe("ElementId of a sheet to place the schedule on (optional)"),
        fields: z.array(z.string()).optional().describe("Field names to include (e.g., ['Name', 'Type', 'Mark', 'Level'])"),
        groupName: z.string().optional().describe("Schedule group/section name"),
        isKeySchedule: z.boolean().optional().describe("Create as a key schedule (for component lookup tables)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_schedule", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and parameters are valid." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
