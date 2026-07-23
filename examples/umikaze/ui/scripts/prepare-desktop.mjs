import { existsSync, mkdirSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const uiRoot = resolve(here, "..");
const gameRoot = resolve(uiRoot, "..");
const repository = resolve(gameRoot, "../..");
const runtime = resolve(repository, "target/aria-web-runtime-tauri");
const output = resolve(gameRoot, "dist/web");

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

// The view-model schema is compiled into this WASM module. A presence-only
// cache check makes a source update silently pair a new React app with an old
// runtime, which fails at boot (and looks like an empty native window). Build
// the optimized runtime on every packaging pass so the web and Tauri bundles
// always originate from the same source revision without shipping a debug VM
// into the native reader.
mkdirSync(runtime, { recursive: true });
run(cargo, ["build", "--release", "-p", "aria-web", "--target", "wasm32-unknown-unknown"]);
run(wasmBindgen, [
  "--target", "web",
  "--out-dir", runtime,
  "--out-name", "aria_web",
  resolve(repository, "target/wasm32-unknown-unknown/release/aria_web.wasm"),
]);

run(cargo, [
  "run", "--release", "-p", "aria-cli", "--", "build", gameRoot, "--target", "web", "--out", output,
], {
  env: { ...process.env, ARIA_WEB_RUNTIME_DIR: runtime },
});
