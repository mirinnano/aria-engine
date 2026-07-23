import {
  assertViewModel,
  routeName,
  type AriaStepOutput,
  type UiIntent,
  type UiViewModel,
} from "@aria/ui-sdk";

type WasmRuntime = {
  step(input: string): string;
  save_envelope_json(timestamp: bigint): string;
  restore_envelope_json(envelope: string): void;
};

type WasmRuntimeConstructor = new (
  bytecode: Uint8Array,
  logicalWidth: number,
  logicalHeight: number,
) => WasmRuntime;

type WasmPak = {
  read(path: string): Uint8Array;
  game_id(): string;
  content_root_blake3(): string;
};

type WasmPakConstructor = new (bytes: Uint8Array) => WasmPak;

type WasmModule = {
  default: () => Promise<void>;
  WebRuntime: WasmRuntimeConstructor;
  WebPak: WasmPakConstructor;
};

type SceneRenderer = {
  backend: string;
  submit(frame: unknown): Promise<void>;
};

type RendererModule = {
  createWebRenderer: (
    target: HTMLCanvasElement,
    asset: (path: string) => Uint8Array,
    options: { onStatus(message: string): void; fontFamilies: string[] },
  ) => Promise<SceneRenderer>;
};

type AudioAdapter = {
  installUnlock(target: EventTarget): void;
  consume(commands: unknown[]): Promise<void>;
  stopAll(): void;
};

type SaveGeneration = { generation: number; payload: string; writtenAt?: number };
type SaveStore = {
  open(): Promise<void>;
  put(namespace: string, slot: number, payload: string): Promise<number>;
  latest(namespace: string, slot: number): Promise<SaveGeneration | null>;
  generations(namespace: string, slot: number): Promise<SaveGeneration[]>;
};

type BundlePak = {
  pack_id: string;
  role: "boot" | "hot" | "cold" | "overlay";
  file: string;
  blake3: string;
  size: number;
  content_root_blake3: string;
  assets: string[];
};

/** Presentation-only metadata safely derived from a save envelope. The VM
 * remains the authority for loading; this is only enough context to make the
 * record table readable without screenshots or renderer-dependent previews. */
export type SaveSlotSummary = {
  slot: number;
  generation: number;
  timestampMs: number | null;
  speaker: string | null;
  excerpt: string | null;
};

function isTauri(): boolean {
  return "__TAURI_INTERNALS__" in window;
}

async function createSaveStore(
  namespace: string,
  BrowserSaveStore: new (name: string, generations: number) => SaveStore,
): Promise<SaveStore> {
  if (!isTauri()) {
    const store = new BrowserSaveStore(`aria-v3-${namespace}`, 3);
    await store.open();
    return store;
  }
  const { invoke } = await import("@tauri-apps/api/core");
  return {
    async open() {},
    async put(saveNamespace, slot, payload) {
      return invoke<number>("save_generation", { namespace: saveNamespace, slot, payload });
    },
    async latest(saveNamespace, slot) {
      return invoke<SaveGeneration | null>("load_latest_generation", {
        namespace: saveNamespace,
        slot,
      });
    },
    async generations(saveNamespace, slot) {
      return invoke<SaveGeneration[]>("load_generations", { namespace: saveNamespace, slot });
    },
  };
}

type Bundle = {
  schema_version: number;
  vm_abi_version: number;
  game_id: string;
  game_title: string;
  logical_width: number;
  logical_height: number;
  save_namespace: string;
  font_assets: string[];
  pak_blake3: string;
  pak_size: number;
  pack_id: string;
  pak_packs?: BundlePak[];
  pak_profile: "dev" | "signed" | "protected";
  pak_content_root_blake3: string;
};

type RuntimeHooks = {
  onOutput(output: AriaStepOutput): void;
  onStatus(message: string): void;
  onError(error: Error): void;
  onSaveSlots?(slots: SaveSlotSummary[]): void;
};

export type PresentationRuntime = {
  intent(intent: UiIntent): void;
  dispose(): void;
  rendererBackend(): string | null;
};

