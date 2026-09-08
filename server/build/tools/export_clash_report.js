import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerExportClashReportTool(server) {
    server.tool("export_clash_report", "Export the result of check_clashes as a report file. format is csv, json, or both. Defaults to the user's Desktop with a timestamped filename when outputPath is omitted.", {
        clashData: z
            .object({})
            .passthrough()
            .describe("The full response object returned by check_clashes (contains a Response array of clash pairs)"),
        format: z.enum(["csv", "json", "both"]).optional().default("csv").describe("Output format"),
        outputPath: z
            .string()
            .optional()
            .describe("Custom output path without extension; defaults to a timestamped file on the Desktop"),
        reportTitle: z.string().optional().default("Clash Detection Report").describe("Title embedded in the report"),
    }, async (args, _extra) => {
        const params = args;
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("export_clash_report", params);
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
