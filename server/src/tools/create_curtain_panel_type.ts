import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateCurtainPanelTypeTool(server: McpServer) {
  server.tool(
    "create_curtain_panel_type",
    "Create a new curtain wall panel type, with an optional color (hex) and transparency.",
    {
      typeName: z.string().describe("Name of the new panel type"),
      color: z.string().describe("Panel color in HEX format (e.g. '#5C4033')"),
      baseTypeId: z
        .number()
        .optional()
        .describe("ElementId of an existing curtain panel type to duplicate as the base (optional — defaults to the first available panel type)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_curtain_panel_type", args);
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
