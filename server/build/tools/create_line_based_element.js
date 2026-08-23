import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateLineBasedElementTool(server) {
    server.tool("create_line_based_element", "Create one or more line-based elements in Revit such as walls, beams, or pipes. Supports batch creation with detailed parameters including family type ID, start and end points, thickness, height, and level information. All units are in millimeters (mm).", {
        data: z
            .array(z.object({
            category: z
                .string()
                .describe("Revit built-in category (e.g., OST_Walls, OST_StructuralFraming, OST_DuctCurves)"),
            typeId: z
                .number()
                .optional()
                .describe("The ID of the family type to create."),
            locationLine: z
                .object({
                p0: z.object({
                    x: z.number().describe("X coordinate of start point"),
                    y: z.number().describe("Y coordinate of start point"),
                    z: z.number().describe("Z coordinate of start point"),
                }),
                p1: z.object({
                    x: z.number().describe("X coordinate of end point"),
                    y: z.number().describe("Y coordinate of end point"),
                    z: z.number().describe("Z coordinate of end point"),
                }),
            })
                .describe("The line defining the element's location"),
            thickness: z
                .number()
                .describe("Thickness/width of the element (e.g., wall thickness)"),
            height: z
                .number()
                .describe("Height of the element (e.g., wall height)"),
            baseLevel: z.number().describe("Base level height"),
            baseOffset: z.number().describe("Offset from the base level"),
        }))
            .describe("Array of line-based elements to create"),
    }, async (args, extra) => {
        const params = args;
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_line_based_element", params);
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
