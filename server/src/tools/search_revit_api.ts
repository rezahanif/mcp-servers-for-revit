import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { RevitError } from "./errors.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface ApiNamespace {
  name: string;
  description: string;
  classes: string[];
  relevance: string;
}

interface ApiIndex {
  namespaces: ApiNamespace[];
}

let _index: ApiIndex | null = null;

function loadIndex(): ApiIndex {
  if (_index) return _index;
  const indexPath = path.join(__dirname, "revit_api_index.json");
  _index = JSON.parse(fs.readFileSync(indexPath, "utf-8"));
  return _index!;
}

export function registerSearchRevitApiTool(server: McpServer) {
  server.tool(
    "search_revit_api",
    "Search the Revit API by keyword — finds matching namespaces, classes, and descriptions. Use this to discover what API classes exist before writing send_code_to_revit code.",
    {
      query: z
        .string()
        .describe("Search keyword (e.g. 'wall', 'room', 'floor', 'transaction', 'parameter')"),
      max_results: z
        .number()
        .optional()
        .describe("Maximum results to return (default 10)"),
    },
    async (args, extra) => {
      try {
        const index = loadIndex();
        const query = args.query.toLowerCase();
        const maxResults = args.max_results || 10;

        const results: { namespace: string; classes: string[]; description: string; relevance: string }[] = [];

        for (const ns of index.namespaces) {
          const matchedClasses = ns.classes.filter(
            (c) => c.toLowerCase().includes(query)
          );
          const descMatch = ns.description.toLowerCase().includes(query);
          const nameMatch = ns.name.toLowerCase().includes(query);

          if (matchedClasses.length > 0 || descMatch || nameMatch) {
            results.push({
              namespace: ns.name,
              classes: matchedClasses.length > 0 ? matchedClasses : ns.classes.slice(0, 3),
              description: ns.description,
              relevance: ns.relevance,
            });
          }
        }

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify({
                query: args.query,
                matches: results.slice(0, maxResults),
                total: results.length,
                hint: results.length === 0
                  ? `No matches for "${args.query}". Try broader terms like "wall", "room", "parameter", "transaction".`
                  : `Found ${results.length} namespaces. Use send_code_to_revit to execute C# with these classes.`,
              }, null, 2),
            },
          ],
        };
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        return {
          content: [{ type: "text", text: JSON.stringify({ error: "search_failed", message: msg }) }],
        };
      }
    }
  );
}
