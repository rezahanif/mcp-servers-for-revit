import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerAnalyzeLoadCombinationsTool(server: McpServer) {
  server.tool(
    "analyze_load_combinations",
    "Analyze and retrieve load combinations defined in the Revit structural model",
    {
      loadCaseNames: z.array(z.string()).optional().describe("Filter by specific load case names"),
      combinationNames: z.array(z.string()).optional().describe("Filter by specific combination names"),
      includeFactors: z.boolean().default(true).describe("Include load factors in results"),
      includeCases: z.boolean().default(true).describe("Include individual load cases"),
      includeCombinations: z.boolean().default(true).describe("Include load combinations"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("analyze_load_combinations", args);
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
