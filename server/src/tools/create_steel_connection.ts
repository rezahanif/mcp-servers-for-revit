import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateSteelConnectionTool(server: McpServer) {
  server.tool(
    "create_steel_connection",
    "Create steel connections between structural members (beam-column joints, splices) using Revit Steel API",
    {
      data: z
        .array(
          z.object({
            primaryElementId: z.number().describe("Element ID of the primary member (e.g., column)"),
            secondaryElementId: z.number().describe("Element ID of the secondary member (e.g., beam)"),
            connectionTypeName: z.string().optional().describe("Steel connection family type name"),
            connectionPoint: z.object({
              x: z.number().describe("Connection point X in mm"),
              y: z.number().describe("Connection point Y in mm"),
              z: z.number().describe("Connection point Z in mm"),
            }).optional().describe("Explicit connection point location"),
          })
        )
        .describe("Array of steel connections to create"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_steel_connection", args);
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
