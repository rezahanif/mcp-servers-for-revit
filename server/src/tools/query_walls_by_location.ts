import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerQueryWallsByLocationTool(server: McpServer) {
  server.tool(
    "query_walls_by_location",
    "Query walls near a coordinate (spatial proximity search, not an attribute filter). Returns thickness, location-line coordinates, and both wall faces for each nearby wall.",
    {
      x: z.number().describe("Search center X coordinate (mm)"),
      y: z.number().describe("Search center Y coordinate (mm)"),
      searchRadius: z.number().describe("Search radius (mm)"),
      level: z.string().optional().describe("Level name to restrict the search to (optional)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("query_walls_by_location", args);
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
