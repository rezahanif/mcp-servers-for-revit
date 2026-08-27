import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerPlaceFamilyInstanceTool(server: McpServer) {
  server.tool(
    "place_family_instance",
    "Place instances of a loaded family at specified locations. Supports host-based and free-standing families.",
    {
      data: z.array(
        z.object({
          familyName: z.string().describe("Family name (e.g., 'Basic Wall', 'Door')"),
          typeName: z.string().describe("Family type name"),
          location: z.object({
            x: z.number().describe("X coordinate in mm"),
            y: z.number().describe("Y coordinate in mm"),
            z: z.number().describe("Z coordinate in mm"),
          }).describe("Placement location"),
          levelId: z.number().optional().describe("Level ElementId"),
          hostId: z.number().optional().describe("Host element ElementId (wall, floor, etc.)"),
          rotation: z.number().optional().describe("Rotation angle in degrees"),
          flipFacing: z.boolean().optional().describe("Flip the facing direction"),
          flipHand: z.boolean().optional().describe("Flip the hand direction"),
        })
      ).describe("Array of family instances to place"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("place_family_instance", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check family is loaded and location is valid." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
