import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerModifyFamilyTypeTool(server: McpServer) {
  server.tool(
    "modify_family_type",
    "Edit parameter values of an existing family type. Updates the type properties for all instances of that type.",
    {
      familyName: z.string().describe("Name of the family"),
      typeName: z.string().describe("Name of the family type to modify"),
      parameters: z.record(z.string(), z.string()).describe("Parameter name-value pairs to update (e.g., {'Width': '1200', 'Cost': '500'})"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("modify_family_type", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check family type exists and parameter names are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
