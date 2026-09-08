import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerGetMepSegmentsAndSizesTool(server) {
    server.tool("get_mep_segments_and_sizes", "Inventory the project's MEP pipe segment and duct size catalogs in one read-only call. Covers what Manage -> MEP Settings -> Segments and Sizes shows: each pipe segment (material x schedule, e.g. 'Copper - K', 'PVC - Sch 40') with its size table (nominal/inner/outer diameter in mm plus Used in Size Lists / Used in Sizing flags), and optionally the duct size tables (Round/Rectangular/Oval — nominal only, since Revit's inner/outer values for ducts are placeholders and are not returned). This data is not visible in any schedule or the System Browser. A full dump can run to hundreds of rows — use summaryOnly=true for an overview first, then segmentName to drill into one segment.", {
        summaryOnly: z.boolean().optional().describe("Return only per-segment/duct-shape counts (size count, checked-flag counts) without listing sizes individually. Default false; recommended for a first full-project pass."),
        segmentName: z.string().optional().describe("Only return segments whose name contains this string (case-insensitive), e.g. 'Copper', 'PVC', 'Copper - K'. When given, includeDuct defaults to false."),
        includeDuct: z.boolean().optional().describe("Whether to also return duct size tables (Round/Rectangular/Oval). Default true, but defaults to false when segmentName is given."),
        usedOnly: z.boolean().optional().describe("Only list sizes with Used in Size Lists or Used in Sizing checked. Default false (list everything)."),
    }, async (args, extra) => {
        const params = args;
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("get_mep_segments_and_sizes", params);
            });
            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify(response, null, 2),
                    },
                ],
            };
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
