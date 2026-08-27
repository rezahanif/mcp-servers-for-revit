// Copy non-TS assets into build/.
//
// WHY: `tsc` emits only .js. The tier-2 discovery layer (search_revit_api,
// query_revit_registry, list_revit_api_categories, list_revit_templates) reads
// its corpus from JSON files resolved relative to the COMPILED module
// (`path.join(__dirname, "revit_function_registry.json")`), so without this step
// those files never exist under build/ and every discovery tool fails at runtime
// with ENOENT. That is audit finding N21: the entire tier-2 architecture — the
// thing that lets the connector cover the Revit API without paying tool-surface
// tokens — was inert in every shipped build.
//
// Node-only and path-agnostic on purpose: this connector is packaged by a
// Windows CI job, so no `cp`/`rsync` shell step.
import { readdir, mkdir, copyFile, stat } from "node:fs/promises";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const SRC = fileURLToPath(new URL("../src", import.meta.url));
const OUT = fileURLToPath(new URL("../build", import.meta.url));
const ASSET_EXT = [".json"];

let copied = 0;

async function walk(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const from = join(dir, entry.name);
    if (entry.isDirectory()) {
      await walk(from);
      continue;
    }
    if (!ASSET_EXT.some((e) => entry.name.endsWith(e))) continue;
    const to = join(OUT, relative(SRC, from));
    await mkdir(join(to, ".."), { recursive: true });
    await copyFile(from, to);
    copied++;
    console.error(`asset: ${relative(SRC, from)}`);
  }
}

try {
  await stat(OUT);
} catch {
  console.error("build/ does not exist — run tsc first");
  process.exit(1);
}
await walk(SRC);
if (copied === 0) {
  console.error("copy-assets: no assets found — expected at least the tier-2 index files");
  process.exit(1);
}
console.error(`copy-assets: ${copied} file(s) -> build/`);
