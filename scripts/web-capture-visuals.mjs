import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium, webkit } = loadPlaywright();

const args = parseArgs(process.argv.slice(2));
const webPackageDir = path.resolve(getArg("webPackageDir", "artifacts/web/AriaEngine-dev-web"));
const outputDir = path.resolve(getArg("outputDir", "artifacts/visual/web"));
const browserName = getArg("browser", "Chrome");
const viewportWidth = Number(getArg("viewportWidth", "1280"));
const viewportHeight = Number(getArg("viewportHeight", "720"));
const captures = [];
const errors = [];

let server;
let browser;

try {
  fs.mkdirSync(outputDir, { recursive: true });
  const port = await startServer();
  browser = await launchBrowser(browserName);
  const page = await browser.newPage({ viewport: { width: viewportWidth, height: viewportHeight } });
  page.on("pageerror", error => errors.push(error.message));
  page.on("response", response => {
    if (response.status() >= 400 && !response.url().endsWith("/favicon.ico")) {
      errors.push(`http ${response.status()}: ${response.url()}`);
    }
  });

  await page.goto(`http://127.0.0.1:${port}/`, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.length > 0, null, { timeout: 60000 });
  await page.evaluate(() => document.fonts?.ready);
  await page.waitForTimeout(500);
  await captureCanvas(page, "title.png");

  await clickLogical(page, 640, 320);
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.some(command => String(command.text ?? "").includes("春が来る")), null, { timeout: 15000 });
  await page.waitForTimeout(250);
  await captureCanvas(page, "text.png");

  await clickLogical(page, 1000, 500, "right");
  await page.waitForFunction(() => window.__ariaWebLastFrame?.drawCommands?.some(command => command.spriteId === -9100), null, { timeout: 15000 });
  await page.waitForTimeout(250);
  await captureCanvas(page, "menu.png");
}
catch (error) {
  errors.push(error?.stack ?? String(error));
}
finally {
  if (browser) await browser.close();
  if (server) await new Promise(resolve => server.close(resolve));
}

const payload = {
  generatedAtUtc: new Date().toISOString(),
  ready: errors.length === 0 && captures.length === 3 && captures.every(capture => capture.bytes > 0),
  webPackageDir,
  outputDir,
  browser: browserName,
  viewport: { width: viewportWidth, height: viewportHeight },
  captures,
  errors
};
fs.writeFileSync(path.join(outputDir, "web-capture-manifest.json"), JSON.stringify(payload, null, 2));
console.log(`Web visual captures written: ${outputDir}`);
if (!payload.ready) {
  console.error(`Web visual capture failed: ${errors.join("; ")}`);
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

async function captureCanvas(page, name) {
  const outputPath = path.join(outputDir, name);
  await page.locator("#aria-canvas").screenshot({ path: outputPath });
  const bytes = fs.statSync(outputPath).size;
  captures.push({ name, path: outputPath, bytes });
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
