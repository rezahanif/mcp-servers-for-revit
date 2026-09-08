import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerColorizeClashesTool(server) {
    server.tool("colorize_clashes", "Apply graphic overrides in a view to the elements found by check_clashes, so clashes are visible without reading JSON. colorScheme 'by_category_b' colors each clashing element by its CategoryB (the second element in the pair); 'by_clash_type' colors by ClashType (e.g. HardClash vs WallOverlap). Pass the exact object check_clashes returned as clashData.", {
        clashData: z
            .object({})
            .passthrough()
            .describe("The full response object returned by check_clashes (contains a Response array of clash pairs)"),
        colorScheme: z
            .enum(["by_category_b", "by_clash_type"])
            .optional()
            .default("by_category_b")
            .describe("How to choose the override color per clash"),
        viewId: z.number().optional().describe("Target view ElementId; defaults to the active view"),
    }, async (args, _extra) => {
        const params = args;
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("colorize_clashes", params);
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
