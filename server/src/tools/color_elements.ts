import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerColorElementsTool(server: McpServer) {
  server.tool(
      "color_elements",
      "Color elements in the current view based on a category and parameter value. Each unique parameter value gets assigned a distinct color.",
      {
        categoryName: z
            .string()
            .describe("The name of the Revit category to color (e.g., 'Walls', 'Doors', 'Rooms')"),
        parameterName: z
            .string()
            .describe("The name of the parameter to use for grouping and coloring elements"),
        useGradient: z
            .boolean()
            .optional()
            .default(false)
            .describe("Whether to use a gradient color scheme instead of random colors"),
        customColors: z
            .array(
                z.object({
                  r: z.number().int().min(0).max(255),
                  g: z.number().int().min(0).max(255),
                  b: z.number().int().min(0).max(255),
                })
            )
            .optional()
            .describe("Optional array of custom RGB colors to use for specific parameter values"),
      },
      async (args, extra) => {
        const params = args;
        try {
          const response = await withRevitConnection(async (revitClient) => {
            return await revitClient.sendCommand("color_splash", params);
          });

          // Format the response into a more user-friendly output
          if (response.success) {
            const coloredGroups = response.results || [];

            let resultText = `Successfully colored ${response.totalElements} elements across ${response.coloredGroups} groups.\n\n`;
            resultText += "Parameter Value Groups:\n";

            coloredGroups.forEach((group: any) => {
              const rgb = group.color;
              resultText += `- "${group.parameterValue}": ${group.count} elements colored with RGB(${rgb.r}, ${rgb.g}, ${rgb.b})\n`;
            });

            return {
              content: [
                {
                  type: "text",
                  text: resultText,
                },
              ],
            };
          } else {
            return {
              content: [
                {
                  type: "text",
                  text: `Color operation failed: ${response.message}`,
                },
              ],
            };
          }
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