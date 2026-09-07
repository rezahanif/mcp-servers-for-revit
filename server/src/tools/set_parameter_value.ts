import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";

export function registerSetParameterValueTool(server: McpServer) {
  server.tool(
    "set_parameter_value",
    "Domain: any category — property writes. Set parameter values on existing Revit elements, for instance or type parameters, with unit conversion. Use this to correct or update a property after an element already exists. Writes are reported per parameter, so one element in the batch can partially succeed. To confirm a write landed, read the element back with ai_element_filter — there is no get_parameter_value on this connector's tool surface. Not every category exposes every property, so a name that fails here may simply not exist on that element rather than being mistyped.",
    {
      data: z.array(
        z.object({
          elementId: z.number().describe("ElementId of the target element"),
          parameters: z.array(
            z.object({
              name: z.string().describe("Parameter name, exactly as Revit spells it for this category. A name that does not exist on the element is reported as that element's failure, not as a call-level error — check `message` rather than assuming the write happened. Walls in particular do not expose a writable 'Level'; to move a wall between levels, recreate it with the levelId you want."),
              value: z.string().describe("Value to set (string representation — Revit handles type conversion)"),
              units: z.string().optional().describe("Unit of the provided value (e.g., 'mm', 'm', 'ft') — Revit converts to internal units"),
            })
          ).describe("Parameters to set on this element"),
        })
      ).describe("Array of elements and their parameter updates"),
    },
    async (args, extra) => {
      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("set_parameter_value", args);
        });
        return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
      } catch (error) {
        if (error instanceof RevitError) {
          return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
        }
        const msg = error instanceof Error ? error.message : String(error);
        const e = msg.includes("connection") || msg.includes("refused")
          ? new ConnectionError(msg)
          : new RevitError(msg, { error_code: "tool_error", hint: "Check element exists and parameter is writable (not read-only)." });
        return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
      }
    }
  );
}
