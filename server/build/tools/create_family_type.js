import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateFamilyTypeTool(server) {
    server.tool("create_family_type", "Create a new family type within a loaded family. Duplicate an existing type and optionally set parameter values.", {
        familyName: z.string().describe("Name of the family to create a type in"),
        newTypeName: z.string().describe("Name for the new family type"),
        duplicateFrom: z.string().optional().describe("Existing type to duplicate from (defaults to first available type)"),
        parameters: z.record(z.string(), z.string()).optional().describe("Parameter name-value pairs for the new type (e.g., {'Width': '1000', 'Height': '2000'})"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_family_type", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check family is loaded and type name is unique." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
