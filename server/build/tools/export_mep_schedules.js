import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportMepSchedulesTool(server) {
    server.tool("export_mep_schedules", "Export MEP data to Revit schedules (duct, pipe, equipment counts and quantities)", {
        scheduleName: z.string().optional().describe("Name for the schedule view"),
        category: z.enum(["ducts", "pipes", "electrical_equipment", "plumbing_fixtures", "all"]).describe("MEP category for the schedule"),
        systemTypeName: z.string().optional().describe("Filter by system type"),
        includeLengths: z.boolean().default(true).describe("Include element lengths"),
        includeAreas: z.boolean().default(false).describe("Include surface areas"),
        includeCount: z.boolean().default(true).describe("Include element counts"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_mep_schedules", args);
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
