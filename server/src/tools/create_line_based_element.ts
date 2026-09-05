import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateLineBasedElementTool(server: McpServer) {
  server.tool(
    "create_line_based_element",
    "Domain: Architecture, Structure, MEP — linear geometry. Create one or more line-based elements in Revit such as walls, beams, or pipes. Supports batch creation with detailed parameters including family type ID, start and end points, thickness, height, and level information. All units are in millimeters (mm). This tool does not inspect what already occupies the line it is given, and it does not necessarily attach to the level you expect: read the typeId, baseLevel and thickness notes below, which describe behaviour that has silently produced wrong models.",
    {
      data: z
        .array(
          z.object({
            category: z
              .string()
              .describe("Revit built-in category (e.g., OST_Walls, OST_StructuralFraming, OST_DuctCurves)"),
            typeId: z
              .number()
              .optional()
              .describe(
                "ElementId of the WallType / DuctType / FamilySymbol to instantiate. TREAT THIS AS REQUIRED. " +
                  "If it is omitted, 0, -1, or names an id that does not resolve, the element is still created, " +
                  "using the FIRST type of that category found in the document — which is arbitrary and is very " +
                  "often the wrong material or assembly. The substitution is reported in `message` under " +
                  "'Warnings' while `success` still reads true, so a caller that checks only `success` will not " +
                  "notice it. Resolve a real id with get_available_family_types before calling."
              ),
            levelId: z
              .number()
              .optional()
              .describe(
                "ElementId of the Level to host the element on. PREFER THIS over baseLevel. When set, the level " +
                  "is used directly and no elevation matching happens, which is the only way to place an element " +
                  "on a specific level with certainty. Get ids from ai_element_filter with " +
                  "filterCategory 'OST_Levels'. Omit (or pass -1) to fall back to matching on baseLevel."
              ),
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
              .describe(
                "Nominal thickness/width in mm. IGNORED FOR WALLS. A wall's width comes from its WallType's " +
                  "compound structure, and this tool does not edit or duplicate that type — Wall.Create is " +
                  "called with the type id alone. To get a different wall width you must pass a different " +
                  "typeId, not a different thickness. If the value here disagrees with the resolved type's " +
                  "actual width, that is reported in `message` under 'Warnings'. Still meaningful for the " +
                  "non-wall categories that size from it."
              ),
            height: z
              .number()
              .describe(
                "Height of the element in mm (e.g., wall height). The TOP level is matched by the same " +
                  "nearest-elevation rule as baseLevel, from baseLevel + baseOffset + height, so a tall " +
                  "element can silently attach its top to an unrelated level."
              ),
            baseLevel: z
              .number()
              .describe(
                "Base elevation in mm. This is an ELEVATION, not an ElementId, and it is resolved by NEAREST " +
                  "MATCH against the levels that already exist: the level with the smallest |elevation - " +
                  "baseLevel| wins, with NO distance limit, and a tie goes to the older level. Two consequences " +
                  "that have produced wrong models: (1) creating a level and then passing its elevation does " +
                  "NOT guarantee the element lands on it — a pre-existing level at the same or a nearer " +
                  "elevation takes it; (2) a value between two levels silently snaps to one of them. Pass " +
                  "levelId instead whenever you know which level you mean. When the match is not exact the " +
                  "resolved level and the difference are reported in `message` under 'Warnings'."
              ),
            baseOffset: z
              .number()
              .describe(
                "Offset in mm from the RESOLVED base level — which may not be the level whose elevation you " +
                  "passed in baseLevel. It is computed as (baseLevel + baseOffset) - resolvedLevel.Elevation, " +
                  "so the element still lands at the absolute height you asked for; only its host differs."
              ),
          })
        )
        .describe("Array of line-based elements to create"),
    },
    async (args, extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_line_based_element",
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
