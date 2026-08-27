import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerTagMepElementsTool(server) {
    server.tool("tag_mep_elements", "Tag MEP elements in a view with size, system type, or other parameters using IndependentTag", {
        viewId: z.number().optional().describe("View ElementId (uses active view if omitted)"),
        category: z.enum(["ducts", "pipes", "all"]).describe("MEP category to tag"),
        tagTypeName: z.string().optional().describe("Tag family type name"),
        parameterFieldName: z.string().optional().describe("Parameter to display in tag (default: size)"),
        systemTypeName: z.string().optional().describe("Filter by system type"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("tag_mep_elements", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check Revit is running and parameters are valid." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
