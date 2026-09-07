import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateDirectShapeTool(server) {
    server.tool("create_direct_shape", "Create custom 3D solid geometry in Revit using DirectShape (ideal for civil engineering: retaining walls, bridge piers, abutments, box culverts, terrain blocks). Supports box solids and polygon-profile extrusions.", {
        name: z.string().describe("Descriptive name for the custom solid element"),
        category: z.string().optional().describe("Revit category for the element (e.g., 'OST_GenericModel', 'OST_Walls', 'OST_StructuralFraming', 'OST_Site'). Defaults to 'OST_GenericModel'."),
        shapeType: z.enum(["box", "polygon_extrusion"]).optional().describe("Type of solid geometry to generate: 'box' or 'polygon_extrusion'"),
        origin: z.object({
            x: z.number().describe("X origin in mm"),
            y: z.number().describe("Y origin in mm"),
            z: z.number().describe("Z origin in mm"),
        }).describe("Origin point for placement"),
        length: z.number().optional().describe("Length in mm along X (for box)"),
        width: z.number().optional().describe("Width in mm along Y (for box)"),
        height: z.number().describe("Extrusion height in mm along Z"),
        polygonPoints: z.array(z.object({
            x: z.number().describe("Profile point X offset in mm"),
            y: z.number().describe("Profile point Y offset in mm"),
        })).optional().describe("Array of 2D coordinates defining the cross-section boundary loop for polygon_extrusion"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_direct_shape", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check coordinates and polygon loop is non-self-intersecting." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
