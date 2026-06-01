import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const { chromium, webkit } = loadPlaywright();

const args = parseArgs(process.argv.slice(2));
const webPackageDir = path.resolve(getArg("webPackageDir", "artifacts/web/AriaEngine-dev-web"));
const outputPath = path.resolve(getArg("outputPath", "artifacts/release/readiness/web-browser-qa-manifest.json"));
const browserName = getArg("browser", "Chrome");
const viewportName = getArg("viewportName", "desktop-16x9");
const viewportWidth = Number(getArg("viewportWidth", "1280"));
const viewportHeight = Number(getArg("viewportHeight", "720"));
const checks = [];
const consoleErrors = [];
const SAVE_MENU_LABELS = ["SAVE", "セーブ", "保存"];
const LOAD_MENU_LABELS = ["LOAD", "ロード", "読込"];

let server;
let browser;

try {
  for (const file of ["index.html", "manifest.webmanifest", "service-worker.js", "_framework", "js/aria-web-runtime.js", "assets/web-text-assets.json"]) {
    addCheck(`package:${file}`, fs.existsSync(path.join(webPackageDir, file)), path.join(webPackageDir, file));
  }

  const port = await startServer();
  browser = await launchBrowser(browserName);
  const context = await browser.newContext({
    viewport: { width: viewportWidth, height: viewportHeight },
    deviceScaleFactor: browserName.toLowerCase() === "mobile" ? 2 : 1,
    isMobile: browserName.toLowerCase() === "mobile",
    hasTouch: browserName.toLowerCase() === "mobile"
  });
  const page = await context.newPage();
  page.on("console", message => {
    if (message.type() === "error" &&
      !message.text().includes("favicon.ico") &&
      !message.text().startsWith("Failed to load resource:")) {
      consoleErrors.push(message.text());
    }
  });
  page.on("pageerror", error => consoleErrors.push(error.message));
  page.on("response", response => {
    if (response.status() >= 400 && !response.url().endsWith("/favicon.ico")) {
      consoleErrors.push(`http ${response.status()}: ${response.url()}`);
    }
  });
  page.on("requestfailed", request => {
    const url = request.url();
    if (!url.endsWith("/favicon.ico")) {
      consoleErrors.push(`request failed: ${url} ${request.failure()?.errorText ?? ""}`.trim());
    }
  });

  await page.goto(`http://127.0.0.1:${port}/`, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.length > 0, null, { timeout: 60000 });

  const initial = await readFrame(page);
  addCheck("layout16x9", has16x9Canvas(initial.canvas), `${viewportName} ${initial.canvas.width}x${initial.canvas.height}`);
  addCheck("fontLoaded", Boolean(initial.frame.font?.sourceUrl), initial.frame.font?.sourceUrl ?? "");
  addCheck("titleStartRendered", initial.frame.drawCommands.some(command => command.text === "START"), "START text command");
  addCheck("storageBridge", await hasStorageBridge(page), "ariaWebRuntime.applyStorageOperation");
  addCheck("inputStart", await clickStartAndCheckText(page), "logical click maps to native START button");
  addCheck("rightClick", await openRightMenu(page), "contextmenu opens SAVE/LOAD overlay");
  addCheck("saveLoad", await verifySaveLoad(page), "IndexedDB save/load bridge");

  await page.waitForTimeout(250);
  addCheck("consoleErrors", consoleErrors.length === 0, consoleErrors.join("\n"));
}
catch (error) {
  addCheck("exception", false, error?.stack ?? String(error));
}
finally {
  if (browser) await browser.close();
  if (server) await new Promise(resolve => server.close(resolve));
}

const failed = checks.filter(check => check.passed !== true);
const payload = {
  generatedAtUtc: new Date().toISOString(),
  ready: failed.length === 0,
  browser: browserName,
  viewport: { name: viewportName, width: viewportWidth, height: viewportHeight },
  webPackageDir,
  consoleErrors,
  checks
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, JSON.stringify(payload, null, 2));
console.log(`Web browser QA manifest written: ${outputPath}`);
if (failed.length > 0) {
  console.error(`Web browser QA gate failed: ${failed.map(check => check.name).join(", ")}`);
  process.exit(1);
}

function loadPlaywright() {
  try {
    return require("playwright");
  }
  catch (error) {
    const nodeModules = process.env.ARIA_PLAYWRIGHT_NODE_MODULES;
    if (nodeModules) return require(path.join(nodeModules, "playwright"));
    throw error;
  }
}

function parseArgs(values) {
  const parsed = new Map();
  for (let i = 0; i < values.length; i++) {
    const value = values[i];
    if (!value.startsWith("--")) continue;
    const key = value.slice(2);
    const next = values[i + 1];
    if (next && !next.startsWith("--")) {
      parsed.set(key, next);
      i++;
    } else {
      parsed.set(key, "true");
    }
  }
  return parsed;
}

function getArg(name, fallback) {
  return args.get(name) ?? fallback;
}

function addCheck(name, passed, message) {
  checks.push({ name, passed: Boolean(passed), message: String(message ?? "") });
}

async function startServer() {
  server = http.createServer((request, response) => {
    const url = new URL(request.url ?? "/", "http://127.0.0.1");
    const pathname = url.pathname === "/" ? "/index.html" : decodeURIComponent(url.pathname);
    const filePath = path.resolve(webPackageDir, `.${pathname}`);
    if (!filePath.startsWith(webPackageDir)) {
      response.writeHead(403);
      response.end();
      return;
    }

    fs.readFile(filePath, (error, data) => {
      if (error) {
        response.writeHead(404);
        response.end();
        return;
      }

      response.writeHead(200, { "Content-Type": contentType(filePath) });
      response.end(data);
    });
  });

  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  return server.address().port;
}

