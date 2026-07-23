import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// `aria build --target web` copies this dist directory next to the bytecode,
// pak, wasm glue, and scene renderer. Relative paths keep the same package
// usable from a PWA host and from Tauri's bundled WebView.
export default defineConfig({
  base: "./",
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    port: 1420,
    strictPort: true,
  },
  build: {
    outDir: process.env.ARIA_PRESENTATION_OUT_DIR || "dist",
    emptyOutDir: true,
    // Source maps are useful for local diagnosis but needlessly enlarge and
    // disclose a release build. CI/debug builds can opt in explicitly.
    sourcemap: process.env.ARIA_PRESENTATION_SOURCEMAP === "true",
  },
});
