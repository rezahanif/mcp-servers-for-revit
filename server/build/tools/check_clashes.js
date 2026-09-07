import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCheckClashesTool(server) {
    server.tool("check_clashes", "Perform geometric interference and clash detection between elements or categories (e.g., Walls vs Walls, MEP vs Structure). Returns colliding pairs with element IDs, clash types, and resolution advice.", {
        categoryA: z.string().optional().describe("Primary category to check (e.g., 'OST_Walls', 'OST_DuctCurves')"),
        categoryB: z.string().optional().describe("Secondary category to check against. If omitted, checks for self-intersections within categoryA."),
        elementIdsA: z.array(z.number()).optional().describe("Specific element IDs in set A to test"),
        elementIdsB: z.array(z.number()).optional().describe("Specific element IDs in set B to test"),
        limit: z.number().optional().describe("Maximum number of clash results to return (default: 50)"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("check_clashes", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Check element IDs and categories exist in document." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
