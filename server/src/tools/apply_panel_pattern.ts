import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerApplyPanelPatternTool(server: McpServer) {
  server.tool(
    "apply_panel_pattern",
    "Apply a panel arrangement pattern to a curtain wall. Requires a type mapping table and an arrangement matrix.",
    {
      elementId: z.number().describe("Element ID of the curtain wall"),
      typeMapping: z
        .record(z.string(), z.number())
        .describe("Type mapping table (letter -> panel type ElementId)"),
      matrix: z
        .array(z.array(z.string()))
        .describe("Panel arrangement matrix, top-to-bottom, left-to-right, cells are letters from typeMapping"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("apply_panel_pattern", args);
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
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