function url(path: string): string {
  return new URL(path, document.baseURI).href;
}

async function importExternal<T>(path: string): Promise<T> {
  return import(/* @vite-ignore */ url(path)) as Promise<T>;
}

function renderScale(): number {
  // WebKit GTK becomes fill-rate bound surprisingly quickly on HiDPI screens.
  // Scene art remains crisp at 1.5x while reserving the browser's compositor
  // budget for the text that the player is actively reading.
  const cap = isTauri() ? 1.5 : 2;
  return Math.min(cap, Math.max(1, window.devicePixelRatio || 1));
}

function viewportFor(canvas: HTMLCanvasElement) {
  const bounds = canvas.getBoundingClientRect();
  // The umikaze presentation owns its visible stage with still photographs.
  // Its compatibility canvas is intentionally not painted, so it has no CSS
  // box from which to derive a viewport. Input/replay coordinates must still
  // describe the actual WebView, not a 0×0 hidden element.
  const cssWidth = bounds.width || window.innerWidth || document.documentElement.clientWidth || 1;
  const cssHeight = bounds.height || window.innerHeight || document.documentElement.clientHeight || 1;
  const scaleFactor = renderScale();
  return {
    width: Math.max(1, Math.round(cssWidth * scaleFactor)),
    height: Math.max(1, Math.round(cssHeight * scaleFactor)),
    scale_factor: scaleFactor,
    safe_area: { top: 0, right: 0, bottom: 0, left: 0 },
  };
}

function resizeCanvas(canvas: HTMLCanvasElement) {
  const viewport = viewportFor(canvas);
  canvas.width = viewport.width;
  canvas.height = viewport.height;
  return viewport;
}

function sceneFingerprint(scene: AriaStepOutput["scene"]): string {
  const value = scene as unknown as Record<string, unknown>;
  // `frame_number` advances even when a background is perfectly still. It is
  // intentionally excluded so a quiet scene keeps its already-presented GPU
  // surface instead of re-uploading identical draw plans sixty times a second.
  return JSON.stringify({
    logical_size: scene.logical_size,
    clear_color: value.clear_color,
    commands: scene.commands,
    // Transitions and effects are finite animation state. They must remain in
    // the fingerprint so the initial fade cannot leave a perfectly valid
    // scene hidden behind its first, opaque transition frame. Once either
    // settles or disappears, the fingerprint stabilizes and redraws stop.
    transition: value.transition,
    effects: value.effects,
  });
}

function viewFingerprint(view: UiViewModel): string {
  // Do not stringify the full semantic model at 60 Hz. In particular, a
  // long history belongs to the backlog route only; the reading loop needs a
  // compact signature plus the current subtitle page, not every past page.
  const dialogue = view.dialogue;
  return [
    routeName(view.route),
    view.game.locale,
    dialogue?.page_id ?? "",
    dialogue?.page_number ?? "",
    dialogue?.page_count ?? "",
    dialogue?.text ?? "",
    dialogue?.complete ? "1" : "0",
    dialogue?.awaiting_advance ? "1" : "0",
    view.auto_mode,
    view.skip_mode,
    view.gallery_viewer ?? "",
    view.confirmation?.action ?? "",
    view.confirmation?.resume_id ?? "",
    view.backlog_total,
    view.backlog_start,
    view.gallery.map((item) => `${item.id}:${Number(item.unlocked)}:${Number(item.selected)}`).join(","),
    view.settings.text_speed_ms,
    view.settings.auto_delay_ms,
    view.settings.bgm_volume,
    view.settings.sound_effect_volume,
    view.settings.voice_volume,
    Number(view.settings.fullscreen),
    view.settings.text_scale,
    Number(view.settings.high_contrast),
    Number(view.settings.reduced_motion),
    Number(view.settings.skip_unread),
    view.choices.map((choice) => `${choice.id}:${Number(choice.selected)}`).join(","),
    view.actions.map((action) => `${action.id}:${Number(action.enabled)}:${Number(action.active)}`).join(","),
  ].join("|");
}

