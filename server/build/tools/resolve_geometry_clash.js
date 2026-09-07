import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerResolveGeometryClashTool(server) {
    server.tool("resolve_geometry_clash", "Resolve geometric overlap and collision between two elements using Revit's Cut Geometry or Join Geometry APIs. Resolves warnings like 'Highlighted walls overlap. Use Cut Geometry to embed one wall within the other.'", {
        elementIdA: z.number().describe("First element ElementId (e.g., cutting wall or host)"),
        elementIdB: z.number().describe("Second element ElementId (e.g., target wall or embedded element)"),
        action: z.enum(["cut", "uncut", "join", "unjoin", "switch_join_order"]).describe("Geometric resolution action: 'cut' (embeds element using SolidSolidCutUtils), 'join' (joins geometry), etc."),
    }, async (args, extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("resolve_geometry_clash", args);
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
                : new RevitError(msg, { error_code: "tool_error", hint: "Verify both elements exist and are solids that support cut/join." });
            return { content: [{ type: "text", text: JSON.stringify(e.toPayload(), null, 2) }] };
        }
    });
}
