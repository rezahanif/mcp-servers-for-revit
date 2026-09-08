import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateDetailComponentTypeTool(server: McpServer) {
  server.tool(
    "create_detail_component_type",
    "Duplicate a detail item family type and auto-fill sheet/detail number type parameters (detail number, sheet name, detail name, detail number), naming the new type '<sheetNumber>-<sheetName>-<detailName>'.",
    {
      sheetNumber: z.string().describe("Target sheet number (e.g. 'A101')"),
      detailName: z.string().describe("Detail name (new detail name to assign)"),
      familyName: z
        .string()
        .optional()
        .describe("Base detail item family name to duplicate (optional; defaults to a family containing 'AE-圖號' if omitted)"),
      detailNumber: z
        .string()
        .optional()
        .describe("Detail number (optional, defaults to '1')"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_detail_component_type", args);
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
