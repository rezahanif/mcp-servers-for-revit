import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerQueryLinkedElementsTool(server: McpServer) {
  server.tool(
    "query_linked_elements",
    "Query elements inside a specific linked model, with category filtering, parameter filters, and custom return fields. Coordinates are automatically transformed into main-model space using the link's transform, so results align with the host document. Get linkInstanceId from get_linked_models first.",
    {
      linkInstanceId: z.number().describe("Link instance ElementId, from get_linked_models"),
      category: z
        .string()
        .describe("Revit category name, e.g. Pipes, Ducts, CableTrays, Walls, StructuralFraming"),
      filters: z
        .array(
          z.object({
            field: z.string().describe("Parameter name (e.g. System Type, Size)"),
            operator: z
              .enum(["equals", "contains", "not_equals", "less_than", "greater_than"])
              .describe("Comparison operator"),
            value: z.string().describe("Value to compare against"),
          })
        )
        .optional()
        .describe("Parameter filters to narrow the linked elements"),
      returnFields: z
        .array(z.string())
        .optional()
        .describe("Extra parameter field names to include in each returned element"),
      maxCount: z.number().optional().default(500).describe("Maximum number of elements to return"),
    },
    async (args, _extra) => {
      const params = args;

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("query_linked_elements", params);
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
