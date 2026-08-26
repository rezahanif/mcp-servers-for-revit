import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetAnalyticalModelTool(server: McpServer) {
  server.tool(
    "get_analytical_model",
    "Query the analytical model for structural elements, retrieving forces, reactions, and member properties",
    {
      elementIds: z.array(z.number()).optional().describe("Element IDs to query (queries all structural if omitted)"),
      modelType: z.enum(["member", "wall", "opening", "all"]).default("all").describe("Analytical model type to query"),
      includeSupportConditions: z.boolean().default(true).describe("Include support/boundary conditions"),
      includeLoading: z.boolean().default(false).describe("Include applied loads"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_analytical_model", args);
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
