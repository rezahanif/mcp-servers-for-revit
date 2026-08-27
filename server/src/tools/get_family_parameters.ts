import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetFamilyParametersTool(server: McpServer) {
  server.tool(
    "get_family_parameters",
    "List all parameters available for a family type, including built-in and shared parameters with their types and values.",
    {
      familyName: z.string().describe("Family name to query"),
      typeName: z.string().optional().describe("Specific type name (if omitted, returns parameters of the first type)"),
      includeInstanceParams: z.boolean().optional().describe("Include instance parameters (default: true)"),
      includeTypeParams: z.boolean().optional().describe("Include type parameters (default: true)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_family_parameters", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check family name is correct and family is loaded." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
