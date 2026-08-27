import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetCircuitLoadTool(server) {
    server.tool("get_circuit_load", "Calculate and retrieve electrical circuit load information from panels and circuits in Revit", {
        panelId: z.number().optional().describe("Panel ElementId to query (returns all panels if omitted)"),
        circuitId: z.number().optional().describe("Specific circuit ElementId to query"),
        includeBreakerInfo: z.boolean().default(false).describe("Include breaker slot information"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_circuit_load", args);
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
