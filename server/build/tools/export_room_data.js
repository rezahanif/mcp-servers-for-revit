import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportRoomDataTool(server) {
    server.tool("export_room_data", "Export all room data from the current Revit project. Returns detailed information about each room including name, number, level, area, volume, perimeter, department, and more. Useful for generating room schedules, space analysis, and facility management data.", {
        includeUnplacedRooms: z
            .boolean()
            .optional()
            .default(false)
            .describe("Whether to include unplaced rooms (rooms not yet placed in the model). Defaults to false."),
        includeNotEnclosedRooms: z
            .boolean()
            .optional()
            .default(false)
            .describe("Whether to include rooms that are not fully enclosed. Defaults to false."),
    }, async (args, extra) => {
        const params = {
            includeUnplacedRooms: args.includeUnplacedRooms ?? false,
            includeNotEnclosedRooms: args.includeNotEnclosedRooms ?? false,
        };
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_room_data", params);
            });
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify(response, null, 2),
                    },
                ],
            };
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
