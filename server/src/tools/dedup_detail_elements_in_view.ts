import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerDedupDetailElementsInViewTool(server: McpServer) {
  server.tool(
    "dedup_detail_elements_in_view",
    "Find duplicate detail elements (same type + position after quantization) in the current or a specified view, covering DetailComponent/DetailCurve/FilledRegion/TextNote/Dimension. When duplicates span a Detail Group boundary, the in-group copy is kept and the out-of-group copy is deleted. Boundary cases (all copies in a group, or all copies outside any group) are reported but never touched. Only Detail Groups (OST_IOSDetailGroups) are recognized, not Model Groups. Defaults to dryRun=true (list only); set dryRun=false to actually delete.",
    {
      viewId: z.number().optional().describe("Target view ElementId (optional, defaults to the active view)"),
      categories: z
        .array(z.enum(["All", "DetailComponent", "DetailCurve", "FilledRegion", "TextNote", "Dimension"]))
        .optional()
        .describe("Categories to process (optional, defaults to ['All'])"),
      tolerance: z
        .number()
        .optional()
        .default(1.0)
        .describe("Position-matching tolerance in millimeters, default 1.0"),
      dryRun: z
        .boolean()
        .optional()
        .default(true)
        .describe("true = list only, no deletion (default); false = actually delete out-of-group duplicates"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("dedup_detail_elements_in_view", args);
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
