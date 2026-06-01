window.ariaWebRuntime = (() => {
  const DB_NAME = "aria-engine";
  const DB_VERSION = 1;
  const images = new Map();
  let lastFrame = null;
  let lastCanvas = null;

  function measure(canvas) {
    const rect = canvas.getBoundingClientRect();
    return {
      width: Math.max(1, rect.width || 1280),
      height: Math.max(1, rect.height || 720)
    };
  }

  function fitCanvas(canvas) {
    const size = measure(canvas);
    const dpr = Math.max(1, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.round(size.width * dpr));
    const height = Math.max(1, Math.round(size.height * dpr));
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;
    const ctx = canvas.getContext("2d");
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = "high";
    return { ...size, dpr, ctx };
  }

  async function boot(canvas, dotNet) {
    lastCanvas = canvas;
    canvas.tabIndex = 0;
    canvas.addEventListener("pointerdown", (event) => {
      const rect = canvas.getBoundingClientRect();
      const x = event.clientX - rect.left;
      const y = event.clientY - rect.top;
      dotNet.invokeMethodAsync("HandlePointerDown", x, y, rect.width, rect.height, event.button ?? 0)
        .catch(error => {
          window.__ariaWebLastInputError = String(error);
          showError(canvas, String(error));
        });
    });
    canvas.addEventListener("contextmenu", (event) => {
      event.preventDefault();
      const rect = canvas.getBoundingClientRect();
      dotNet.invokeMethodAsync("HandlePointerDown", event.clientX - rect.left, event.clientY - rect.top, rect.width, rect.height, 2)
        .catch(error => {
          window.__ariaWebLastInputError = String(error);
          showError(canvas, String(error));
        });
    });
    window.addEventListener("resize", () => {
      if (lastFrame && lastCanvas) renderFrame(lastCanvas, lastFrame);
    });
  }

  function renderFrame(canvas, frame) {
    lastCanvas = canvas;
    lastFrame = frame;
    const { width, height, ctx } = fitCanvas(canvas);
    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = "#050607";
    ctx.fillRect(0, 0, width, height);
    ensureFont(frame.font);

    for (const command of frame.drawCommands ?? []) {
      if (command.kind === 0 || command.kind === "Image") drawImage(ctx, command, frame);
      else if (command.kind === 1 || command.kind === "Text") drawText(ctx, command, frame);
      else if (command.kind === 3 || command.kind === "Triangle") drawTriangle(ctx, command);
      else drawRect(ctx, command);
    }

    window.__ariaWebLastFrame = frame;
  }

  function showError(canvas, message) {
    const { width, height, ctx } = fitCanvas(canvas);
    ctx.fillStyle = "#050607";
    ctx.fillRect(0, 0, width, height);
    ctx.fillStyle = "#e7e2d6";
    ctx.font = "18px sans-serif";
    ctx.textAlign = "left";
    ctx.textBaseline = "top";
    wrapText(ctx, `Web runtime error: ${message}`, 32, 32, width - 64, 24);
  }

  function ensureFont(font) {
    if (!font?.cssDeclaration || document.getElementById("aria-runtime-font")) return;
    const style = document.createElement("style");
    style.id = "aria-runtime-font";
    style.textContent = `@font-face { ${font.cssDeclaration} font-display: swap; }`;
    document.head.appendChild(style);
  }

  function drawRect(ctx, command) {
    const scale = commandScale(command);
    const radius = Math.max(0, (command.cornerRadius ?? command.CornerRadius ?? 0) * scale);
    const shadowColor = command.shadowColor || command.ShadowColor || "";
    const shadowX = (command.shadowOffsetX ?? command.ShadowOffsetX ?? 0) * scale;
    const shadowY = (command.shadowOffsetY ?? command.ShadowOffsetY ?? 0) * scale;
    const shadowAlpha = command.shadowAlpha ?? command.ShadowAlpha ?? 128;
    const borderWidth = Math.max(0, (command.borderWidth ?? command.BorderWidth ?? 0) * scale);
    const borderColor = command.borderColor || command.BorderColor || "";
    const borderOpacity = command.borderOpacity ?? command.BorderOpacity ?? 255;
    ctx.save();
    ctx.globalAlpha = command.opacity ?? 1;
    if (shadowColor && shadowAlpha > 0 && (shadowX !== 0 || shadowY !== 0)) {
      ctx.fillStyle = colorWithAlpha(shadowColor, shadowAlpha);
      fillBox(ctx, command.cssX + shadowX, command.cssY + shadowY, command.cssWidth, command.cssHeight, radius);
    }
    ctx.fillStyle = colorWithAlpha(command.fillColor ?? "#000000", command.fillAlpha ?? 255);
    fillBox(ctx, command.cssX, command.cssY, command.cssWidth, command.cssHeight, radius);
    if (borderWidth > 0 && borderColor) {
      ctx.lineWidth = borderWidth;
      ctx.strokeStyle = colorWithAlpha(borderColor, borderOpacity);
      strokeBox(ctx, command.cssX + borderWidth / 2, command.cssY + borderWidth / 2, Math.max(0, command.cssWidth - borderWidth), Math.max(0, command.cssHeight - borderWidth), Math.max(0, radius - borderWidth / 2));
    }
    ctx.restore();
  }

  function drawImage(ctx, command, frame) {
    const path = normalizeAssetUrl(command.imagePath);
    if (!path) return;
    let image = images.get(path);
    if (!image) {
      image = new Image();
      image.decoding = "async";
      image.onload = () => {
        if (lastFrame && lastCanvas) renderFrame(lastCanvas, lastFrame);
      };
      image.src = path;
      images.set(path, image);
    }
    if (!image.complete || image.naturalWidth <= 0) return;
    const useNaturalSize = command.useNaturalImageSize === true ||
      command.UseNaturalImageSize === true ||
      command.cssWidth <= 0 ||
      command.cssHeight <= 0;
    const scale = command.logicalWidth > 0 && command.cssWidth > 0
      ? command.cssWidth / command.logicalWidth
      : measure(lastCanvas).width / Math.max(1, frame.logicalWidth || 1280);
    const width = useNaturalSize ? image.naturalWidth * scale : command.cssWidth;
    const height = useNaturalSize ? image.naturalHeight * scale : command.cssHeight;
    ctx.save();
    ctx.globalAlpha = command.opacity ?? 1;
    ctx.drawImage(image, command.cssX, command.cssY, width, height);
    ctx.restore();
  }

  function drawText(ctx, command, frame) {
    const scale = command.logicalHeight > 0
      ? command.cssHeight / command.logicalHeight
      : command.logicalWidth > 0
        ? command.cssWidth / command.logicalWidth
        : measure(lastCanvas).width / Math.max(1, frame.logicalWidth || 1280);
    const fontSize = Math.max(8, Math.round((command.fontSize || 26) * scale));
    ctx.save();
    ctx.globalAlpha = command.opacity ?? 1;
    ctx.font = `${fontSize}px AriaRuntime, sans-serif`;
    ctx.textAlign = textAlign(command.textAlign);
    ctx.textBaseline = "top";
    const x = alignedX(command);
    const y = alignedY(command, fontSize);
    const shadowColor = command.textShadowColor || command.TextShadowColor || "";
    const shadowX = (command.textShadowX ?? command.TextShadowX ?? 0) * scale;
    const shadowY = (command.textShadowY ?? command.TextShadowY ?? 0) * scale;
    if (shadowColor && (shadowX !== 0 || shadowY !== 0)) {
      ctx.fillStyle = shadowColor;
      wrapText(ctx, command.text || "", x + shadowX, y + shadowY, command.cssWidth || 960, fontSize * 1.32, command.textAlign);
    }
    ctx.fillStyle = command.color || "#ffffff";
    wrapText(ctx, command.text || "", x, y, command.cssWidth || 960, fontSize * 1.32, command.textAlign);
    ctx.restore();
  }

  function drawTriangle(ctx, command) {
    ctx.save();
    ctx.globalAlpha = command.opacity ?? 1;
    ctx.fillStyle = colorWithAlpha(command.fillColor ?? "#cdcdcf", command.fillAlpha ?? 255);
    ctx.beginPath();
    ctx.moveTo(command.cssX, command.cssY);
    ctx.lineTo(command.cssX + command.cssWidth, command.cssY);
    ctx.lineTo(command.cssX + command.cssWidth * 0.5, command.cssY + command.cssHeight);
    ctx.closePath();
    ctx.fill();
    const borderWidth = command.borderWidth ?? command.BorderWidth ?? 0;
    const borderColor = command.borderColor || command.BorderColor || "";
    if (borderWidth > 0 && borderColor) {
      ctx.lineWidth = borderWidth;
      ctx.strokeStyle = colorWithAlpha(borderColor, command.borderOpacity ?? command.BorderOpacity ?? 255);
      ctx.stroke();
    }
    ctx.restore();
  }

  function commandScale(command) {
    if (command.logicalWidth > 0 && command.cssWidth > 0) return command.cssWidth / command.logicalWidth;
    if (command.logicalHeight > 0 && command.cssHeight > 0) return command.cssHeight / command.logicalHeight;
    return measure(lastCanvas).width / 1280;
  }

  function fillBox(ctx, x, y, width, height, radius) {
    if (radius <= 0) {
      ctx.fillRect(x, y, width, height);
      return;
    }
    roundedBoxPath(ctx, x, y, width, height, radius);
    ctx.fill();
  }

  function strokeBox(ctx, x, y, width, height, radius) {
    if (radius <= 0) {
      ctx.strokeRect(x, y, width, height);
      return;
    }
    roundedBoxPath(ctx, x, y, width, height, radius);
    ctx.stroke();
  }

  function roundedBoxPath(ctx, x, y, width, height, radius) {
    const r = Math.min(radius, width / 2, height / 2);
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.lineTo(x + width - r, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + r);
    ctx.lineTo(x + width, y + height - r);
    ctx.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
    ctx.lineTo(x + r, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - r);
    ctx.lineTo(x, y + r);
    ctx.quadraticCurveTo(x, y, x + r, y);
    ctx.closePath();
  }

  function alignedX(command) {
    if (command.textAlign === "center") return command.cssX + command.cssWidth / 2;
    if (command.textAlign === "right") return command.cssX + command.cssWidth;
    return command.cssX;
  }

  function alignedY(command, fontSize) {
    if (command.textVAlign === "center") return command.cssY + Math.max(0, (command.cssHeight - fontSize * 1.32) / 2);
    if (command.textVAlign === "bottom") return command.cssY + Math.max(0, command.cssHeight - fontSize * 1.32);
    return command.cssY;
  }

  function textAlign(value) {
    if (value === "center" || value === "right") return value;
    return "left";
  }

  function wrapText(ctx, text, x, y, maxWidth, lineHeight, align = "left") {
    const lines = String(text).split("\n");
    for (const line of lines) {
      let current = "";
      for (const token of Array.from(line)) {
        const test = current + token;
        if (current && ctx.measureText(test).width > maxWidth) {
          ctx.fillText(current, x, y);
          y += lineHeight;
          current = token;
        } else {
          current = test;
        }
      }
      ctx.fillText(current, x, y);
      y += lineHeight;
    }
  }

  function colorWithAlpha(hex, alpha) {
    if (!hex || !hex.startsWith("#")) return hex || "#000000";
    const value = hex.slice(1);
    const r = parseInt(value.slice(0, 2), 16) || 0;
    const g = parseInt(value.slice(2, 4), 16) || 0;
    const b = parseInt(value.slice(4, 6), 16) || 0;
    return `rgba(${r}, ${g}, ${b}, ${(alpha ?? 255) / 255})`;
  }

  function normalizeAssetUrl(path) {
    return String(path || "").replace(/\\/g, "/").replace(/^\/+/, "");
  }

  function openDb(databaseName = DB_NAME) {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(databaseName || DB_NAME, DB_VERSION);
      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains("saves")) db.createObjectStore("saves");
        if (!db.objectStoreNames.contains("settings")) db.createObjectStore("settings");
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  async function applyStorageOperation(operation) {
    const area = readProperty(operation, "area");
    if (area !== 0 && area !== "IndexedDb") return null;

    const kind = readProperty(operation, "kind");
    const storeName = readProperty(operation, "storeName") || "saves";
    const key = readProperty(operation, "key");
    const payload = readProperty(operation, "payload") || "";
    const databaseName = readProperty(operation, "databaseName") || DB_NAME;
    const db = await openDb(databaseName);

    if (kind === 1 || kind === "Write") {
      return writeStoreValue(db, storeName, key, payload, operation);
    }

    if (kind === 0 || kind === "Read") {
      return readStoreValue(db, storeName, key, operation);
    }

    return null;
  }

  function writeStoreValue(db, storeName, key, payload, operation) {
    return new Promise((resolve, reject) => {
      const tx = db.transaction(storeName, "readwrite");
      tx.objectStore(storeName).put(payload, key);
      tx.oncomplete = () => {
        window.__ariaWebLastStorageOperation = { operation, stored: true };
        resolve(payload);
      };
      tx.onerror = () => reject(tx.error);
    });
  }

  function readStoreValue(db, storeName, key, operation) {
    return new Promise((resolve, reject) => {
      const tx = db.transaction(storeName, "readonly");
      const request = tx.objectStore(storeName).get(key);
      request.onsuccess = () => {
        window.__ariaWebLastStorageOperation = { operation, loaded: request.result != null };
        resolve(request.result ?? null);
      };
      request.onerror = () => reject(request.error);
    });
  }

  function readProperty(value, name) {
    if (!value) return undefined;
    const pascal = `${name[0].toUpperCase()}${name.slice(1)}`;
    return value[name] ?? value[pascal];
  }

  async function saveSlot(slot, payload = "{}") {
    const db = await openDb();
    return new Promise((resolve, reject) => {
      const tx = db.transaction("saves", "readwrite");
      tx.objectStore("saves").put(payload, `save:${String(slot).padStart(3, "0")}`);
      tx.oncomplete = () => {
        window.__ariaWebLastSave = { slot, saved: true };
        resolve(true);
      };
      tx.onerror = () => reject(tx.error);
    });
  }

  async function loadSlot(slot) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
      const tx = db.transaction("saves", "readonly");
      const request = tx.objectStore("saves").get(`save:${String(slot).padStart(3, "0")}`);
      request.onsuccess = () => {
        window.__ariaWebLastLoad = { slot, loaded: request.result != null };
        resolve(request.result ?? null);
      };
      request.onerror = () => reject(request.error);
    });
  }

  return { boot, measure, renderFrame, showError, saveSlot, loadSlot, applyStorageOperation };
})();
