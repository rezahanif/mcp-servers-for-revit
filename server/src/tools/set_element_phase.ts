import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerSetElementPhaseTool(server: McpServer) {
  server.tool(
    "set_element_phase",
    "Assign phase properties to elements. Set the created phase, demolished phase, and phase status of elements.",
    {
      data: z.array(
        z.object({
          elementId: z.number().describe("ElementId of the target element"),
          createdPhase: z.string().optional().describe("Name of the phase when this element was created"),
          demolishedPhase: z.string().optional().describe("Name of the phase when this element was demolished (use 'None' to clear)"),
        })
      ).describe("Array of elements and their phase assignments"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_phase", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check element IDs exist and phase names are valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
