import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreatePointBasedElementTool(server) {
    server.tool("create_point_based_element", "Domain: Architecture — hosted and free-standing components. Create one or more point-based elements in Revit such as doors, windows, or furniture. Supports batch creation with detailed parameters including family type ID, position, dimensions, and level information. All units are in millimeters (mm). This tool does not inspect what already occupies the point it is given, and it does not necessarily attach to the level you expect: read the typeId and baseLevel notes below.", {
        data: z
            .array(z.object({
            name: z
                .string()
                .describe("Description of the element (e.g., door, window)"),
            typeId: z
                .number()
                .optional()
                .describe("ElementId of the FamilySymbol to instantiate. TREAT THIS AS REQUIRED. If it is omitted, 0, " +
                "-1, or names an id that does not resolve, the element is still created, using the first " +
                "active symbol of that category found in the document — arbitrary, and very often the wrong " +
                "family. The substitution is reported in `message` under 'Warnings' while `success` still " +
                "reads true. Resolve a real id with get_available_family_types before calling."),
            locationPoint: z
                .object({
                x: z.number().describe("X coordinate"),
                y: z.number().describe("Y coordinate"),
                z: z.number().describe("Z coordinate"),
            })
                .describe("The position coordinates where the element will be placed"),
            width: z.number().describe("Width of the element in mm"),
            depth: z.number().optional().describe("Depth of the element in mm"),
            height: z.number().describe("Height of the element in mm"),
            baseLevel: z
                .number()
                .describe("Base elevation in mm. This is an ELEVATION, not an ElementId, and it is resolved by NEAREST " +
                "MATCH against the levels that already exist: the level with the smallest |elevation - " +
                "baseLevel| wins, with NO distance limit, and a tie goes to the older level. Creating a level " +
                "and then passing its elevation does NOT guarantee the element lands on it. Pass levelId " +
                "instead whenever you know which level you mean. When the match is not exact the resolved " +
                "level and the difference are reported in `message` under 'Warnings'."),
            levelId: z
                .number()
                .optional()
                .describe("ElementId of the Level to host the element on. PREFER THIS over baseLevel. When set, the level " +
                "is used directly and no elevation matching happens. Get ids from ai_element_filter with " +
                "filterCategory 'OST_Levels'. Omit (or pass -1) to fall back to matching on baseLevel."),
            baseOffset: z
                .number()
                .describe("Offset in mm from the RESOLVED base level — which may not be the level whose elevation you " +
                "passed in baseLevel. The absolute height you asked for is preserved; only the host differs."),
            rotation: z
                .number()
                .optional()
                .describe("Rotation angle in degrees (0-360)"),
            hostWallId: z
                .number()
                .optional()
                .describe("The ElementId of a specific wall to use as host for doors/windows. " +
                "If not provided, the nearest wall will be auto-detected."),
            facingFlipped: z
                .boolean()
                .optional()
                .default(false)
                .describe("Whether to flip the facing direction of the door/window. " +
                "When true, the element faces the opposite side of the wall."),
        }))
            .describe("Array of point-based elements to create"),
    }, async (args, extra) => {
        const params = args;
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_point_based_element", params);
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
