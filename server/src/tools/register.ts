import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { ensureLicensed, envelope } from "../aioconnect.js";

export async function registerTools(server: McpServer) {
  // AiConnect: startup license gate — the server refuses to register any
  // tool (and therefore to serve) without a valid MCP_LICENSE_TOKEN.
  const license = await ensureLicensed();

  // AiConnect: wrap EVERY tool's handler — per-call license recheck + response
  // envelope. Generic monkey-patch of server.tool, so the 24 tool files need
  // zero edits (Phase 5 contract: structured envelope everywhere).
  const origTool = (server as any).tool.bind(server);
  (server as any).tool = (name: string, desc: string, schema: any, handler?: any) => {
    const cb = handler ?? schema;
    const wrapped = async (args: any, extra: any) => {
      license.ensureLicensed(); // per-call recheck (cheap HS256)
      const result = await cb(args, extra);
      if (result && Array.isArray(result.content)) {
        result.content = await Promise.all(
          result.content.map(async (c: any) =>
            c.type === "text" ? { ...c, text: await envelope(c.text) } : c
          )
        );
      }
      return result;
    };
    if (handler) return origTool(name, desc, schema, wrapped);
    return origTool(name, schema, wrapped);
  };

  // All expected tool files — keep in sync with actual .ts files in this directory.
  // Excludes: errors.ts (typed errors), register.ts (this file), index.ts (barrel).
  const EXPECTED_TOOLS = [
    "ai_element_filter.ts",
    "add_schedule_field.ts",
    "analyze_load_combinations.ts",
    "analyze_model_statistics.ts",
    "apply_view_template.ts",
    "bind_parameter.ts",
    "color_elements.ts",
    "color_mep_by_system.ts",
    "connect_mep_to_space.ts",
    "create_dimensions.ts",
    "create_duct.ts",
    "create_duct_system.ts",
    "create_electrical_equipment.ts",
    "create_electrical_panel.ts",
    "create_electrical_system.ts",
    "create_family_type.ts",
    "create_fire_protection.ts",
    "create_grid.ts",
    "create_hvac_zone.ts",
    "create_level.ts",
    "create_line_based_element.ts",
    "create_phase.ts",
    "create_pipe.ts",
    "create_pipe_system.ts",
    "create_plumbing_fixture.ts",
    "create_point_based_element.ts",
    "create_project_parameter.ts",
    "create_rebar.ts",
    "create_room.ts",
    "create_schedule.ts",
    "create_shared_parameter.ts",
    "create_structural_column.ts",
    "create_structural_foundation.ts",
    "create_structural_framing_system.ts",
    "create_structural_wall.ts",
    "create_steel_connection.ts",
    "create_surface_based_element.ts",
    "create_view_template.ts",
    "create_wire.ts",
    "create_workset.ts",
    "delete_element.ts",
    "delete_view_template.ts",
    "export_dwg.ts",
    "export_dfx.ts",
    "export_ifc.ts",
    "export_mep_schedules.ts",
    "export_room_data.ts",
    "export_schedule_csv.ts",
    "export_schedules.ts",
    "get_analytical_model.ts",
    "get_available_family_types.ts",
    "get_circuit_load.ts",
    "get_current_view_elements.ts",
    "get_current_view_info.ts",
    "get_family_parameters.ts",
    "get_family_types.ts",
    "get_material_quantities.ts",
    "get_mep_elements.ts",
    "get_mep_quantities.ts",
    "get_parameter_value.ts",
    "get_phase_filter.ts",
    "get_phases.ts",
    "get_schedule_data.ts",
    "get_schedule_fields.ts",
    "get_selected_elements.ts",
    "get_structural_quantities.ts",
    "get_view_templates.ts",
    "get_worksets.ts",
    "import_dwg.ts",
    "import_ifc.ts",
    "list_revit_api_categories.ts",
    "load_family.ts",
    "modify_family_type.ts",
    "modify_schedule_cell.ts",
    "operate_element.ts",
    "place_family_instance.ts",
    "query_revit_registry.ts",
    "query_stored_data.ts",
    "revit_templates.ts",
    "search_revit_api.ts",
    "send_code_to_revit.ts",
    "set_element_phase.ts",
    "set_element_workset.ts",
    "set_family_parameter.ts",
    "set_mep_offsets.ts",
    "set_mep_sizes.ts",
    "set_parameter_value.ts",
    "set_phase_visibility.ts",
    "set_schedule_filter.ts",
    "set_structural_material.ts",
    "sort_schedule.ts",
    "store_project_data.ts",
    "store_room_data.ts",
    "tag_all_rooms.ts",
    "tag_all_walls.ts",
    "tag_mep_elements.ts",
    "tag_structural_elements.ts",
    "unload_family.ts",
  ];

  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);
  const files = fs.readdirSync(__dirname);

  const KNOWN_NON_TOOLS = new Set(["errors.ts", "register.ts", "index.ts", "errors.js", "register.js", "index.js"]);

  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      !KNOWN_NON_TOOLS.has(file)
  );

  let registered = 0;
  for (const file of toolFiles) {
    const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
    const module = await import(importPath);

    const registerFunctionName = Object.keys(module).find(
      (key) => key.startsWith("register") && typeof module[key] === "function"
    );

    if (registerFunctionName) {
      module[registerFunctionName](server);
      registered++;
      console.error(`Registered tool: ${file}`);
    } else {
      console.warn(`Warning: no register function in ${file}`);
    }
  }

  if (registered < EXPECTED_TOOLS.length) {
    const missing = EXPECTED_TOOLS.filter(
      (t) => !toolFiles.some((f) => f.replace(/\.(ts|js)$/, ".js") === t.replace(/\.ts$/, ".js"))
    );
    throw new Error(
      `Tool registration incomplete: expected ${EXPECTED_TOOLS.length}, got ${registered}. Missing: ${missing.join(", ")}`
    );
  }
}
