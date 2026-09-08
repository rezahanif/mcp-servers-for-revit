import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerUnjoinColumnJoinsTool(server) {
    server.tool("unjoin_column_joins", "Unjoin columns from neighboring elements (walls, floors, structural framing), centered on the column. Defaults to every column in the project (or in viewId, if given) when columnIds is omitted. Restore with rejoin_wall_joins (same Revit session only).", {
        columnIds: z.array(z.number()).optional().describe("Column Element IDs to process (optional, defaults to all columns)"),
        viewId: z.number().optional().describe("View ID (optional) — scopes the search when columnIds is omitted"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("unjoin_column_joins", args);
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
