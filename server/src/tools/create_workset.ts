import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateWorksetTool(server: McpServer) {
  server.tool(
    "create_workset",
    "Create a new workset in the Revit project. Worksets enable worksharing by allowing team members to partition the model into editable segments.",
    {
      name: z.string().describe("Name for the new workset (e.g., 'Architecture', 'Structure', 'MEP')"),
      isOpenByDefault: z.boolean().optional().describe("Whether the workset is open by default for all users (default: true)"),
      isDefault: z.boolean().optional().describe("Set as the default workset for new elements (default: false)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_workset", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check worksharing is enabled and workset name is unique." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