function saveSummary(slot: number, generation: SaveGeneration): SaveSlotSummary {
  // A malformed or old envelope remains loadable through the runtime's
  // validation/recovery path. It must still have a visible place in the
  // record table rather than being mistaken for an empty slot.
  let timestampMs = generation.writtenAt ?? null;
  let speaker: string | null = null;
  let excerpt: string | null = null;
  try {
    const envelope = JSON.parse(generation.payload) as {
      timestamp_unix_ms?: unknown;
      payload?: { backlog?: Array<{ speaker?: unknown; text?: unknown }> };
    };
    if (typeof envelope.timestamp_unix_ms === "number" && Number.isFinite(envelope.timestamp_unix_ms)) {
      timestampMs = Number(envelope.timestamp_unix_ms);
    }
    const entry = envelope.payload?.backlog?.at(-1);
    if (typeof entry?.speaker === "string" && entry.speaker.trim()) speaker = entry.speaker;
    if (typeof entry?.text === "string" && entry.text.trim()) {
      const normalized = entry.text.replace(/\s+/g, " ").trim();
      excerpt = normalized.length > 74 ? `${normalized.slice(0, 73)}…` : normalized;
    }
  } catch {
    // The load path will inspect each generation with the WASM validator.
  }
  return { slot, generation: generation.generation, timestampMs, speaker, excerpt };
}

type BundledFonts = {
  families: string[];
  ensure(index: number): Promise<void>;
};

async function loadBundledFonts(
  reader: { read(path: string): Uint8Array },
  fontAssets: string[],
  eagerCount: number,
): Promise<BundledFonts> {
  if (fontAssets.length === 0) return { families: [], async ensure() {} };
  if (!document.fonts || typeof FontFace !== "function") {
    throw new Error("This browser cannot load the bundled font contract");
  }
  const families = fontAssets.map((_, index) => `AriaBundledFont${index}`);
  const loads = new Map<number, Promise<void>>();
  const ensure = async (index: number) => {
    if (index < 0 || index >= fontAssets.length) return;
    let loading = loads.get(index);
    if (!loading) {
      const asset = fontAssets[index];
      const family = families[index];
      loading = (async () => {
        const bytes = reader.read(asset);
        if (!(bytes instanceof Uint8Array) || bytes.byteLength === 0) {
          throw new Error(`Bundled font '${asset}' is unavailable`);
        }
        // Copy out of WASM memory before FontFace retains it. `new Uint8Array(n)`
        // is backed by an ordinary ArrayBuffer even when wasm-bindgen's view is
        // typed as ArrayBufferLike by modern TypeScript.
        const copied = new Uint8Array(bytes.byteLength);
        copied.set(bytes);
        const face = new FontFace(family, copied.buffer, { display: "block" });
        await face.load();
        document.fonts.add(face);
        await document.fonts.ready;
      })();
      loads.set(index, loading);
    }
    await loading;
  };
  await Promise.all(Array.from({ length: Math.min(eagerCount, fontAssets.length) }, (_, index) => ensure(index)));
  return { families, ensure };
}

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) return false;
  return Boolean(target.closest("input, textarea, select, [contenteditable=true]"));
}

function focusRelative(direction: -1 | 1) {
  const candidates = [...document.querySelectorAll<HTMLElement>("[data-aria-focusable]")]
    .filter((element) => !element.matches(":disabled") && element.offsetParent !== null);
  if (!candidates.length) return;
  const current = candidates.indexOf(document.activeElement as HTMLElement);
  const next = current < 0
    ? (direction > 0 ? 0 : candidates.length - 1)
    : (current + direction + candidates.length) % candidates.length;
  candidates[next]?.focus({ preventScroll: true });
}

function isPressed(button: GamepadButton | undefined): boolean {
  return Boolean(button && (button.pressed || button.value >= 0.5));
}

/**
 * Boots the one shared WASM runtime. The canvas is strictly scene-only;
 * React receives the adjacent semantic `view` object and owns every control.
 */
