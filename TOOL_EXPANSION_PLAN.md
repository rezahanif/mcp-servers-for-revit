# Revit MCP Connector — Tool Expansion Plan

**Date:** 2026-08-25
**Repo:** mcp-servers-for-revit
**Current state:** 29 MCP tools + 47 registry entries (25 tools + 22 C# API)
**Target:** Cover 9 missing categories with ~69 new tools

---

## API Sources Used

1. **revit_api_index.json** — 30 namespaces, ~200 classes (SDK + revitapidocs.com)
2. **revit_function_registry.json** — 47 verified entries (25 MCP tools + 22 C# API calls)
3. **Autodesk Revit SDK 2025** — official .NET API documentation
4. **revitapidocs.com** — community API reference

---

## Current Coverage vs Missing

| Category | Current | Missing | New Tools Needed |
|---|---|---|---|
| MEP (ducts, pipes, electrical) | 0 | All MEP operations | 20 |
| Structural analysis | 1 | Beyond framing | 10 |
| Scheduling | 0 | All scheduling | 8 |
| Phasing | 0 | All phasing | 5 |
| Worksets | 0 | All worksets | 3 |
| Import/Export | 0 | DWG, IFC, DFX | 6 |
| Family editing | 0 | Load, edit, create families | 8 |
| Parameter creation | 0 | Shared/project params | 5 |
| View templates | 0 | Create, apply, manage | 4 |
| **Total** | **29** | | **+69 → 98 tools** |

---

## Tool Definitions by Category

### 1. MEP Tools (20 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_duct` | Create duct segments with fittings | Duct, DuctType, DuctFitting |
| `create_pipe` | Create pipe segments with fittings | Pipe, PipeType, PipeFitting |
| `create_electrical_equipment` | Place electrical equipment | ElectricalEquipment |
| `create_wire` | Draw wire connections | Wire, WireType |
| `create_duct_system` | Create HVAC duct systems | MechanicalSystem |
| `create_pipe_system` | Create piping systems | PlumbingSystem |
| `create_electrical_system` | Create electrical circuits | ElectricalSystem |
| `get_mep_elements` | Query MEP elements by system/category | FilteredElementCollector |
| `set_mep_sizes` | Set duct/pipe sizes and insulation | Duct.Size, Pipe.Size |
| `connect_mep_to_space` | Connect MEP to room spaces | Space, MEPModel |
| `create_hvac_zone` | Create HVAC zoning | MechanicalSystem |
| `create_plumbing_fixture` | Place plumbing fixtures | PlumbingFixtures |
| `get_mep_quantities` | Extract MEP material quantities | MEPModel, UnitUtils |
| `set_mep_offsets` | Set MEP element offsets | Duct offset, Pipe offset |
| `create_fire_protection` | Create fire protection piping | PipeType, PlumbingSystem |
| `tag_mep_elements` | Tag MEP elements in views | IndependentTag |
| `color_mep_by_system` | Color MEP elements by system | OverrideGraphicsSettings |
| `create_electrical_panel** | Place electrical panels | ElectricalEquipment |
| `get_circuit_load` | Calculate electrical circuit loads | ElectricalSystem |
| `export_mep_schedules** | Export MEP data to schedules | ViewSchedule |

**API classes from index:** Duct, DuctType, MechanicalEquipment, MechanicalSystem, ElectricalSystem, ElectricalEquipment, WireType, Pipe, PipeType, PlumbingSystem, PlumbingFixtures

---

### 2. Structural Analysis Tools (10 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_structural_column` | Place structural columns | FamilyInstance, StructuralColumn |
| `create_structural_wall` | Create structural walls | Wall, WallType |
| `create_structural_foundation` | Place foundations | Foundation |
| `create_rebar` | Place reinforcement bars | Rebar, RebarBarType |
| `get_analytical_model` | Query analytical model | AnalyticalMember, AnalyticalModel |
| `set_structural_material` | Assign structural materials | Material, StructuralMaterial |
| `get_structural_quantities` | Extract structural quantities | StructuralMember |
| `create_steel_connection` | Create steel connections | SteelConnection, SteelProfile |
| `analyze_load_combinations** | Analyze load combinations | LoadCombination |
| `tag_structural_elements** | Tag structural elements | IndependentTag |

**API classes from index:** StructuralFraming, StructuralMember, Foundation, AnalyticalMember, SteelProfile, SteelConnection, SteelMaterial

---

### 3. Scheduling Tools (8 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_schedule` | Create a new schedule/view | ViewSchedule |
| `get_schedule_fields` | List schedule fields | ScheduleField |
| `add_schedule_field` | Add fields to a schedule | ScheduleField |
| `set_schedule_filter` | Apply filters to schedules | ScheduleFilter |
| `sort_schedule` | Set sort/group criteria | ScheduleSortGroupField |
| `export_schedule_csv` | Export schedule to CSV | ViewSchedule, CSV |
| `get_schedule_data` | Read schedule cell data | ViewSchedule |
| `modify_schedule_cell` | Edit schedule cell values | ViewSchedule |

**API classes from index:** ViewSchedule (in Autodesk.Revit.DB)

---

### 4. Phasing Tools (5 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_phase` | Create a new project phase | Phase |
| `get_phases` | List all project phases | Phase |
| `set_element_phase` | Assign phase to elements | Phase, Element.PhasedCreated |
| `get_phase_filter` | Get phase filter settings | PhaseFilter |
| `set_phase_visibility** | Control phase visibility in views | PhaseFilter, View |

---

### 5. Workset Tools (3 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_workset` | Create a new workset | Workset |
| `get_worksets` | List all worksets | WorksetTable |
| `set_element_workset` | Assign elements to worksets | Workset |

---

### 6. Import/Export Tools (6 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `export_dwg` | Export view/sheet to DWG | DWGExportOptions |
| `import_dwg` | Import DWG file | DWGImportOptions |
| `export_ifc` | Export to IFC format | IFCExportConfiguration, IFCExportUtils |
| `import_ifc` | Import IFC file | IFCImportOptions |
| `export_dfx` | Export to DFX format | DXFExportOptions |
| `export_schedules` | Export all schedules to Excel/CSV | ViewSchedule |

**API classes from index:** IFCExportConfiguration, IFCExportUtils

---

### 7. Family Editing Tools (8 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `load_family` | Load family from RFA file | Family, FamilySymbol |
| `get_family_types` | List loaded family types | FamilySymbol |
| `create_family_type` | Create new family type | FamilySymbol |
| `modify_family_type` | Edit family type parameters | FamilySymbol, FamilyParameter |
| `place_family_instance` | Place instance at location | FamilyInstance |
| `get_family_parameters** | List family parameters | FamilyParameter |
| `set_family_parameter** | Set family parameter values | FamilyParameter |
| `unload_family** | Unload family from project | Family |

**API classes from index:** Family, FamilySymbol, FamilyInstance

---

### 8. Parameter Tools (5 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_shared_parameter` | Create a shared parameter definition | SharedParameterFile |
| `create_project_parameter` | Create project parameter | Parameter, BuiltInParameter |
| `bind_parameter` | Bind parameter to categories | BindingMap |
| `get_parameter_value` | Read parameter values | Parameter |
| `set_parameter_value` | Write parameter values | Parameter |

**API classes from index:** Parameter, BuiltInParameter

---

### 9. View Template Tools (4 tools)

| Tool | Description | API Classes Used |
|---|---|---|
| `create_view_template` | Create view template from view | View, ViewTemplate |
| `apply_view_template` | Apply template to view | View |
| `get_view_templates` | List all view templates | View |
| `delete_view_template` | Remove view template | ViewTemplate |

---

## Summary

| Category | New Tools | API Classes | Priority |
|---|---|---|---|
| MEP | 20 | Duct, Pipe, ElectricalEquipment, etc. | High — common BIM ops |
| Structural | 10 | StructuralFraming, Foundation, Steel, etc. | High — core AEC |
| Scheduling | 8 | ViewSchedule, ScheduleField | Medium — reporting |
| Import/Export | 6 | DWGExportOptions, IFCExportConfiguration | Medium — interoperability |
| Family editing | 8 | Family, FamilySymbol, FamilyInstance | Medium — content mgmt |
| Parameter creation | 5 | Parameter, BuiltInParameter | Medium — data mgmt |
| Phasing | 5 | Phase, PhaseFilter | Low — project mgmt |
| View templates | 4 | View, ViewTemplate | Low — workflow mgmt |
| Worksets | 3 | Workset | Low — collaboration |
| **Total** | **69** | | |

---

## Implementation Order

1. **Phase 1 (High priority):** MEP (20) + Structural (10) = 30 tools
2. **Phase 2 (Medium):** Scheduling (8) + Import/Export (6) + Family (8) + Parameters (5) = 27 tools
3. **Phase 3 (Low):** Phasing (5) + View templates (4) + Worksets (3) = 12 tools

**Estimated effort:** ~2 weeks for Phase 1, ~2 weeks for Phase 2, ~1 week for Phase 3.
