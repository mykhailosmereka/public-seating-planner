#!/usr/bin/env node
// Fails (exit 2) if any NuGet or npm dependency, direct or transitive, has a license
// outside the allowlist. Reads restore outputs (obj/project.assets.json) and
// src/frontend/package-lock.json, so run `dotnet restore` / `npm install` first.
//
// Usage:
//   node tools/check-licenses.mjs            full check (CI / manual)
//   node tools/check-licenses.mjs --hook     Claude Code PostToolUse hook (reads hook JSON on stdin)
import { execFileSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const srcDir = join(root, "src");
const frontendDir = join(srcDir, "frontend");

const ALLOWED = new Set([
  "MIT", "MIT-0", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "0BSD",
  "BlueOak-1.0.0", "CC0-1.0", "Unlicense", "Zlib", "PostgreSQL", "Python-2.0",
  "MPL-2.0",   // file-level copyleft only; fine while unmodified
  "CC-BY-4.0", // data packages such as caniuse-lite
]);

// Reviewed packages whose license metadata is not an SPDX id. Keyed by name@version
// because licenses can change between versions.
const exceptionsFile = join(root, "tools", "license-exceptions.json");
const EXCEPTIONS = existsSync(exceptionsFile) ? JSON.parse(readFileSync(exceptionsFile, "utf8")) : {};

function isAllowed(expr) {
  if (!expr) return false;
  const clean = expr.replace(/[()]/g, " ").trim();
  // SPDX: "A OR B" passes if any branch passes; "A AND B" needs every part.
  return clean.split(/\s+OR\s+/i).some(branch =>
    branch.split(/\s+AND\s+/i).every(id => ALLOWED.has(id.trim().replace(/\+$/, ""))));
}

function checkNuGet() {
  const found = new Map();
  for (const project of readdirSync(srcDir)) {
    const assetsPath = join(srcDir, project, "obj", "project.assets.json");
    if (!existsSync(assetsPath)) continue;
    const assets = JSON.parse(readFileSync(assetsPath, "utf8"));
    // packageFolders holds the paths of whichever OS ran the restore (Windows vs WSL),
    // so fall back to this machine's NuGet cache.
    const folders = [...Object.keys(assets.packageFolders ?? {}), process.env.NUGET_PACKAGES, join(homedir(), ".nuget", "packages")]
      .filter(Boolean);
    for (const [key, lib] of Object.entries(assets.libraries)) {
      if (lib.type !== "package" || found.has(key)) continue;
      const [name, version] = key.split("/");
      let license = null;
      for (const folder of folders) {
        const nuspec = join(folder, lib.path, `${name.toLowerCase()}.nuspec`);
        if (!existsSync(nuspec)) continue;
        const xml = readFileSync(nuspec, "utf8");
        const m = xml.match(/<license\s+type="expression"\s*>([^<]+)</);
        license = m ? m[1].trim() : (xml.match(/<license\s+type="file"\s*>([^<]+)</)?.[1] ? "license file" : xml.match(/<licenseUrl>([^<]+)</)?.[1] ?? "none");
        break;
      }
      found.set(key, { id: `nuget:${name}@${version}`, license: license ?? "not in package cache", project });
    }
  }
  return [...found.values()];
}

function checkNpm() {
  const lockPath = join(frontendDir, "package-lock.json");
  if (!existsSync(lockPath)) return [];
  const lock = JSON.parse(readFileSync(lockPath, "utf8"));
  const result = [];
  for (const [path, pkg] of Object.entries(lock.packages ?? {})) {
    if (!path || pkg.link) continue;
    const name = pkg.name ?? path.replace(/^.*node_modules\//, "");
    let license = pkg.license;
    if (!license) {
      try { license = JSON.parse(readFileSync(join(frontendDir, path, "package.json"), "utf8")).license; } catch { }
    }
    if (license && typeof license === "object") license = license.type;
    result.push({ id: `npm:${name}@${pkg.version}`, license: license ?? "none", project: "frontend" });
  }
  return result;
}

function runCheck() {
  const all = [...checkNuGet(), ...checkNpm()];
  const bad = all.filter(p => !isAllowed(p.license) && !EXCEPTIONS[p.id]);
  return { total: all.length, bad };
}

function report({ total, bad }) {
  if (bad.length === 0) {
    console.log(`License check passed: ${total} packages.`);
    return 0;
  }
  console.error(`License check FAILED: ${bad.length} of ${total} packages have a non-allowlisted license.`);
  for (const p of bad) console.error(`  ${p.id} (${p.project}): ${p.license}`);
  console.error("Remove or replace these packages. If one is actually permissive (e.g. a license file you reviewed), " +
    "add it with the reason to tools/license-exceptions.json. See the Dependencies section in AGENTS.md.");
  return 2;
}

function hookMode() {
  let input;
  try { input = JSON.parse(readFileSync(0, "utf8")); } catch { return 0; }
  const tool = input.tool_name;
  const ti = input.tool_input ?? {};

  if (tool === "Bash") {
    const cmd = ti.command ?? "";
    const relevant = /\bdotnet\s+(add\b.*\bpackage|restore|remove\b.*\bpackage)\b|\bnpm\s+(i|install|add|update|up|ci|uninstall)\b/.test(cmd);
    if (!relevant) return 0;
  } else if (tool === "Edit" || tool === "Write" || tool === "MultiEdit") {
    const file = ti.file_path ?? "";
    if (/\.csproj$|Directory\.Packages\.props$/.test(file)) {
      // The assets file only changes on restore, so restore before checking.
      try {
        const target = /\.csproj$/.test(file) ? file : root;
        execFileSync("dotnet", ["restore", target, "--verbosity", "quiet"], { stdio: ["ignore", "ignore", "pipe"], timeout: 150_000 });
      } catch (e) {
        console.error(`License check could not restore ${file}: ${e.stderr?.toString().trim() || e.message}`);
        return 2;
      }
    } else if (/package\.json$/.test(file)) {
      console.error("package.json changed: run `npm install` in src/frontend so the license check can see the new dependencies.");
      return 2;
    } else {
      return 0;
    }
  } else {
    return 0;
  }
  return report(runCheck());
}

process.exitCode = process.argv.includes("--hook") ? hookMode() : report(runCheck());
