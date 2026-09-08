import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
const panelTypeDefSchema = z.object({
    id: z.string().describe("Type code (e.g. 'A')"),
    name: z.string().optional().describe("Type name"),
    width: z.number().describe("Width (mm)"),
    height: z.number().optional().describe("Height (mm)"),
    depth: z.number().optional().describe("Arc depth (mm)"),
    thickness: z.number().optional().describe("Panel thickness (mm)"),
    curveType: z.enum(["concave", "convex"]).optional(),
    color: z.string().describe("Color (HEX)"),
    geometryType: z
        .enum(["curved_panel", "beveled_opening", "angled_panel", "rounded_opening", "flat_panel"])
        .optional(),
});
export function registerCreateFacadeFromAnalysisTool(server) {
    server.tool("create_facade_from_analysis", "Batch-create a full facade based on an analysis result. Creates multiple DirectShape panels in front of a wall, supporting multiple panel types and arrangement patterns.", {
        wallId: z.number().optional().describe("Element ID of the target wall (optional — falls back to current selection)"),
        facadeLayers: z
            .object({
            outer: z
                .object({
                offset: z.number().default(200).describe("Offset from wall face (mm), default 200"),
                panelTypes: z
                    .array(panelTypeDefSchema)
                    .describe("Array of panel type definitions"),
                pattern: z
                    .array(z.string())
                    .describe("Arrangement matrix (e.g. ['ABABAB', 'BABABA']), one string per floor row, one character per column"),
                gap: z.number().default(20).describe("Gap between panels (mm), default 20"),
                horizontalBandHeight: z.number().optional().describe("Horizontal divider band height (mm)"),
                floorHeight: z.number().default(3600).describe("Floor-to-floor height (mm), default 3600"),
            })
                .describe("Outer layer panel definition"),
        })
            .describe("Facade layer definitions"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_facade_from_analysis", args);
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
