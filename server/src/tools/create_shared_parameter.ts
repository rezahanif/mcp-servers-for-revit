import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateSharedParameterTool(server: McpServer) {
  server.tool(
    "create_shared_parameter",
    "Create a shared parameter definition in the project's shared parameter file. Shared parameters can be bound to multiple categories and used across projects.",
    {
      parameterName: z.string().describe("Name for the new shared parameter"),
      groupName: z.string().describe("Parameter group name (e.g., 'Identity Data', 'Dimensions', 'Text', 'Other')"),
      parameterType: z.string().describe("Parameter type: 'Text', 'Integer', 'Number', 'Length', 'Area', 'Volume', 'Angle', 'Slope', 'Currency', 'YesNo', 'MultiLineText'"),
      description: z.string().optional().describe("Description of the parameter"),
      visible: z.boolean().optional().describe("Parameter is visible in the UI (default: true)"),
      userModifiable: z.boolean().optional().describe("Parameter can be modified by users (default: true)"),
      usage: z.string().optional().describe("Usage: 'Type' or 'Instance' (default: 'Instance')"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_shared_parameter", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check parameter name is unique and shared parameter file path is set." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
