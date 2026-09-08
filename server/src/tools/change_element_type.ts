import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerChangeElementTypeTool(server: McpServer) {
  server.tool(
    "change_element_type",
    "Change a Revit element's (or a batch of elements') type — e.g. switch a wall from Type A to Type B. Elements that cannot have a type assigned are reported per-element rather than failing the whole batch.",
    {
      elementId: z.number().optional().describe("Single element ID to change (optional)"),
      elementIds: z.array(z.number()).optional().describe("Element IDs to change in bulk (optional)"),
      typeId: z.number().describe("Element ID of the target type"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("change_element_type", args);
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
