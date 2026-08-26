import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateHvacZoneTool(server: McpServer) {
  server.tool(
    "create_hvac_zone",
    "Create HVAC zones in Revit by grouping spaces into thermal/ventilation zones with defined boundaries",
    {
      zoneName: z.string().describe("Name for the HVAC zone"),
      spaceIds: z.array(z.number()).describe("Element IDs of Space elements to include in the zone"),
      systemType: z.enum(["supply_air", "return_air", "exhaust_air", "other_air"]).describe("Zone system type"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_hvac_zone", args);
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
