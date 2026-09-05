import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateSurfaceBasedElementTool(server: McpServer) {
  server.tool(
    "create_surface_based_element",
    "Domain: Architecture — planar geometry. Create one or more surface-based elements in Revit such as floors, ceilings, or roofs from a closed boundary loop. Supports batch creation with detailed parameters including family type ID, boundary lines, thickness, and level information. All units are in millimeters (mm). This tool does not inspect what already occupies the boundary it is given, and it does not necessarily attach to the level you expect: read the baseLevel and thickness notes below.",
    {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Description of the element (e.g., floor, ceiling)"),
            category: z
              .enum(["OST_Floors", "OST_Ceilings", "OST_Roofs"])
              .optional()
              .describe("The Revit built-in category for the element. Use OST_Floors for floors, OST_Ceilings for ceilings, OST_Roofs for roofs. If not specified, will be determined from typeId."),
            typeId: z
              .number()
              .optional()
              .describe("The ID of the family type to create."),
            boundary: z
              .object({
                outerLoop: z
                  .array(
                    z.object({
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
                  )
                  .min(3)
                  .describe("Array of line segments defining the boundary"),
              })
              .describe("Boundary definition with outer loop"),
            thickness: z
              .number()
              .describe(
                "Thickness in mm. Unlike the wall case in create_line_based_element, this value IS honoured — " +
                  "but by DUPLICATING the base type into a new project type named '<base><thickness>mm' and " +
                  "editing its compound structure. Every distinct thickness therefore adds a type to the " +
                  "document. Reuse an existing type via typeId when one already has the thickness you want."
              ),
            baseLevel: z
              .number()
              .describe(
                "Base elevation in mm. This is an ELEVATION, not an ElementId, and it is resolved by NEAREST " +
                  "MATCH against the levels that already exist: the level with the smallest |elevation - " +
                  "baseLevel| wins, with NO distance limit, and a tie goes to the older level. Creating a level " +
                  "and then passing its elevation does NOT guarantee the element lands on it. Pass levelId " +
                  "instead whenever you know which level you mean. When the match is not exact the resolved " +
                  "level and the difference are reported in `message` under 'Warnings'."
              ),
            levelId: z
              .number()
              .optional()
              .describe(
                "ElementId of the Level to host the element on. PREFER THIS over baseLevel. When set, the level " +
                  "is used directly and no elevation matching happens. Get ids from ai_element_filter with " +
                  "filterCategory 'OST_Levels'. Omit (or pass -1) to fall back to matching on baseLevel."
              ),
            baseOffset: z
              .number()
              .describe(
                "Offset in mm from the RESOLVED base level — which may not be the level whose elevation you " +
                  "passed in baseLevel. The absolute height you asked for is preserved; only the host differs."
              ),
          })
        )
        .describe("Array of surface-based elements to create"),
    },
    async (args, extra) => {
      const params = args;
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_surface_based_element",
            params
          );
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and parameters are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
