import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSetStructuralMaterialTool(server) {
    server.tool("set_structural_material", "Assign or change structural materials on structural elements (concrete, steel, wood, etc.)", {
        data: z
            .array(z.object({
            elementId: z.number().describe("Element ID of the structural element"),
            materialName: z.string().describe("Material name (e.g., 'Concrete 28 MPa', 'A992 Steel')"),
            structuralAssetName: z.string().optional().describe("Structural asset/behavior name"),
        }))
            .describe("Array of material assignments"),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("set_structural_material", args);
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
