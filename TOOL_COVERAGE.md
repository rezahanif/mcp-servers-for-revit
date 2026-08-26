# AiConnect MCP Connector — Tool Coverage Model

This document defines how each AiConnect MCP connector covers its host application's API surface, the ratio between tools and API methods, and the workflow architecture.

---

## Current API Data (from static scans)

| Connector | API Source | API Size | Scan Method | Complete? |
|---|---|---|---|---|
| abaqus | abqpy SWIG stubs | **1,194 classes** | Perl wrapper scan on Windows | ✅ Yes |
| sap2000 | SAP2000 OAPI registry | **241 verified methods** | Manual verification + API docs | ⚠️ Partial (full OAPI ~500-1000+) |
| ansys-cfx | Base class + catalog | **20 defined, 10 exposed** | Code analysis | ⚠️ Partial (full CFX API larger) |
| qgis | PyQGIS SDK | **~300 ops** (estimated) | Partial SDK scan | ⚠️ Partial |
| discovery-studio | Perl wrapper scan | **143 functions** | Static scan of install dir | ✅ Yes |
| revit | SDK docs + registry | **3000+ classes** (indexed ~200) | SDK + revitapidocs.com | ⚠️ Partial (index covers ~7%) |
| office | MS docs + COM reflection | **3000+ methods** (indexed ~54) | Learn.microsoft.com (manual) | ⚠️ Partial |

---

## Coverage Architecture

AiConnect connectors use three layers:

### Layer A: Dedicated Tools
Pre-built tools with typed parameters and error handling. Safe, validated, limited to what's implemented.

### Layer B: API Guidance
Search/registry tools that help the agent discover the host API:
- `search_<domain>_api` — keyword search
- `<domain>_function_registry` — exact lookup
- `list_<domain>_api_categories` — browse by namespace

### Layer C: Exec Hatch
Generic code execution covering 100% of the API:
- `run_python` (abaqus) — Python code to Abaqus kernel
- `execute_sap_function` (sap2000) — any OAPI method via COM
- `send_code_to_revit` (revit) — C# code to Revit add-in
- `execute_code` (qgis) — PyQGIS code in plugin

---

## Per-Connector Coverage

### 1. abaqus-mcp — 1,194 classes

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 6 | ping, set_workdir, monitor_job_status, inspect_odb, capture_viewport, get_error_hints |
| Layer B | 1 | 88 documented functions in api_registry.json |
| Layer C | 1 | `run_python` — sends Python to Abaqus kernel |

**Workflow:** Agent searches API → writes PyAbaqus code → `run_python` executes in kernel.
**API coverage: 100%** (exec hatch covers all 1,194 classes)

---

### 2. sap2000-mcp — 241+ methods

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 12 | Lifecycle (launch, connect, quit) + query (get_model_info, etc.) |
| Layer B | 1 | search_api_docs (241 verified entries + 25 API doc files) |
| Layer C | 1 | `execute_sap_function` — any OAPI method via COM dot-path |

**Workflow:** Agent searches API docs → calls `execute_sap_function("SapModel.FrameObj.AddByCoord", args)` → COM dispatch.
**API coverage: 100%** (exec hatch covers full OAPI, 241 verified + unknown total)

---

### 3. ansys-cfx-mcp — 20 defined, 10 exposed

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 10 | session_status, connect, disconnect, cfx_workflow, cfx_model_context, run_code, validate_code, find_api, get_help, error_remediation |
| Layer B | 3 | Via cfx_model_context action parameter (dark) |
| Layer C | 1 | `run_code` — executes arbitrary CFX code |

**Workflow:** Agent uses cfx_model_context(action="find_api") → discovers API → run_code executes.
**API coverage: 100%** (exec hatch), dedicated ~50% of defined tools

---

### 4. qgis-mcp — ~300 operations

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 124 | Layer CRUD, feature editing, symbology, processing, rendering, print layouts |
| Layer B | 6 | search_qgis_api, function_registry, categories, templates |
| Layer C | 1 | `execute_code` — PyQGIS code in plugin |

**Workflow:** Most ops via dedicated tools. Edge cases via execute_code. Layer B for discovery.
**API coverage: ~85%** dedicated + 100% exec = **100% effective**

---

### 5. discovery-studio-mcp — 143 functions

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 15 | Structure ops, protocol management, rendering |
| Layer B | 2 | ds_search_api (88 documented), ds_function_registry |
| Layer C | 0 | **No exec hatch** |

**Workflow:** Agent discovers API via Layer B but cannot execute arbitrary code. Limited to 15 pre-built tools.
**API coverage: ~10%** (15/143 dedicated, no exec)

---

### 6. revit-mcp — 3,000+ classes

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 29 | Room/level/grid creation, element querying, annotation |
| Layer B | 2 | search_revit_api (30-namespace index), query_revit_registry (49 entries) |
| Layer C | 1 | `send_code_to_revit` — C# code to Revit add-in |

**Workflow:** Agent searches 30-namespace API index → sends C# code to Revit add-in → executes.
**API coverage: <1%** dedicated + **100%** exec = **100% effective**

---

### 7. office-mcp — 3,000+ methods

| Layer | Tools | What they cover |
|---|---|---|
| Layer A | 54 | 13 COM lifecycle + 18 OOXML CRUD + 16 MSProject + 7 Layer B |
| Layer B | 7 | search_office_api, function_registry, categories, templates |
| Layer C | 0 | **No exec hatch** |

**Workflow:** Limited to 54 pre-built tools. OOXML tools cover document operations (python-docx, openpyxl, python-pptx). No application automation beyond COM lifecycle.
**API coverage: ~1.8%** (54/3000+, no exec)

---

## Coverage Summary

| Connector | API Size | Dedicated | Exec Hatch | Effective Coverage |
|---|---|---|---|---|
| abaqus | 1,194 | 6 (0.5%) | ✅ run_python | **100%** |
| sap2000 | 241+ | 12 (5%) | ✅ execute_sap_function | **100%** |
| ansys-cfx | 20 | 10 (50%) | ✅ run_code | **100%** |
| qgis | ~300 | 124 (41%) | ✅ execute_code | **100%** |
| revit | 3,000+ | 29 (<1%) | ✅ send_code_to_revit | **100%** |
| discovery-studio | 143 | 15 (10%) | ❌ None | **10%** |
| office | 3,000+ | 54 (1.8%) | ❌ None | **1.8%** |

---

## The Benchmark Paradox

The E dimension measures **dedicated tool count** — but real capability comes from the **exec hatch**:

| Connector | E Score | Can Actually Do |
|---|---|---|
| abaqus | 100 | Everything (1,194 classes via exec) |
| sap2000 | 95 | Everything (full OAPI via exec) |
| qgis | 95 | Everything (124 tools + exec) |
| revit | 70 | Everything (3,000+ classes via exec) |
| office | 65 | Very little (54 tools, no exec) |
| discovery-studio | 65 | Little (15 tools, no exec) |

**Office has the highest dedicated tool count (54) among non-exec connectors but the lowest effective coverage.** Revit has the lowest dedicated count (29) but 100% effective coverage.

---

## Optimal Architecture

The best-performing connectors use **Layer A + Layer B + Layer C**:

```
Agent discovers API (Layer B) → Agent writes code → Exec hatch executes (Layer C)
                                                      ↓
                                              Typed errors return
```

This is what abaqus, sap2000, and revit use. The agent handles the intelligence; the tool handles the transport.

Connectors without exec hatches (office, discovery-studio) are limited to whatever Layer A implements — which is never more than a small fraction of a 3,000+ method API.
