import fs from "node:fs/promises";
import path from "node:path";

const repoRoot = path.resolve(process.cwd());

const IGNORE_DIRS = new Set([
  ".git",
  "bin",
  "obj",
  "TestResults",
  ".nuget",
  "publish",
]);

const MD_DIR_HINTS = ["README.md", "docs", "documentation", "architecture", "adr", "runbooks", "ops"];

function isProbablyMarkdownRoot(relPath) {
  const normalized = relPath.replace(/\\/g, "/");
  return MD_DIR_HINTS.some((h) => normalized === h || normalized.startsWith(`${h}/`));
}

async function walk(dir, out) {
  const entries = await fs.readdir(dir, { withFileTypes: true });
  for (const e of entries) {
    const abs = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (IGNORE_DIRS.has(e.name)) continue;
      await walk(abs, out);
      continue;
    }
    if (e.isFile() && e.name.toLowerCase().endsWith(".md")) {
      const rel = path.relative(repoRoot, abs);
      if (isProbablyMarkdownRoot(rel)) out.push(abs);
    }
  }
}

function extractMarkdownLinks(markdown) {
  const rx = /\[[^\]]*]\(([^)]+)\)/g;
  const targets = [];
  for (const m of markdown.matchAll(rx)) targets.push(m[1].trim());
  return targets;
}

function isSkippableLinkTarget(target) {
  if (!target) return true;
  if (target.startsWith("#")) return true;
  if (/^(https?:)?\/\//i.test(target)) return true;
  if (/^(mailto:|tel:)/i.test(target)) return true;
  return false;
}

function resolveLink(fromFileAbs, targetRaw) {
  const target = targetRaw.split("#")[0].trim();
  if (!target) return null;
  if (path.isAbsolute(target)) return target;
  return path.resolve(path.dirname(fromFileAbs), target);
}

function extractMermaidBlocks(markdown) {
  const lines = markdown.split(/\r?\n/);
  const blocks = [];
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].trim() === "```mermaid") {
      const start = i;
      i++;
      const body = [];
      while (i < lines.length && lines[i].trim() !== "```") {
        body.push(lines[i]);
        i++;
      }
      const closed = i < lines.length && lines[i].trim() === "```";
      blocks.push({ startLine: start + 1, closed, body: body.join("\n") });
    }
  }
  return blocks;
}

function mermaidLooksSane(body) {
  const first = body
    .split(/\r?\n/)
    .map((l) => l.trim())
    .find((l) => l.length > 0);
  if (!first) return false;
  return (
    /^(C4Context|C4Container|C4Component)\b/.test(first) ||
    /^sequenceDiagram\b/.test(first) ||
    /^stateDiagram-v2\b/.test(first) ||
    /^flowchart\b/.test(first) ||
    /^graph\s+(TD|LR)\b/.test(first)
  );
}

async function main() {
  const mdFiles = [];
  await walk(repoRoot, mdFiles);

  const brokenLinks = [];
  const badMermaid = [];

  for (const f of mdFiles) {
    const text = await fs.readFile(f, "utf8");

    for (const raw of extractMarkdownLinks(text)) {
      if (isSkippableLinkTarget(raw)) continue;
      const resolved = resolveLink(f, raw);
      if (!resolved) continue;
      // Skip cross-repo links (CI checks out a single repo).
      if (path.relative(repoRoot, resolved).startsWith("..")) continue;
      try {
        await fs.stat(resolved);
      } catch {
        brokenLinks.push({
          file: path.relative(repoRoot, f),
          link: raw,
          resolved: path.relative(repoRoot, resolved),
        });
      }
    }

    for (const b of extractMermaidBlocks(text)) {
      if (!b.closed) {
        badMermaid.push({
          file: path.relative(repoRoot, f),
          line: b.startLine,
          issue: "Unclosed ```mermaid fence",
        });
        continue;
      }
      if (!mermaidLooksSane(b.body)) {
        badMermaid.push({
          file: path.relative(repoRoot, f),
          line: b.startLine,
          issue: "Mermaid block first line not recognized (syntax sanity check)",
        });
      }
    }
  }

  if (brokenLinks.length) {
    console.error("Broken relative links:");
    for (const x of brokenLinks) console.error(`- ${x.file}: (${x.link}) -> ${x.resolved}`);
  }
  if (badMermaid.length) {
    console.error("Mermaid sanity issues:");
    for (const x of badMermaid) console.error(`- ${x.file}:${x.line} ${x.issue}`);
  }

  if (brokenLinks.length || badMermaid.length) process.exit(1);
  console.log(`OK: ${mdFiles.length} markdown files scanned; no broken relative links; mermaid fences look sane.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

