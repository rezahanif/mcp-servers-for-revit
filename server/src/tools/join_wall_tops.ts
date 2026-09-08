import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerJoinWallTopsTool(server: McpServer) {
  server.tool(
    "join_wall_tops",
    "Joins the tops of walls on the given levels to the floors, ceilings, and structural framing above them (JoinGeometry). Returns joined / already-joined-skipped / failed counts plus a per-level breakdown. Commonly used as a batch step right after modeling a level's walls.",
    {
      levels: z.array(z.string()).optional().describe("Level names to process, e.g. [\"2F\", \"3F\"]. Omitted or empty means process every level."),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("join_wall_tops", args);
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
