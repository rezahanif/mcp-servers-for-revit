import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerCreateDuctSystemTool(server) {
    server.tool("create_duct_system", "Create HVAC duct systems in Revit by assigning ducts to mechanical system types (Supply Air, Return Air, Exhaust Air, etc.)", {
        systemName: z.string().describe("Name for the duct system"),
        systemType: z.enum(["supply_air", "return_air", "exhaust_air", "other_air"]).describe("HVAC system classification"),
        ductElementIds: z.array(z.number()).describe("Element IDs of ducts to assign to this system"),
        baseEquipmentId: z.number().optional().describe("Element ID of the base/mechanical equipment for the system"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("create_duct_system", args);
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
