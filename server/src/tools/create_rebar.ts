import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateRebarTool(server: McpServer) {
  server.tool(
    "create_rebar",
    "Place reinforcement bars (rebar) on structural elements in Revit",
    {
      data: z
        .array(
          z.object({
            hostElementId: z.number().describe("Element ID of the host structural element (beam, column, wall)"),
            rebarBarTypeName: z.string().optional().describe("Rebar bar type name (e.g., '#5 (16mm)')"),
            count: z.number().int().positive().optional().describe("Number of bars to place"),
            spacing: z.number().positive().optional().describe("Spacing between bars in mm"),
            coverOffset: z.number().optional().describe("Cover distance from face in mm"),
            distributionType: z.enum(["single_bar", "bar_set"]).default("bar_set").describe("Distribution type"),
          })
        )
        .describe("Array of rebar sets to create"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_rebar", args);
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
