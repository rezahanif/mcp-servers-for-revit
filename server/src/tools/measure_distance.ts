import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerMeasureDistanceTool(server: McpServer) {
  server.tool(
    "measure_distance",
    "Measure the distance between two points. Returns the distance in millimeters (mm).",
    {
      point1X: z.number().describe("First point X coordinate (mm)"),
      point1Y: z.number().describe("First point Y coordinate (mm)"),
      point1Z: z.number().optional().default(0).describe("First point Z coordinate (mm), default 0"),
      point2X: z.number().describe("Second point X coordinate (mm)"),
      point2Y: z.number().describe("Second point Y coordinate (mm)"),
      point2Z: z.number().optional().default(0).describe("Second point Z coordinate (mm), default 0"),
    },
    async (args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("measure_distance", args);
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
