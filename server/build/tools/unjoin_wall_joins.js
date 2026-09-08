import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerUnjoinWallJoinsTool(server) {
    server.tool("unjoin_wall_joins", "Unjoin walls from columns and other geometry they're joined to. Commonly used as prep before element coloring/graphics overrides. Pairs are recorded and can be restored with rejoin_wall_joins (same Revit session only).", {
        wallIds: z.array(z.number()).optional().describe("Wall Element IDs to unjoin (optional)"),
        viewId: z.number().optional().describe("View ID — used to scope the wall search when wallIds is omitted"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("unjoin_wall_joins", args);
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
