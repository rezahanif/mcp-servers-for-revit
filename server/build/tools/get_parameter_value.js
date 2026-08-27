import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetParameterValueTool(server) {
    server.tool("get_parameter_value", "Read parameter values from one or more Revit elements. Returns parameter names, values, storage types, and units.", {
        elementIds: z.array(z.number()).describe("ElementIds to read parameters from"),
        parameterNames: z.array(z.string()).optional().describe("Specific parameter names to read (omit for all parameters)"),
        includeBuiltIn: z.boolean().optional().describe("Include built-in parameters (default: false)"),
        formattedValues: z.boolean().optional().describe("Return display-formatted values with units (default: true)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_parameter_value", args);
            });
            return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
        }
        catch (error) {
            if (error instanceof RevitError) {
                return { content: [{ type: "text", text: JSON.stringify(error.toPayload(), null, 2) }] };
            }
            const msg = error instanceof Error ? error.message : String(error);
            const e = msg.includes("connection") || msg.includes("refused")
                ? new ConnectionError(msg)
                : new RevitError(msg, { error_code: "tool_error", hint: "Check element IDs exist and parameters are readable." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
