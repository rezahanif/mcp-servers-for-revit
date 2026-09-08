import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { RevitError, ConnectionError } from "./errors.js";
export function registerSetProjectUnitsTool(server) {
    server.tool("set_project_units", "Switch the whole project's display units to a preset system/mode in one action (project-wide, single Transaction, Ctrl+Z reversible). mode='taiwan' = metric base + Air Flow m3/h + Length unit symbol shown. mode='taiwan-plumbing' = taiwan plus 6 plumbing-side fields (pipe size mm, flow L/min, velocity m/s, pressure mH2O, friction mmH2O/m, slope 1:ratio). 'metric'/'imperial' are plain presets. Each spec is set together with precision and unit symbol. Individual fields (length/area/volume/airFlow/pipeSize/flow/velocity/pressure/friction/slope) can override the preset. Note: Taiwan code pressure unit kgf/cm2 does not exist in Revit, so mH2O is used instead (1 kgf/cm2 = 10.0 mH2O). The returned Result is read back from the Document after applying, not an echo of the input.", {
        mode: z.string().optional().describe("Preset mode: 'taiwan', 'taiwan-plumbing', 'metric', 'imperial'. Mutually exclusive with system."),
        system: z.string().optional().describe("Base unit system: 'metric' or 'imperial' (used when mode is not given; default metric)."),
        length: z.string().optional().describe("Length unit override: m / mm / cm / ft / ft-in"),
        area: z.string().optional().describe("Area unit override: m2 / sf"),
        volume: z.string().optional().describe("Volume unit override: m3 / l / cf"),
        airFlow: z.string().optional().describe("Air flow unit override: m3/h / l/s / cfm"),
        pipeSize: z.string().optional().describe("Pipe size unit override: mm / cm / m / in"),
        flow: z.string().optional().describe("Pipe flow unit override: l/min / l/s / m3/h / gpm"),
        velocity: z.string().optional().describe("Pipe velocity unit override: m/s / fps"),
        pressure: z.string().optional().describe("Pipe pressure unit override: mh2o / mmh2o / kpa / pa / bar / kgf/m2"),
        friction: z.string().optional().describe("Pipe friction loss unit override: mmh2o/m / mh2o/m / pa/m"),
        slope: z.string().optional().describe("Pipe slope unit override: 1:ratio / ratio:1 / % / deg"),
    }, async (args, _extra) => {
        try {
            const response = await withRevitConnection(async (revitClient) => {
                return await revitClient.sendCommand("set_project_units", args);
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
