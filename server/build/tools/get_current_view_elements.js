import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetCurrentViewElementsTool(server) {
    server.tool("get_current_view_elements", "Get elements from the current active view in Revit. You can filter by model categories (like Walls, Floors) or annotation categories (like Dimensions, Text). Use includeHidden to show/hide invisible elements and limit to control the number of returned elements.", {
        modelCategoryList: z
            .array(z.string())
            .optional()
            .describe("List of Revit model category names (e.g., 'OST_Walls', 'OST_Doors', 'OST_Floors')"),
        annotationCategoryList: z
            .array(z.string())
            .optional()
            .describe("List of Revit annotation category names (e.g., 'OST_Dimensions', 'OST_WallTags', 'OST_TextNotes')"),
        includeHidden: z
            .boolean()
            .optional()
            .describe("Whether to include hidden elements in the results"),
        limit: z
            .number()
            .optional()
            .describe("Maximum number of elements to return"),
    }, async (args, extra) => {
        const params = {
            modelCategoryList: args.modelCategoryList || [],
            annotationCategoryList: args.annotationCategoryList || [],
            includeHidden: args.includeHidden || false,
            limit: args.limit || 100,
        };
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_current_view_elements", params);
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
