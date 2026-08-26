import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerGetMepQuantitiesTool(server: McpServer) {
  server.tool(
    "get_mep_quantities",
    "Extract MEP material quantities and takeoffs (duct lengths, pipe runs, fittings counts, insulation areas)",
    {
      category: z.enum(["ducts", "pipes", "fittings", "insulation", "all"]).describe("MEP category to quantify"),
      systemTypeName: z.string().optional().describe("Filter by system type name"),
      levelName: z.string().optional().describe("Filter by level name"),
      unitSystem: z.enum(["metric", "imperial"]).default("metric").describe("Unit system for results"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_mep_quantities", args);
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
