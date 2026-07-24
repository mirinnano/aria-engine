import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";
import { dirname, relative, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const uiRoot = resolve(here, "..");
const gameRoot = resolve(uiRoot, "..");
const repository = resolve(gameRoot, "../..");
const runtime = resolve(repository, "target/aria-web-runtime-tauri");
const output = resolve(gameRoot, "dist/web");
const runtimeStamp = resolve(repository, "target/aria-web-runtime-tauri.fingerprint");
const presentationCache = resolve(repository, "target/aria-presentation-tauri");
const presentationStamp = resolve(repository, "target/aria-presentation-tauri.fingerprint");
const release = process.env.ARIA_RELEASE === "true";
const profile = process.env.ARIA_PAK_PROFILE || (release ? "signed" : "dev");
const force = process.env.ARIA_FORCE_REBUILD === "true";

// npm does not necessarily retain ~/.cargo/bin on PATH. Prefer the user's
// rustup proxies so this script honors rust-toolchain.toml and can find the
// matching WASM target instead of accidentally invoking a system Cargo.
function rustTool(name, configured = undefined) {
  if (configured) return configured;
  const candidate = resolve(process.env.HOME ?? "", ".cargo", "bin", name);
  return existsSync(candidate) ? candidate : name;
}

const cargo = rustTool("cargo", process.env.CARGO);
const wasmBindgen = rustTool("wasm-bindgen");

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: repository, stdio: "inherit", ...options });
  if (result.status !== 0) process.exit(result.status || 1);
}

function sourceFiles(root, relativeRoot = "") {
  const absolute = resolve(root, relativeRoot);
  if (!existsSync(absolute)) return [];
  const stat = statSync(absolute);
  if (stat.isFile()) return [absolute];
  const files = [];
  for (const entry of readdirSync(absolute, { withFileTypes: true })) {
    if (["node_modules", "dist", "target", ".git", ".aria-presentation"].includes(entry.name)) continue;
    const child = relativeRoot ? `${relativeRoot}/${entry.name}` : entry.name;
    if (entry.isDirectory()) files.push(...sourceFiles(root, child));
    else if (entry.isFile()) files.push(resolve(root, child));
  }
  return files;
}

function fingerprint(label, roots) {
  const hash = createHash("sha256").update(label);
  const files = roots.flatMap(([root, relativeRoot]) => sourceFiles(root, relativeRoot));
  files.sort();
  for (const file of files) {
    hash.update(relative(repository, file));
    hash.update(readFileSync(file));
  }
  return hash.digest("hex");
}

function stampMatches(path, value) {
  return !force && existsSync(path) && readFileSync(path, "utf8").trim() === value;
}

function ensureFrontend() {
  const publicKeyId = process.env.ARIA_PAK_VERIFICATION_KEY_ID || "";
  const publicKeyHex = process.env.ARIA_PAK_VERIFICATION_KEY_HEX || "";
  const value = fingerprint(`umikaze-presentation:${process.env.ARIA_PRESENTATION_SOURCEMAP === "true"}:${publicKeyId}:${publicKeyHex}`, [
    [uiRoot, "src"],
    [uiRoot, "public"],
    [uiRoot, "index.html"],
    [uiRoot, "package.json"],
    [uiRoot, "package-lock.json"],
    [repository, "ui/packages/aria-ui-sdk"],
  ]);
  if (stampMatches(presentationStamp, value) && existsSync(resolve(presentationCache, "index.html"))) {
    console.log(`  Reusing presentation cache: ${presentationCache}`);
    return;
  }
  rmSync(presentationCache, { recursive: true, force: true });
  mkdirSync(presentationCache, { recursive: true });
  console.log("  Building presentation (cache miss)...");
  run("npm", ["run", "build"], {
    cwd: uiRoot,
    env: {
      ...process.env,
      ARIA_PRESENTATION_OUT_DIR: presentationCache,
      VITE_ARIA_PAK_VERIFICATION_KEY_ID: publicKeyId,
      VITE_ARIA_PAK_VERIFICATION_KEY_HEX: publicKeyHex,
    },
  });
  if (!existsSync(resolve(presentationCache, "index.html"))) {
    throw new Error(`presentation build did not produce ${presentationCache}/index.html`);
  }
  writeFileSync(presentationStamp, `${value}\n`);
}

// The view-model schema is compiled into this WASM module. A content
// fingerprint lets local Tauri launches stay fast while still invalidating
// the cache when the VM, renderer, toolchain, or UI changes.
mkdirSync(runtime, { recursive: true });
const runtimeFingerprint = fingerprint("umikaze-web-runtime", [
  [repository, "Cargo.toml"],
  [repository, "Cargo.lock"],
  [repository, "rust-toolchain.toml"],
  [repository, "crates/aria-core/src"],
  [repository, "crates/aria-protection/src"],
  [repository, "crates/aria-render/src"],
  [repository, "crates/aria-web/src"],
]);
const runtimeReady = existsSync(resolve(runtime, "aria_web.js")) && existsSync(resolve(runtime, "aria_web_bg.wasm"));
if (stampMatches(runtimeStamp, runtimeFingerprint) && runtimeReady) {
  console.log(`  Reusing WASM runtime cache: ${runtime}`);
} else {
  rmSync(runtime, { recursive: true, force: true });
  mkdirSync(runtime, { recursive: true });
  run(cargo, ["build", "--release", "-p", "aria-web", "--target", "wasm32-unknown-unknown"]);
  run(wasmBindgen, [
    "--target", "web",
    "--out-dir", runtime,
    "--out-name", "aria_web",
    resolve(repository, "target/wasm32-unknown-unknown/release/aria_web.wasm"),
  ]);
  writeFileSync(runtimeStamp, `${runtimeFingerprint}\n`);
}

ensureFrontend();

const buildArgs = [
  "run", "--release", "-p", "aria-cli", "--", "build", gameRoot,
  "--target", "web", "--out", output, "--profile", profile,
];
if (release) buildArgs.push("--release");
run(cargo, buildArgs, {
  env: {
    ...process.env,
    ARIA_WEB_RUNTIME_DIR: runtime,
    ARIA_PRESENTATION_PREBUILT_DIR: presentationCache,
  },
});
