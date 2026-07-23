import { dotnet } from "./_framework/dotnet.js";

const DB_NAME = "aria-engine";
const DB_VERSION = 1;
const canvas = document.getElementById("canvas");
const fatal = document.getElementById("fatal-error");
const moduleConfig = { canvas };

if ("serviceWorker" in navigator) {
  void navigator.serviceWorker.register("service-worker.js").catch(error => {
    console.error("Service worker registration failed", error);
  });
}

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains("saves")) db.createObjectStore("saves");
      if (!db.objectStoreNames.contains("settings")) db.createObjectStore("settings");
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function readStore(db, storeName) {
  return new Promise((resolve, reject) => {
    const entries = [];
    const tx = db.transaction(storeName, "readonly");
    const cursor = tx.objectStore(storeName).openCursor();
    cursor.onsuccess = () => {
      const current = cursor.result;
      if (!current) return;
      entries.push({ key: String(current.key), payload: String(current.value ?? "") });
      current.continue();
    };
    tx.oncomplete = () => resolve(entries);
    tx.onerror = () => reject(tx.error);
  });
}

async function readAllStorage() {
  const db = await openDb();
  const [saves, settings] = await Promise.all([
    readStore(db, "saves"),
    readStore(db, "settings")
  ]);
  return JSON.stringify({ saves, settings });
}

async function writeStorage(storeName, key, payload) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(storeName, "readwrite");
    tx.objectStore(storeName).put(payload, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

function showFatal(message) {
  fatal.hidden = false;
  fatal.textContent = `Raylib WASM runtime error\n\n${message}`;
}

function unlockAudio() {
  const candidates = [
    moduleConfig.audioContext,
    moduleConfig.SDL2?.audioContext,
    globalThis.Module?.audioContext,
    globalThis.Module?.SDL2?.audioContext,
    globalThis.AL?.currentCtx?.audioCtx
  ];
  for (const context of candidates) {
    if (context?.state === "suspended") void context.resume();
  }
}

canvas.addEventListener("pointerdown", () => {
  canvas.focus({ preventScroll: true });
  unlockAudio();
}, { passive: true });
canvas.addEventListener("contextmenu", event => event.preventDefault());

try {
  const runtime = await dotnet
    .withModuleConfig(moduleConfig)
    .create();
  const { setModuleImports, getAssemblyExports, getConfig, runMain } = runtime;
  setModuleImports("ariaWasm", {
    env: {
      baseUri: () => new URL("./", document.baseURI).href
    },
    storage: {
      readAll: readAllStorage,
      write: writeStorage
    },
    ui: {
      showFatal
    }
  });

  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);
  await runMain();
  globalThis.__ariaWasm = { exports, canvas, ready: true };

  const resizeObserver = new ResizeObserver(() => {
    const rect = canvas.getBoundingClientRect();
    const scale = Math.max(1, globalThis.devicePixelRatio || 1);
    const width = Math.max(1, Math.round(rect.width * scale));
    const height = Math.max(1, Math.round(rect.height * scale));
    if (canvas.width !== width || canvas.height !== height) {
      exports.AriaEngine.Wasm.BrowserEntry.Resize(width, height);
    }
  });
  resizeObserver.observe(canvas);

  const frame = timestamp => {
    try {
      if (exports.AriaEngine.Wasm.BrowserEntry.Frame(timestamp)) {
        requestAnimationFrame(frame);
      }
    } catch (error) {
      showFatal(error?.stack ?? String(error));
    }
  };
  requestAnimationFrame(frame);

  addEventListener("beforeunload", () => {
    try { exports.AriaEngine.Wasm.BrowserEntry.Shutdown(); } catch { }
  });
} catch (error) {
  showFatal(error?.stack ?? String(error));
}
