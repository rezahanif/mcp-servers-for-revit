import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreatePipeSystemTool(server: McpServer) {
  server.tool(
    "create_pipe_system",
    "Create piping systems in Revit by assigning pipes to plumbing/piping system types (Domestic Hot Water, Sanitary, etc.)",
    {
      systemName: z.string().describe("Name for the pipe system"),
      systemType: z.enum(["domestic_hot_water", "domestic_cold_water", "sanitary", "storm", "hydronic", "other"]).describe("Piping system classification"),
      pipeElementIds: z.array(z.number()).describe("Element IDs of pipes to assign to this system"),
      baseEquipmentId: z.number().optional().describe("Element ID of the base equipment for the system"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_pipe_system", args);
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
