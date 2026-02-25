#!/usr/bin/env node
/**
 * Run the same checks as the Docs Guardrails workflow locally.
 * Run from repo root: node ./scripts/docs/run-release-gate.mjs
 * Use before committing to catch markdown lint, OpenAPI drift, and spell issues.
 */

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

const repoRoot = process.cwd();

function run(name, cmd, args, opts = {}) {
  const { status, error, stdout, stderr } = spawnSync(cmd, args, {
    cwd: repoRoot,
    stdio: "inherit",
    shell: opts.shell ?? false,
    ...opts,
  });
  if (error) {
    console.error(`[${name}] Failed to run ${cmd}:`, error.message);
    process.exit(1);
  }
  if (status !== 0 && status != null) {
    console.error(`[${name}] Exited with ${status}`);
    process.exit(status);
  }
}

function collectMdFiles(dir, out) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const e of entries) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (e.name === "node_modules" || e.name === ".git" || e.name === "bin" || e.name === "obj" || e.name === "TestResults" || e.name === ".nuget") continue;
      collectMdFiles(full, out);
    } else if (e.name.toLowerCase().endsWith(".md")) {
      out.push(full);
    }
  }
}

async function main() {
  console.log("--- 1. Link check + Mermaid sanity (guardrails.mjs) ---");
  run("guardrails", "node", [path.join(repoRoot, "scripts/docs/guardrails.mjs")]);

  console.log("\n--- 2. Generate OpenAPI artifact ---");
  const psScript = path.join(repoRoot, "scripts/generate-openapi.ps1");
  const pwsh = spawnSync("pwsh", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", psScript], { cwd: repoRoot, stdio: "inherit" });
  if (pwsh.status !== 0) {
    if (process.platform === "win32") {
      const ps = spawnSync("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", psScript], { cwd: repoRoot, stdio: "inherit" });
      if (ps.status !== 0) process.exit(ps.status ?? 1);
    } else {
      process.exit(pwsh.status ?? 1);
    }
  }

  console.log("\n--- 3. Verify OpenAPI artifact is up to date ---");
  const diff = spawnSync("git", ["diff", "--exit-code", "--", "docs/api/openapi.json"], { cwd: repoRoot });
  if (diff.status !== 0) {
    console.error("OpenAPI artifact is out of date: docs/api/openapi.json");
    console.error("Regenerate and commit: powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\generate-openapi.ps1");
    process.exit(1);
  }

  console.log("\n--- 4. Markdown lint (docs + README only) ---");
  run("markdownlint", "npx", ["--yes", "markdownlint-cli2"], { shell: true });

  console.log("\n--- 5. Spell check (docs + README) ---");
  const mdFiles = [];
  const docsDir = path.join(repoRoot, "docs");
  if (fs.existsSync(docsDir)) collectMdFiles(docsDir, mdFiles);
  const readme = path.join(repoRoot, "README.md");
  if (fs.existsSync(readme)) mdFiles.push(readme);
  if (mdFiles.length > 0) {
    const cspellJson = path.join(repoRoot, "cspell.json");
    if (fs.existsSync(cspellJson)) {
      run("cspell", "npx", ["--yes", "cspell", "lint", "-c", cspellJson, ...mdFiles], { shell: true });
    } else {
      console.log("No cspell.json found; skipping spell check.");
    }
  }

  console.log("\n--- Release gate passed. ---");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
