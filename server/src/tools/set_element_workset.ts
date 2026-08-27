import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerSetElementWorksetTool(server: McpServer) {
  server.tool(
    "set_element_workset",
    "Assign elements to a workset. Control which workset owns each element for worksharing collaboration.",
    {
      data: z.array(
        z.object({
          elementId: z.number().describe("ElementId of the target element"),
          worksetName: z.string().describe("Name of the workset to assign the element to"),
        })
      ).describe("Array of elements and their workset assignments"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_element_workset", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check element IDs exist and workset name is valid. Workset must be owned/Editable." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
