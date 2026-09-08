import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerAnalyzeFloorSlopesTool(server) {
    server.tool("analyze_floor_slopes", "Analyze floor top-surface drainage slope: computes the angle between each upward-facing PlanarFace's normal and the Z axis to get a slope percentage, returns Min/Max slope per floor, and can write the result back to a parameter (default Comments). If elementIds is omitted, automatically collects all floors with Function=Exterior.", {
        elementIds: z.array(z.number()).optional().describe("Floor Element IDs to analyze; omit to auto-collect all Function=Exterior floors"),
        paramName: z.string().optional().describe("Target parameter name to write the slope back to, default Comments"),
    }, async (args, _extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("analyze_floor_slopes", args);
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
