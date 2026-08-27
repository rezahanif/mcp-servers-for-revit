import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetFamilyTypesTool(server) {
    server.tool("get_family_types", "List all loaded family types in the project, optionally filtered by family name or category.", {
        familyName: z.string().optional().describe("Filter by specific family name (e.g., 'Basic Wall')"),
        categoryName: z.string().optional().describe("Filter by category (e.g., 'Walls', 'Doors', 'Windows')"),
        includeParameters: z.boolean().optional().describe("Include type parameters in output (default: false)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_family_types", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running with a loaded project." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
