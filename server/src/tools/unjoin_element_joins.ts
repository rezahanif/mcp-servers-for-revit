import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerUnjoinElementJoinsTool(server: McpServer) {
  server.tool(
    "unjoin_element_joins",
    "Generic version of unjoin_wall_joins / unjoin_column_joins: unjoins any source category from a given list of target categories. Defaults to 8 target categories (Walls, Floors, Columns, StructuralColumns, StructuralFraming, StructuralFoundation, Roofs, Ceilings) when targetCategories is omitted. Shares the same pair store as unjoin_wall_joins / unjoin_column_joins — restore with rejoin_wall_joins (same Revit session only).",
    {
      sourceCategory: z.string().describe("Source category name, without the OST_ prefix (e.g. StructuralFraming, Walls, Floors)"),
      targetCategories: z.array(z.string()).optional().describe("Target category names (optional, defaults to 8 categories: Walls, Floors, Columns, StructuralColumns, StructuralFraming, StructuralFoundation, Roofs, Ceilings)"),
      elementIds: z.array(z.number()).optional().describe("Source Element IDs to process (optional, defaults to every element in sourceCategory)"),
      viewId: z.number().optional().describe("View ID (optional) — scopes the search when elementIds is omitted"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("unjoin_element_joins", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
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
