import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerExportNavisworksTool(server: McpServer) {
  server.tool(
    "export_navisworks",
    "Export the Revit model to a Navisworks coordination file (.nwc) with preserved element IDs, linked CAD conversions, and shared coordinates for 4D/5D clash detection.",
    {
      outputFolder: z.string().optional().describe("Directory where the .nwc file should be saved (defaults to temp folder)"),
      fileName: z.string().optional().describe("Output file name (defaults to project document title)"),
      exportElementIds: z.boolean().optional().describe("Whether to embed Revit Element IDs into Navisworks properties (default: true)"),
      convertLinkedCAD: z.boolean().optional().describe("Whether to include linked CAD formats (default: true)"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("export_navisworks", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Ensure Navisworks export utility is supported or fallback to IFC/DWG." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
