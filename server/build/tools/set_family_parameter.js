import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSetFamilyParameterTool(server) {
    server.tool("set_family_parameter", "Set parameter values on a family instance or family type. Works with both instance and type parameters.", {
        elementId: z.number().optional().describe("ElementId of the family instance to modify"),
        familyName: z.string().optional().describe("Family name (for type-level changes)"),
        typeName: z.string().optional().describe("Type name (for type-level changes)"),
        parameters: z.array(z.object({
            name: z.string().describe("Parameter name"),
            value: z.string().describe("New parameter value"),
            parameterType: z.string().optional().describe("Parameter type hint: 'Text', 'Number', 'Length', 'Area', 'Volume'"),
        })).describe("Parameters to set"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("set_family_parameter", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check element exists and parameter is writable." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
