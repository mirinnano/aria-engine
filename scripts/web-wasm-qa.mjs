import { createServer } from "node:http";
import { readFile, stat, mkdir } from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import { chromium } from "playwright";

const packageDir = path.resolve(process.argv[2] ?? "artifacts/web-wasm/AriaEngine-qa-raylib-wasm");
const outputDir = path.resolve(process.argv[3] ?? "artifacts/web-wasm/qa");
const types = new Map([
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".webmanifest", "application/manifest+json; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".wasm", "application/wasm"],
  [".dll", "application/octet-stream"],
  [".ttf", "font/ttf"],
  [".png", "image/png"],
  [".bmp", "image/bmp"],
  [".mp3", "audio/mpeg"]
]);

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    let relative = decodeURIComponent(url.pathname).replace(/^\/+/, "");
    if (!relative) relative = "index.html";
    let file = path.resolve(packageDir, relative);
    if (!file.startsWith(packageDir + path.sep)) throw new Error("path traversal");
    if ((await stat(file)).isDirectory()) file = path.join(file, "index.html");
    const bytes = await readFile(file);
    response.writeHead(200, {
      "content-type": types.get(path.extname(file)) ?? "application/octet-stream",
      "cross-origin-opener-policy": "same-origin",
      "cross-origin-embedder-policy": "require-corp"
    });
    response.end(bytes);
  } catch {
    response.writeHead(404);
    response.end("not found");
  }
});

await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const address = server.address();
const baseUrl = `http://127.0.0.1:${address.port}/`;
await mkdir(outputDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
const page = await context.newPage();
const errors = [];
const assetRequests = [];
page.on("pageerror", error => errors.push(String(error)));
page.on("console", message => {
  if (message.type() === "error") errors.push(message.text());
});
page.on("request", request => {
  const pathname = new URL(request.url()).pathname;
  if (pathname.includes("/assets/") || pathname.endsWith("/init.aria")) assetRequests.push(pathname);
});

try {
  await page.goto(baseUrl, { waitUntil: "networkidle", timeout: 120_000 });
  await page.locator("#canvas").waitFor({ state: "visible", timeout: 30_000 });
  await page.waitForFunction(() => globalThis.__ariaWasm?.ready === true, null, { timeout: 120_000 });
  await page.waitForTimeout(1_000);

  const runtime = await page.evaluate(() => ({
    fatalHidden: document.getElementById("fatal-error")?.hidden === true,
    status: globalThis.__ariaWasm.exports.AriaEngine.Wasm.BrowserEntry.GetRuntimeStatus()
  }));
  if (!runtime.fatalHidden || runtime.status.startsWith("Uninitialized")) {
    throw new Error(`Raylib runtime did not initialize: ${JSON.stringify(runtime)}`);
  }

  const canvas = await page.locator("#canvas").evaluate(element => ({
    width: element.width,
    height: element.height,
    cssWidth: element.getBoundingClientRect().width,
    cssHeight: element.getBoundingClientRect().height
  }));
  if (canvas.width < 320 || canvas.height < 240 || canvas.cssWidth <= 0 || canvas.cssHeight <= 0) {
    throw new Error(`invalid canvas size: ${JSON.stringify(canvas)}`);
  }
  if (!assetRequests.some(url => url.endsWith("/init.aria"))) throw new Error("boot group was not requested");
  if (!assetRequests.some(url => url.includes("ui_title_menu_ocean"))) throw new Error("ui group was not requested");
  if (assetRequests.some(url => url.includes("bg_hotel_room"))) throw new Error("scenario group was fetched during boot");

  const stores = await page.evaluate(async () => {
    const request = indexedDB.open("aria-engine", 1);
    const db = await new Promise((resolve, reject) => {
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    return Array.from(db.objectStoreNames);
  });
  if (!stores.includes("saves") || !stores.includes("settings")) throw new Error("IndexedDB compatibility stores are missing");

  await page.screenshot({ path: path.join(outputDir, "title-raylib-wasm.png") });

  await page.evaluate(() => navigator.serviceWorker.ready);
  const offlineShell = await page.evaluate(async () => {
    const cacheNames = (await caches.keys()).filter(name => name.startsWith("aria-raylib-wasm-"));
    const urls = [];
    for (const name of cacheNames) {
      const cache = await caches.open(name);
      urls.push(...(await cache.keys()).map(request => new URL(request.url).pathname));
    }
    return { cacheNames, urls };
  });
  for (const required of ["/index.html", "/main.js", "/_framework/dotnet.js", "/_framework/dotnet.native.wasm"])
  {
    if (!offlineShell.urls.includes(required)) throw new Error(`offline shell missing ${required}`);
  }
  await page.reload({ waitUntil: "networkidle", timeout: 120_000 });
  await context.setOffline(true);
  await page.reload({ waitUntil: "domcontentloaded", timeout: 120_000 });
  await page.locator("#canvas").waitFor({ state: "visible", timeout: 30_000 });
  await page.waitForFunction(() => globalThis.__ariaWasm?.ready === true, null, { timeout: 120_000 });
  const offlineHealthy = await page.evaluate(() =>
    document.getElementById("fatal-error")?.hidden === true &&
    !globalThis.__ariaWasm.exports.AriaEngine.Wasm.BrowserEntry.GetRuntimeStatus().startsWith("Uninitialized"));
  if (!offlineHealthy) throw new Error("offline Raylib runtime did not initialize");
  await context.setOffline(false);

  if (errors.length) throw new Error(`browser errors:\n${errors.join("\n")}`);
  process.stdout.write(JSON.stringify({ canvas, runtime, assetRequests: [...new Set(assetRequests)], stores, offlineShell }, null, 2));
} finally {
  await context.setOffline(false).catch(() => {});
  await browser.close();
  await new Promise(resolve => server.close(resolve));
}
