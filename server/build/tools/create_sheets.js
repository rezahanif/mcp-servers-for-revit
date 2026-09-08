import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateSheetsTool(server) {
    server.tool("create_sheets", "Batch-create blank sheets from a list of sheet numbers and names, all using the same title block type.", {
        titleBlockId: z.number().describe("Element ID of the title block type to use for every sheet"),
        sheets: z
            .array(z.object({
            number: z.string().describe("Sheet number (e.g. 'A101')"),
            name: z.string().describe("Sheet name (e.g. 'Level 1 Floor Plan')"),
        }))
            .describe("List of sheets to create"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_sheets", args);
            });
            return {
                content: [{ type: "text", text: JSON.stringify(response, null, 2) }],
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
