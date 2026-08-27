import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateProjectParameterTool(server) {
    server.tool("create_project_parameter", "Create a project parameter bound to specific categories. Project parameters are local to the project and not shared across files.", {
        parameterName: z.string().describe("Name for the new project parameter"),
        categoryName: z.string().describe("Category to bind the parameter to (e.g., 'Walls', 'Doors', 'Rooms')"),
        parameterType: z.string().describe("Parameter type: 'Text', 'Integer', 'Number', 'Length', 'Area', 'Volume', 'Angle', 'YesNo'"),
        groupName: z.string().optional().describe("Parameter group for UI organization (e.g., 'Identity Data', 'Dimensions')"),
        usage: z.string().describe("Usage: 'Type' or 'Instance'"),
        description: z.string().optional().describe("Description of the parameter"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_project_parameter", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check parameter name is unique and category exists." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
