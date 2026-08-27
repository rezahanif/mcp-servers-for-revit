import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetStructuralQuantitiesTool(server) {
    server.tool("get_structural_quantities", "Extract structural member quantities (lengths, volumes, weights, material takeoffs) for structural elements", {
        category: z.enum(["columns", "beams", "braces", "walls", "foundations", "rebar", "all"]).describe("Structural category to quantify"),
        levelName: z.string().optional().describe("Filter by level name"),
        materialFilter: z.string().optional().describe("Filter by material name"),
        unitSystem: z.enum(["metric", "imperial"]).default("metric").describe("Unit system for results"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_structural_quantities", args);
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
