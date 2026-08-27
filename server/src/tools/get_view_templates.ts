import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetViewTemplatesTool(server: McpServer) {
  server.tool(
    "get_view_templates",
    "List all view templates in the Revit project. Returns template names, IDs, view types, and which views have each template applied.",
    {
      viewType: z.string().optional().describe("Filter by view type (e.g., 'FloorPlan', 'Section', 'CeilingPlan', '3D')"),
      includeAppliedViews: z.boolean().optional().describe("Include list of views each template is applied to (default: false)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_view_templates", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and document is open." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
