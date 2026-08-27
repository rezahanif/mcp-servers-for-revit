import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerBindParameterTool(server) {
    server.tool("bind_parameter", "Bind a shared or project parameter to one or more categories. Controls which element categories display the parameter.", {
        parameterName: z.string().describe("Name of the parameter to bind"),
        parameterType: z.string().describe("Parameter type: 'Text', 'Integer', 'Number', 'Length', 'Area', 'Volume', 'Angle', 'YesNo'"),
        categoryNames: z.array(z.string()).describe("Category names to bind to (e.g., ['Walls', 'Doors', 'Windows'])"),
        usage: z.string().describe("Binding usage: 'Type' or 'Instance'"),
        groupName: z.string().optional().describe("Parameter group for UI placement (e.g., 'Identity Data')"),
        description: z.string().optional().describe("Parameter description"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("bind_parameter", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check parameter and categories exist." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
