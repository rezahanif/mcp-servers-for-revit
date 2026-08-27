import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerSetPhaseVisibilityTool(server: McpServer) {
  server.tool(
    "set_phase_visibility",
    "Control phase visibility in views. Apply a phase filter to a view or set phase display options (show new, show existing, show demolished, etc.).",
    {
      viewId: z.number().describe("ElementId of the target view"),
      phaseFilterName: z.string().describe("Name of the phase filter to apply (e.g., 'Show All', 'Show Complete', 'Show New', 'Show Existing')"),
      phaseName: z.string().optional().describe("Name of the phase to set visibility for (when adjusting per-phase display)"),
      showNew: z.boolean().optional().describe("Show new construction elements"),
      showExisting: z.boolean().optional().describe("Show existing elements"),
      showDemolished: z.boolean().optional().describe("Show demolished elements"),
      showTemporary: z.boolean().optional().describe("Show temporary elements"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_phase_visibility", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check view ID exists and phase filter name is valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
