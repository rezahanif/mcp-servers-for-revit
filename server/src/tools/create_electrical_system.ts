import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateElectricalSystemTool(server: McpServer) {
  server.tool(
    "create_electrical_system",
    "Create electrical circuits/systems in Revit by assigning electrical equipment to circuits on a panel",
    {
      circuitName: z.string().optional().describe("Name for the electrical circuit"),
      voltage: z.number().optional().describe("System voltage (e.g., 120, 240, 480)"),
      panelId: z.number().describe("Element ID of the electrical panel"),
      equipmentIds: z.array(z.number()).describe("Element IDs of electrical equipment to include in the circuit"),
      loadClassification: z.string().optional().describe("Load classification name"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_electrical_system", args);
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
