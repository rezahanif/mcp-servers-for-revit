import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerCreateFacadePanelTool(server: McpServer) {
  server.tool(
    "create_facade_panel",
    "Create a single facade panel (DirectShape). Supports 5 geometry types: curved_panel, beveled_opening, angled_panel, rounded_opening, flat_panel.",
    {
      wallId: z.number().optional().describe("Element ID of the reference wall (optional — falls back to current selection)"),
      geometryType: z
        .enum(["curved_panel", "beveled_opening", "angled_panel", "rounded_opening", "flat_panel"])
        .default("curved_panel")
        .describe("Geometry type"),
      positionAlongWall: z.number().default(0).describe("Position along the wall (mm)"),
      positionZ: z.number().default(0).describe("Bottom Z elevation (mm)"),
      width: z.number().default(800).describe("Width (mm), default 800"),
      height: z.number().default(3400).describe("Height (mm), default 3400"),
      depth: z.number().default(150).describe("Arc depth / recess depth (mm), default 150"),
      thickness: z.number().default(30).describe("Panel thickness (mm), default 30"),
      offset: z.number().default(200).describe("Offset from wall face (mm), default 200"),
      color: z.string().optional().describe("Color (HEX)"),
      name: z.string().optional().describe("Panel name"),
      curveType: z.enum(["concave", "convex"]).optional().describe("[curved_panel] curve type"),
      tiltAngle: z.number().optional().describe("[angled_panel] tilt angle (degrees)"),
      tiltAxis: z.enum(["horizontal", "vertical"]).optional().describe("[angled_panel] tilt axis"),
      bevelDirection: z.enum(["center", "up", "down", "left", "right"]).optional().describe("[beveled_opening] bevel direction"),
      bevelDepth: z.number().optional().describe("[beveled_opening] bevel depth (mm)"),
      openingWidth: z.number().optional().describe("[opening] opening width (mm)"),
      openingHeight: z.number().optional().describe("[opening] opening height (mm)"),
      cornerRadius: z.number().optional().describe("[rounded_opening] corner radius (mm)"),
      openingShape: z.enum(["rounded_rect", "arch", "stadium", "rect"]).optional().describe("[rounded_opening] opening shape"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("create_facade_panel", args);
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
