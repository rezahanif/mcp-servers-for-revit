import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerLoadSharedParametersTool(server: McpServer) {
  server.tool(
    "load_shared_parameters",
    "Load a shared parameter file and bind the specified parameters to the given categories, at Type or Instance level. Use before writing custom parameter values, to make sure the project has the parameter definitions loaded. 'Columns' binds both OST_Columns and OST_StructuralColumns. dryRun=true reports what would be bound/expanded/skipped without changing the document.",
    {
      filePath: z.string().describe("Absolute path to the shared parameter file (e.g. C:/path/to/SharedParams.txt)"),
      categories: z.array(z.string()).describe("Category names to bind to: Walls, Floors, Ceilings, Windows, Doors, Materials, Roofs, CurtainPanels, Columns (binds both OST_Columns and OST_StructuralColumns), StructuralFraming"),
      bindToInstance: z.boolean().optional().default(false).describe("true = bind to Instance parameters, false = bind to Type parameters (default false)"),
      groupFilter: z.array(z.number()).optional().describe("Only load parameters from these Group IDs (optional)"),
      dryRun: z.boolean().optional().describe("true = only report which parameters would be newly bound/expanded/idempotently skipped, without calling any BindingMap change. Default false = execute normally."),
    },
    async (args, _extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("load_shared_parameters", args);
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