function contentType(filePath) {
  const extension = path.extname(filePath).toLowerCase();
  if (extension === ".html") return "text/html; charset=utf-8";
  if (extension === ".js") return "application/javascript; charset=utf-8";
  if (extension === ".json" || extension === ".webmanifest") return "application/json; charset=utf-8";
  if (extension === ".css") return "text/css; charset=utf-8";
  if (extension === ".wasm") return "application/wasm";
  if (extension === ".png") return "image/png";
  if (extension === ".jpg" || extension === ".jpeg") return "image/jpeg";
  if (extension === ".ttf") return "font/ttf";
  if (extension === ".woff2") return "font/woff2";
  return "application/octet-stream";
}

async function launchBrowser(name) {
  switch (name.toLowerCase()) {
    case "chrome":
      return chromium.launch({ channel: "chrome" });
    case "edge":
      return chromium.launch({ channel: "msedge" });
    case "safari":
      return webkit.launch();
    case "mobile":
      return chromium.launch();
    default:
      throw new Error(`Unsupported browser target: ${name}`);
  }
}

async function readFrame(page) {
  return page.evaluate(() => {
    const canvas = document.getElementById("aria-canvas");
    const rect = canvas.getBoundingClientRect();
    return {
      frame: window.__ariaWebLastFrame,
      canvas: { width: rect.width, height: rect.height }
    };
  });
}

function has16x9Canvas(canvas) {
  const ratio = canvas.width / canvas.height;
  return Math.abs(ratio - 16 / 9) < 0.01;
}

async function clickStartAndCheckText(page) {
  await clickLogical(page, 640, 320);
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.some(command => String(command.text ?? "").includes("春が来る")), null, { timeout: 15000 });
  return page.evaluate(() => window.__ariaWebLastInputError == null);
}

async function hasStorageBridge(page) {
  return page.evaluate(() => typeof window.ariaWebRuntime?.applyStorageOperation === "function");
}

async function openRightMenu(page) {
  await clickLogical(page, 1000, 500, "right");
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.some(command => command.spriteId === -9100), null, { timeout: 15000 });
  return page.evaluate(() => {
    const commands = window.__ariaWebLastFrame?.drawCommands ?? [];
    return commands.some(command => command.spriteId === -9100) &&
      commands.some(command => command.spriteId < 0 && ["SAVE", "セーブ", "保存"].includes(command.text)) &&
      commands.some(command => command.spriteId < 0 && ["LOAD", "ロード", "読込"].includes(command.text)) &&
      window.__ariaWebLastInputError == null;
  });
}

async function verifySaveLoad(page) {
  await clickMenuText(page, SAVE_MENU_LABELS);
  await page.waitForFunction(() => window.__ariaWebLastStorageOperation?.stored === true, null, { timeout: 15000 });
  const saved = await page.evaluate(async () => {
    const payload = await window.ariaWebRuntime.loadSlot(0);
    return typeof payload === "string" && payload.trimStart().startsWith("{") && payload.includes("\"SlotId\": 0");
  });
  if (!saved) return false;

  await clickLogical(page, 1000, 500, "right");
  await page.waitForFunction(labels => window.__ariaWebLastFrame?.drawCommands?.some(command => command.spriteId < 0 && labels.includes(command.text)), LOAD_MENU_LABELS, { timeout: 15000 });
  await page.evaluate(() => { window.__ariaWebLastStorageOperation = null; });
  await clickMenuText(page, LOAD_MENU_LABELS);
  await page.waitForFunction(() => window.__ariaWebLastStorageOperation?.loaded === true, null, { timeout: 15000 });
  return page.evaluate(() => window.__ariaWebLastInputError == null);
}

async function clickMenuText(page, labels) {
  const values = Array.isArray(labels) ? labels : [labels];
  const command = await page.evaluate(items => {
    return (window.__ariaWebLastFrame?.drawCommands ?? []).find(command => command.spriteId < 0 && items.includes(command.text));
  }, values);
  if (!command) throw new Error(`Menu command not found: ${values.join("/")}`);
  const box = await page.locator("#aria-canvas").boundingBox();
  await page.mouse.click(box.x + command.cssX + command.cssWidth / 2, box.y + command.cssY + command.cssHeight / 2);
}

async function clickLogical(page, logicalX, logicalY, button = "left") {
  const data = await page.evaluate(({ x, y }) => {
    const canvas = document.getElementById("aria-canvas");
    const rect = canvas.getBoundingClientRect();
    const frame = window.__ariaWebLastFrame;
    const logicalWidth = frame?.logicalWidth ?? 1280;
    const logicalHeight = frame?.logicalHeight ?? 720;
    const scale = Math.min(rect.width / logicalWidth, rect.height / logicalHeight);
    const offsetX = Math.max(0, (rect.width - logicalWidth * scale) / 2);
    const offsetY = Math.max(0, (rect.height - logicalHeight * scale) / 2);
    return {
      pageX: rect.left + offsetX + x * scale,
      pageY: rect.top + offsetY + y * scale
    };
  }, { x: logicalX, y: logicalY });
  await page.mouse.click(data.pageX, data.pageY, { button });
}
