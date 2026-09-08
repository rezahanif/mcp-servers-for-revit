import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetElementGeometryTool(server: McpServer) {
  server.tool(
    "get_element_geometry",
    "Extract geometry for an element, from either the main model or a linked model. geometryType selects centerline (curve-based elements), boundingbox, solid (volume/surface-area statistics), or all. When linkInstanceId is given, coordinates are transformed into main-model space unless applyTransform is false.",
    {
      elementId: z.number().describe("Element ElementId"),
      linkInstanceId: z
        .number()
        .optional()
        .describe("Link instance ElementId if this element lives in a linked model"),
      geometryType: z
        .enum(["centerline", "boundingbox", "solid", "all"])
        .optional()
        .default("centerline")
        .describe("Which geometry representation to return"),
      applyTransform: z
        .boolean()
        .optional()
        .default(true)
        .describe("Whether to apply the link's transform to returned coordinates"),
    },
    async (args, _extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_geometry", params);
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
