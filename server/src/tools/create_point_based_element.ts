import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreatePointBasedElementTool(server: McpServer) {
  server.tool(
    "create_point_based_element",
    "Create one or more point-based elements in Revit such as doors, windows, or furniture. Supports batch creation with detailed parameters including family type ID, position, dimensions, and level information. All units are in millimeters (mm).",
    {
      data: z
        .array(
          z.object({
            name: z
              .string()
              .describe("Description of the element (e.g., door, window)"),
            typeId: z
              .number()
              .optional()
              .describe("The ID of the family type to create."),
            locationPoint: z
              .object({
                x: z.number().describe("X coordinate"),
                y: z.number().describe("Y coordinate"),
                z: z.number().describe("Z coordinate"),
              })
              .describe(
                "The position coordinates where the element will be placed"
              ),
            width: z.number().describe("Width of the element in mm"),
            depth: z.number().optional().describe("Depth of the element in mm"),
            height: z.number().describe("Height of the element in mm"),
            baseLevel: z.number().describe("Base level height"),
            baseOffset: z.number().describe("Offset from the base level"),
            rotation: z
              .number()
              .optional()
              .describe("Rotation angle in degrees (0-360)"),
            hostWallId: z
              .number()
              .optional()
              .describe(
                "The ElementId of a specific wall to use as host for doors/windows. " +
                "If not provided, the nearest wall will be auto-detected."
              ),
            facingFlipped: z
              .boolean()
              .optional()
              .default(false)
              .describe(
                "Whether to flip the facing direction of the door/window. " +
                "When true, the element faces the opposite side of the wall."
              ),
          })
        )
        .describe("Array of point-based elements to create"),
    },
    async (args, extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "create_point_based_element",
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
