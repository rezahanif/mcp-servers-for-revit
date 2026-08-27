import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetWorksetsTool(server: McpServer) {
  server.tool(
    "get_worksets",
    "List all worksets in the Revit project. Returns workset names, IDs, owner, open status, and element counts.",
    {
      includeElementCount: z.boolean().optional().describe("Include count of elements in each workset (default: false, can be slow on large models)"),
      includeOwner: z.boolean().optional().describe("Include workset owner information (default: true)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_worksets", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check worksharing is enabled in the project." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
