import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerApplyViewTemplateTool(server: McpServer) {
  server.tool(
    "apply_view_template",
    "Apply a view template to one or more views. Transfers all display settings (visibility, graphics, scale, detail level) from the template to the target views.",
    {
      templateName: z.string().describe("Name of the view template to apply"),
      viewIds: z.array(z.number()).describe("ElementIds of the target views to apply the template to"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("apply_view_template", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check template name and view IDs exist." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