export async function bootPresentation(
  canvas: HTMLCanvasElement,
  hooks: RuntimeHooks,
): Promise<PresentationRuntime> {
  hooks.onStatus("Opening the record…");
  let viewport = resizeCanvas(canvas);

  // The semantic scene remains part of the runtime contract, but umikaze
  // deliberately renders its stage through the photograph component in
  // App.tsx. Creating an invisible WebGL/WebGPU canvas in a WebKit WebView
  // consumed compositor time and battery without changing a single pixel.
  // Other presentations can omit this marker and retain the full renderer.
  const presentationOwnsStage = canvas.dataset.ariaStage === "dom";
  const rendererImport: Promise<RendererModule | null> = presentationOwnsStage
    ? Promise.resolve(null)
    : importExternal<RendererModule>("./web-renderer.js");

  const [wasm, rendererModule, audioModule, saveModule, bundleResponse, bytecodeResponse] =
    await Promise.all([
      importExternal<WasmModule>("./pkg/aria_web.js"),
      rendererImport,
      importExternal<{ WebAudioAdapter: new (asset: (path: string) => Uint8Array) => AudioAdapter }>("./web-audio.js"),
      importExternal<{ IndexedDbSaveStore: new (name: string, generations: number) => SaveStore }>("./save-store.js"),
      fetch(url("./bundle.aria.json")),
      fetch(url("./game.ariac")),
    ]);
  await wasm.default();
  if (!bundleResponse.ok || !bytecodeResponse.ok) {
    throw new Error("The game bundle is incomplete; build the presentation through aria build.");
  }
  const bundle = await bundleResponse.json() as Bundle;
  if (bundle.schema_version !== 5 || bundle.vm_abi_version !== 4) {
    throw new Error("This presentation does not support the bundled Aria runtime.");
  }
  if (bundle.pak_profile !== "dev") {
    throw new Error("This presentation needs a host pak-key provider for signed/protected packages.");
  }

  const bytecode = new Uint8Array(await bytecodeResponse.arrayBuffer());
  // The title and settings screens are DOM-owned and do not need any asset
  // package. Packs are mounted independently, so a first subtitle only pays
  // for the hot reader pack and a later scene/audio request can mount boot or
  // cold content without rebuilding an already validated archive.
  const packs: BundlePak[] = bundle.pak_packs?.length
    ? bundle.pak_packs
    : [{
      pack_id: bundle.pack_id,
      role: "boot",
      file: "game.ariapak",
      blake3: bundle.pak_blake3,
      size: bundle.pak_size,
      content_root_blake3: bundle.pak_content_root_blake3,
      assets: bundle.font_assets,
    }];
  const loadedPaks = new Map<string, WasmPak>();
  const packPromises = new Map<string, Promise<WasmPak>>();
  const assetPack = new Map<string, BundlePak>();
  for (const pack of packs) {
    for (const asset of pack.assets) {
      if (!assetPack.has(asset)) assetPack.set(asset, pack);
    }
  }
  const ensurePack = async (pack: BundlePak): Promise<WasmPak> => {
    const loaded = loadedPaks.get(pack.pack_id);
    if (loaded) return loaded;
    let loading = packPromises.get(pack.pack_id);
    if (!loading) {
      loading = (async () => {
        const response = await fetch(url(`./${pack.file}`));
        if (!response.ok) throw new Error(`${pack.file}: HTTP ${response.status}`);
        const bytes = new Uint8Array(await response.arrayBuffer());
        if (bytes.byteLength !== pack.size) {
          throw new Error(`${pack.file}: size does not match bundle.aria.json`);
        }
        const archive = new wasm.WebPak(bytes);
        if (archive.game_id() !== bundle.game_id
          || archive.content_root_blake3() !== pack.content_root_blake3) {
          throw new Error(`${pack.file}: metadata does not match bundle.aria.json`);
        }
        loadedPaks.set(pack.pack_id, archive);
        return archive;
      })().catch((error) => {
        packPromises.delete(pack.pack_id);
        throw error;
      });
      packPromises.set(pack.pack_id, loading);
    }
    return loading;
  };
  const ensurePackForAsset = async (asset: string) => {
    const pack = assetPack.get(asset);
    if (pack) return ensurePack(pack);
    // A legacy bundle may not include per-pack asset indexes. It has one
    // primary pack, while a hand-authored overlay can still be discovered by
    // probing every already-declared pack in deterministic order.
    await Promise.all(packs.map((candidate) => ensurePack(candidate)));
    return loadedPaks.values().next().value as WasmPak;
  };
  const ensureAllPaks = async () => {
    await Promise.all(packs.map((pack) => ensurePack(pack)));
  };
  const readAsset = (path: string): Uint8Array => {
    const preferred = assetPack.get(path);
    const candidates = preferred
      ? [preferred, ...packs.filter((pack) => pack.pack_id !== preferred.pack_id)]
      : packs;
    let lastError: unknown = null;
    for (const pack of candidates) {
      const archive = loadedPaks.get(pack.pack_id);
      if (!archive) continue;
      try {
        return archive.read(path);
      } catch (error) {
        lastError = error;
      }
    }
    throw lastError instanceof Error
      ? lastError
      : new Error(`asset '${path}' is not mounted in the declared PAK set`);
  };
  // First light deliberately paints with platform faces. Loading a full CJK
  // face just to show a still title retained tens of MiB in WebKit; the
  // readable Noto face is loaded only when the first subtitle is reached.
  // The font contract remains bundled for offline use.
  let bundledFontsPromise: Promise<BundledFonts> | null = null;
  const ensureBundledFonts = (eagerCount: number): Promise<BundledFonts> => {
    bundledFontsPromise ??= (async () => {
      await Promise.all(bundle.font_assets
        .slice(0, eagerCount)
        .map((asset) => ensurePackForAsset(asset)));
      const loaded = await loadBundledFonts(
        { read: readAsset },
        bundle.font_assets,
        eagerCount,
      );
      return {
        families: loaded.families,
        ensure: async (index: number) => {
          await ensurePackForAsset(bundle.font_assets[index] || "");
          await loaded.ensure(index);
        },
      };
    })();
    return bundledFontsPromise;
  };
  if (!presentationOwnsStage) await ensureAllPaks();
  const bundledFonts = await ensureBundledFonts(presentationOwnsStage ? 0 : bundle.font_assets.length);
  const fonts = bundledFonts.families;
  const sceneRendererEnabled = rendererModule !== null;
  const renderer: SceneRenderer = rendererModule
    ? await rendererModule.createWebRenderer(canvas, readAsset, {
      onStatus: () => {},
      fontFamilies: fonts,
    })
    : {
      backend: "dom-stage",
      async submit() {},
    };
  const audio = new audioModule.WebAudioAdapter(readAsset);
  audio.installUnlock(document);
  const saves = await createSaveStore(bundle.save_namespace, saveModule.IndexedDbSaveStore);

  const refreshSaveSlots = async () => {
    const records = await Promise.all(Array.from({ length: 10 }, async (_, index) => {
      const slot = index + 1;
      const generation = await saves.latest(bundle.save_namespace, slot);
      return generation ? saveSummary(slot, generation) : null;
    }));
    hooks.onSaveSlots?.(records.filter((record): record is SaveSlotSummary => record !== null));
  };
  // Loading the table is independent from the first VM step. A storage issue
  // should not turn the title screen into a startup failure; it merely leaves
  // the archive empty until the next successful save refreshes it.
  void refreshSaveSlots().catch((cause) => {
    console.warn("Unable to enumerate save records", cause);
  });

  let runtime = new wasm.WebRuntime(bytecode, bundle.logical_width, bundle.logical_height);
  let disposed = false;
  let frameRequest = 0;
  let timerRequest: number | null = null;
  let ticking = false;
  let wakeRequested = false;
  let sequence = 0;
  let previous = performance.now();
  let queued: UiIntent[] = [];
  let activeOutput: AriaStepOutput | null = null;
  let lastSceneFingerprint = "";
  let lastViewFingerprint = "";
  let lastViewPublish = 0;
  let sceneNeedsDraw = sceneRendererEnabled;
  let readingFaceReady = !presentationOwnsStage || bundle.font_assets.length === 0;

  const connectedGamepads = () => [...(navigator.getGamepads?.() || [])]
    .filter((pad): pad is Gamepad => Boolean(pad?.connected));
  const hasConnectedGamepad = () => connectedGamepads().length > 0;
  const sceneIsAnimating = (scene: AriaStepOutput["scene"]) => {
    const value = scene as unknown as Record<string, unknown>;
    const transition = value.transition as { progress?: unknown } | null | undefined;
    if (transition && Number(transition.progress) < 1) return true;
    const effects = value.effects;
    return Array.isArray(effects) && effects.some((effect) => {
      if (!effect || typeof effect !== "object") return false;
      return Number((effect as { progress?: unknown }).progress) < 1;
    });
  };
  const nextTickDelay = (output: AriaStepOutput): number | null => {
    // Transitions and visible typewriting need display cadence.  A 32 ms
    // typewriter tick made the record feel as though the whole native window
    // was running at 30 fps, even though its static scene was idle.  Auto/skip
    // remain paced lower; a waiting screen stays completely idle until an
    // input, resize, or gamepad event wakes it.
    if (sceneIsAnimating(output.scene)) return 16;
    if (output.view.dialogue && !output.view.dialogue.complete) return 16;
    if (output.view.actions.some((action) => (
      action.active && (action.id === "menu.auto" || action.id === "menu.skip")
    ))) return 32;
    if (hasConnectedGamepad()) return 33;
    return null;
  };
  const clearScheduledTick = () => {
    if (frameRequest) {
      cancelAnimationFrame(frameRequest);
      frameRequest = 0;
    }
    if (timerRequest !== null) {
      window.clearTimeout(timerRequest);
      timerRequest = null;
    }
  };
  const scheduleTick = (delay = 0) => {
    if (disposed || ticking || frameRequest || timerRequest !== null) return;
    if (delay <= 16) {
      frameRequest = requestAnimationFrame((now) => {
        frameRequest = 0;
        void tick(now);
      });
      return;
    }
    timerRequest = window.setTimeout(() => {
      timerRequest = null;
      void tick(performance.now());
    }, delay);
  };
  const wake = () => {
    if (disposed) return;
    if (ticking) {
      wakeRequested = true;
      return;
    }
    if (timerRequest !== null) {
      window.clearTimeout(timerRequest);
      timerRequest = null;
    }
    scheduleTick();
  };
  const resize = () => {
    viewport = resizeCanvas(canvas);
    if (sceneRendererEnabled) sceneNeedsDraw = true;
    wake();
  };
  window.addEventListener("resize", resize);
  const onGamepadConnection = () => wake();
  window.addEventListener("gamepadconnected", onGamepadConnection);
  window.addEventListener("gamepaddisconnected", onGamepadConnection);

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.defaultPrevented || isEditableTarget(event.target)) return;
    const view = activeOutput?.view;
    const activeRoute = view ? routeName(view.route) : "";
    const readingRoute = activeRoute === "dialogue"
      || (activeRoute === "chapter_select" && Boolean(view?.dialogue) && view?.choices.length === 0);
    const galleryViewer = activeRoute === "gallery" && Boolean(view?.gallery_viewer);
    const visibleChromeControl = event.target instanceof Element
      && Boolean(event.target.closest(".quiet-chrome.is-visible [data-aria-action]"));
    const command = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && (command === "a" || command === "c" || command === "x")) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    if (
      (event.key === "Enter" || (event.key === " " && !visibleChromeControl))
      && readingRoute
    ) {
      // A click can leave focus on a now-hidden chrome button. Own the
      // advance key at capture phase so Enter never accidentally activates
      // that stale DOM control instead of turning the next line. Space still
      // activates a deliberately focused visible chrome control, preserving
      // an accessible keyboard route to the top-edge tools.
      event.preventDefault();
      event.stopPropagation();
      if (!event.repeat) {
        queued.push({ kind: "activate", id: "dialogue.advance" });
        wake();
      }
      return;
    }
    if (command === "h" && readingRoute) {
      event.preventDefault();
      event.stopPropagation();
      if (!event.repeat) {
        queued.push({ kind: "activate", id: "chrome.backlog" });
        wake();
      }
      return;
    }
    if (galleryViewer && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
      event.preventDefault();
      event.stopPropagation();
      if (!event.repeat) {
        queued.push({ kind: "activate", id: event.key === "ArrowLeft" ? "gallery.previous" : "gallery.next" });
        wake();
      }
      return;
    }
    if (event.key === "Escape") {
      // React Aria owns Escape while a modal is open. Handling it here as
      // well used to enqueue a second dismiss, which could reopen a sheet or
      // strand focus on the body. Outside a dialog Escape is the same menu
      // affordance as the old engine's right click.
      if (document.querySelector('[role="dialog"]') || !readingRoute) return;
      event.preventDefault();
      queued.push({ kind: "activate", id: "chrome.menu" });
      wake();
    }
  };
  const preventTextExtraction = (event: Event) => {
    if (!isEditableTarget(event.target)) event.preventDefault();
  };
  window.addEventListener("keydown", onKeyDown, true);
  document.addEventListener("copy", preventTextExtraction, true);
  document.addEventListener("cut", preventTextExtraction, true);
  document.addEventListener("dragstart", preventTextExtraction, true);
  document.addEventListener("selectstart", preventTextExtraction, true);

  const previousPadPresses = new Set<string>();
  const pollGamepad = () => {
    const galleryViewerActive = () => {
      const view = activeOutput?.view;
      return Boolean(view && routeName(view.route) === "gallery" && view.gallery_viewer);
    };
    for (const pad of connectedGamepads()) {
      const commands: Array<[number, () => void]> = [
        [0, () => queued.push({ kind: "activate", id: "dialogue.advance" })],
        [1, () => queued.push({ kind: "dismiss" })],
        [3, () => queued.push({ kind: "activate", id: "chrome.backlog" })],
        [9, () => queued.push({ kind: "activate", id: "chrome.menu" })],
        [12, () => {
          if (galleryViewerActive()) {
            queued.push({ kind: "activate", id: "gallery.previous" });
          } else {
            focusRelative(-1);
          }
        }],
        [13, () => {
          if (galleryViewerActive()) {
            queued.push({ kind: "activate", id: "gallery.next" });
          } else {
            focusRelative(1);
          }
        }],
      ];
      for (const [index, execute] of commands) {
        const key = `${pad.index}:${index}`;
        const pressed = isPressed(pad.buttons[index]);
        if (pressed && !previousPadPresses.has(key)) execute();
        if (pressed) previousPadPresses.add(key);
        else previousPadPresses.delete(key);
      }
    }
  };

  const saveOrLoad = async (output: AriaStepOutput): Promise<boolean> => {
    let restored = false;
    for (const command of output.runtime) {
      if (command.kind === "save" && typeof command.slot === "number") {
        const envelope = runtime.save_envelope_json(BigInt(Date.now()));
        await saves.put(bundle.save_namespace, command.slot, envelope);
        await refreshSaveSlots();
      }
      if (command.kind === "load" && typeof command.slot === "number") {
        const generations = await saves.generations(bundle.save_namespace, command.slot);
        for (const generation of generations) {
          try {
            const probe = new wasm.WebRuntime(bytecode, bundle.logical_width, bundle.logical_height);
            probe.restore_envelope_json(generation.payload);
            audio.stopAll();
            runtime.restore_envelope_json(generation.payload);
            restored = true;
            break;
          } catch (error) {
            console.warn("Skipping invalid save generation", generation.generation, error);
          }
        }
      }
      if (command.kind === "return_to_title") window.location.reload();
      if (command.kind === "quit" && isTauri()) {
        const { getCurrentWindow } = await import("@tauri-apps/api/window");
        await getCurrentWindow().close();
      }
    }
    return restored;
  };

  const tick = async (now: number) => {
    if (disposed || ticking) return;
    ticking = true;
    let nextDelay: number | null = null;
    try {
      const delta = Math.min(250, Math.max(0, Math.round(now - previous)));
      previous = now;
      pollGamepad();
      const intents = queued;
      queued = [];
      const input = {
        sequence: ++sequence,
        delta_ms: delta,
        pressed: [],
        held: [],
        pointer: null,
        scroll_delta_y: 0,
        viewport,
        intents,
      };
      const output = JSON.parse(runtime.step(JSON.stringify(input))) as AriaStepOutput;
      assertViewModel(output.view);
      activeOutput = output;
      if (!readingFaceReady && output.view.dialogue) {
        await bundledFonts.ensure(0);
        readingFaceReady = true;
      }
      if (output.audio.length > 0) {
        await ensureAllPaks();
        await audio.consume(output.audio);
      }

      if (sceneRendererEnabled) {
        const nextSceneFingerprint = sceneFingerprint(output.scene);
        if (sceneNeedsDraw || nextSceneFingerprint !== lastSceneFingerprint) {
          await renderer.submit(output.scene);
          lastSceneFingerprint = nextSceneFingerprint;
          sceneNeedsDraw = false;
        }
      }

      const nextViewFingerprint = viewFingerprint(output.view);
      const isTyping = output.view.dialogue !== null && !output.view.dialogue.complete;
      // The VM is already driven by rAF while visible text is arriving. Do
      // not turn that 60 Hz clock back into an apparent 30 Hz typewriter by
      // publishing React state only every 32 ms. The no-op scene renderer
      // above leaves enough headroom for the reading surface to stay smooth.
      const publishInterval = isTyping ? 16 : 0;
      if (
        nextViewFingerprint !== lastViewFingerprint
        && (
          lastViewFingerprint.length === 0
          || !isTyping
          || now - lastViewPublish >= publishInterval
          || output.halted
        )
      ) {
        hooks.onOutput(output);
        lastViewFingerprint = nextViewFingerprint;
        lastViewPublish = now;
      }
      if (await saveOrLoad(output)) {
        // Restoring replaces the WASM VM after this frame was already
        // published. Wake a clean step so React immediately receives the
        // restored route instead of remaining on the load sheet until a
        // separate input or resize happens.
        wakeRequested = true;
      }
      if (!output.halted) nextDelay = nextTickDelay(output);
      else hooks.onStatus("The record has ended.");
    } catch (cause) {
      const error = cause instanceof Error ? cause : new Error(String(cause));
      hooks.onError(error);
    } finally {
      ticking = false;
      if (disposed) return;
      if (wakeRequested) {
        wakeRequested = false;
        scheduleTick();
      } else if (nextDelay !== null) {
        scheduleTick(nextDelay);
      }
    }
  };
  scheduleTick();
  hooks.onStatus("");

  return {
    intent(intent) {
      if (!disposed) {
        queued.push(intent);
        wake();
      }
    },
    dispose() {
      disposed = true;
      clearScheduledTick();
      window.removeEventListener("resize", resize);
      window.removeEventListener("gamepadconnected", onGamepadConnection);
      window.removeEventListener("gamepaddisconnected", onGamepadConnection);
      window.removeEventListener("keydown", onKeyDown, true);
      document.removeEventListener("copy", preventTextExtraction, true);
      document.removeEventListener("cut", preventTextExtraction, true);
      document.removeEventListener("dragstart", preventTextExtraction, true);
      document.removeEventListener("selectstart", preventTextExtraction, true);
      // Do not clear persisted saves or mutate the VM during React teardown.
      audio.stopAll();
      void activeOutput;
    },
    rendererBackend() {
      return renderer.backend || null;
    },
  };
}

export function emptyView(): UiViewModel | null {
  return null;
}
