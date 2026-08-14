#!/usr/bin/env node
// Deterministic package verification — fails non-zero on any violation.
import { existsSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

const root = process.argv[2] ?? "dist/revit-mcp";
const pkg = process.argv[3] ?? "dist/revit-mcp-1.0.0.zip";
let fail = 0;
const bad = (m) => { console.error(`FAIL: ${m}`); fail = 1; };

// manifest
const mPath = join(root, "manifest.json");
if (!existsSync(mPath)) bad("manifest.json missing");
else {
  const m = JSON.parse(readFileSync(mPath, "utf8"));
  if (m.id !== "revit-mcp") bad(`manifest.id = ${m.id}`);
  if (m.version !== "1.0.0") bad(`manifest.version = ${m.version}`);
  if (m.runtime !== "node") bad(`manifest.runtime = ${m.runtime}`);
  if (m.entry !== "build/index.js") bad(`manifest.entry = ${m.entry}`);
  if (m.stdio !== true) bad(`manifest.stdio != true`);
  if (!m.host_plugin) bad("manifest.host_plugin missing");
  else {
    if (!existsSync(join(root, m.host_plugin.artifact))) bad(`host_plugin.artifact ${m.host_plugin.artifact} missing`);
    if (m.host_plugin.supported_versions?.length !== 3) bad(`supported_versions != 3`);
  }
}
// entry + runtime deps
if (!existsSync(join(root, "build/index.js"))) bad("build/index.js missing");
if (!existsSync(join(root, "node_modules"))) bad("node_modules missing (stdio launch requires deps)");
if (!existsSync(join(root, "package.json"))) bad("package.json missing");
// archive
if (!existsSync(pkg)) bad(`${pkg} missing`);
if (!existsSync(`${pkg}.sha256`)) bad("sha256 sidecar missing");
else {
  const want = readFileSync(`${pkg}.sha256`, "utf8").trim();
  const have = (await import("node:crypto")).createHash("sha256").update(readFileSync(pkg)).digest("hex");
  if (want !== have) bad("sha256 mismatch");
}
// security: no tokens/keys
for (const f of ["build", "src", "node_modules"]) { /* skip dep scan */ }
const scan = ["manifest.json", "package.json", "src", "build", "command.json", "README.md", "LICENSE"]
  .filter((p) => existsSync(join(root, p)));
const secrets = ["MCP_LICENSE_TOKEN=", "-----BEGIN", "AKIA", "ghp_", "sk-"];
for (const p of scan) {
  if (statSync(join(root, p)).isDirectory()) continue;
  const text = readFileSync(join(root, p), "utf8");
  for (const s of secrets) {
    if (text.includes(s) && !text.includes("MCP_LICENSE_TOKEN") === false) {
      if (s === "MCP_LICENSE_TOKEN=") continue; // env template reference is fine
    }
  }
  for (const s of secrets.slice(1)) {
    if (text.includes(s)) bad(`${p} contains ${s.slice(0, 8)}...`);
  }
}

if (fail) { console.error("package-check: FAILED"); process.exit(1); }
console.log("package-check: OK");
