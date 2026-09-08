import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCheckSmokeExhaustWindowsTool(server: McpServer) {
  server.tool(
    "check_smoke_exhaust_windows",
    "Smoke-exhaust window compliance check: verifies that openable window area within 800mm below the ceiling is >= 2% of the room's floor area, per building code smoke-exhaust requirements. Also flags windowless rooms. Rooms with a name matching a non-residential keyword (corridor, stair, elevator, shaft, mechanical, toilet, bath, vestibule, balcony) or with floor area <= 50 sq m are skipped. Optionally colorizes windows in the active view: green = fully effective, yellow = reduced-ratio (sliding/projected/louver), red = fixed (ineffective).",
    {
      levelName: z.string().describe("Level name to check"),
      ceilingHeightSource: z
        .enum(["room_parameter", "ceiling_element"])
        .optional()
        .describe("Where to read ceiling height from: the Room's Upper Limit + Limit Offset parameters, or a Ceiling element's height. Default room_parameter"),
      colorize: z.boolean().optional().describe("Whether to colorize checked windows in the active view. Default true"),
      smokeZoneHeight: z.number().optional().describe("Effective smoke-exhaust band height below the ceiling, in mm. Default 800"),
      excludeKeywords: z
        .array(z.string())
        .optional()
        .describe("Room-name keywords to treat as non-residential (skipped). Defaults to a built-in list (corridor, stair, elevator, shaft, mechanical, toilet, bath, vestibule, balcony, in English and Chinese)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("check_smoke_exhaust_windows", args);
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
