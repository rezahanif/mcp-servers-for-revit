import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateFireProtectionTool(server: McpServer) {
  server.tool(
    "create_fire_protection",
    "Create fire protection piping systems in Revit (sprinkler pipes, standpipes). All coordinates in millimeters.",
    {
      data: z
        .array(
          z.object({
            startPoint: z.object({
              x: z.number().describe("Start X coordinate in mm"),
              y: z.number().describe("Start Y coordinate in mm"),
              z: z.number().describe("Start Z coordinate in mm"),
            }),
            endPoint: z.object({
              x: z.number().describe("End X coordinate in mm"),
              y: z.number().describe("End Y coordinate in mm"),
              z: z.number().describe("End Z coordinate in mm"),
            }),
            pipeTypeName: z.string().optional().describe("Pipe type name for fire protection"),
            diameter: z.number().optional().describe("Pipe diameter in mm"),
            systemType: z.enum(["sprinkler", "standpipe", "both"]).default("sprinkler").describe("Fire protection system type"),
            levelId: z.number().optional().describe("Level ElementId"),
          })
        )
        .describe("Array of fire protection pipe segments to create"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_fire_protection", args);
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
