import { expect, test } from "@playwright/test";

async function beginJapaneseRecord(page: import("@playwright/test").Page) {
  await page.goto("/");
  await expect(page.getByRole("button", { name: "日本語" })).toBeVisible();
  await expect(page.locator(".record-setup-screen .title-record-card, .record-setup-screen .record-stage-slip")).toHaveCount(0);
  await page.getByRole("button", { name: "日本語" }).click();
  await expect(page.getByRole("heading", { name: "海風" })).toBeVisible();
}

async function beginFirstChapter(page: import("@playwright/test").Page) {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "START" }).click();
  const catalogue = page.getByRole("dialog", { name: "CHAPTERS" });
  await expect(catalogue).toBeVisible();
  await catalogue.getByRole("button", { name: /序章/ }).click();
  const advance = page.getByRole("button", { name: "次へ" });
  await expect(advance).toBeVisible();
  return advance;
}

async function waitForCompletedPage(page: import("@playwright/test").Page) {
  await expect(page.locator(".continue-mark")).toBeVisible({ timeout: 15_000 });
}

test("first light reaches a playable chapter catalogue without an operation guide", async ({ page }) => {
  await beginJapaneseRecord(page);
  await expect(page.getByText("操作方法")).toHaveCount(0);
  await page.getByRole("button", { name: "START" }).click();
  const catalogue = page.getByRole("dialog", { name: "CHAPTERS" });
  await expect(catalogue).toBeVisible();
  await expect(catalogue.getByRole("button", { name: /序章/ })).toBeVisible();
  await expect(catalogue.getByRole("button", { name: "DAY 1", exact: true })).toBeVisible();
});

test("title load opens a record table and explains an empty slot", async ({ page }) => {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "LOAD" }).click();
  const load = page.getByRole("dialog", { name: "LOAD" });
  await expect(load).toBeVisible();
  const first = load.getByRole("button", { name: "記録 1 を開く" });
  await expect(first).toBeDisabled();
  await expect(load.getByText("この記録には保存されていません。", { exact: true })).toHaveCount(10);
});

test("title and transparent RMenu use English commands with a stable localized description", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await beginJapaneseRecord(page);
  await expect(page.getByRole("button", { name: "START" })).toBeVisible();
  await expect(page.getByRole("button", { name: "LOAD" })).toBeVisible();
  await expect(page.getByRole("button", { name: "EXTRA" })).toBeVisible();
  await expect(page.getByRole("button", { name: "CONFIG" })).toBeVisible();
  await expect(page.getByRole("button", { name: "EXIT" })).toBeVisible();
  const titleStage = page.locator(".record-title-screen--home");
  await expect(titleStage.locator(".title-record-card, .record-stage-slip, .title-opening")).toHaveCount(0);
  const titleTypeface = await titleStage.getByRole("heading", { name: "海風" }).evaluate((element) => getComputedStyle(element).fontFamily);
  expect(titleTypeface).toContain("UmikazeTitle");
  await expect(titleStage.locator(".record-stage-fragment--tractatus-one")).toContainText("Die Welt ist alles, was der Fall ist.");
  await expect(titleStage.locator(".record-stage-fragment--yodaka")).toHaveText("よだかは、実にみにくい鳥です。");
  await page.getByRole("button", { name: "LOAD" }).focus();
  const loadNote = page.getByText("保存した記録を開く", { exact: true });
  await expect(loadNote).toBeVisible();
  const titleLayout = await loadNote.evaluate((note) => {
    const command = note.closest<HTMLElement>("[data-stage-menu-item]");
    const commandLabel = command?.querySelector<HTMLElement>(".focus-menu-command");
    const noteBox = note.getBoundingClientRect();
    const commandBox = commandLabel?.getBoundingClientRect();
    const stage = document.querySelector<HTMLElement>(".record-title-screen--home");
    const fragments = stage?.querySelector<HTMLElement>(".record-stage-fragments");
    return {
      noteBelowCommand: Boolean(commandBox && noteBox.top >= commandBox.bottom),
      dividerWidth: command ? getComputedStyle(command).borderBottomWidth : "missing",
      fragmentAnimation: fragments ? getComputedStyle(fragments).animationName : "missing",
    };
  });
  expect(titleLayout.noteBelowCommand).toBe(true);
  expect(titleLayout.dividerWidth).toBe("0px");
  expect(titleLayout.fragmentAnimation).toBe("none");

  await page.getByRole("button", { name: "START" }).click();
  const catalogue = page.getByRole("dialog", { name: "CHAPTERS" });
  await catalogue.getByRole("button", { name: /序章/ }).click();
  await expect(page.getByRole("button", { name: "次へ" })).toBeVisible();
  await page.keyboard.press("Escape");
  const menu = page.getByRole("dialog", { name: "メニュー" });
  await expect(menu).toBeVisible();
  await expect(menu.getByRole("button", { name: "RESUME" })).toBeVisible();
  await expect(menu.getByRole("button", { name: "SAVE" })).toBeVisible();
  await menu.getByRole("button", { name: "SAVE" }).focus();
  const saveNote = menu.getByText("現在位置を記録する", { exact: true });
  await expect(saveNote).toBeVisible();
  const rmenuLayout = await saveNote.evaluate((note) => {
    const command = note.closest<HTMLElement>("[data-stage-menu-item]");
    const commandLabel = command?.querySelector<HTMLElement>(".focus-menu-command");
    const list = command?.parentElement;
    const noteBox = note.getBoundingClientRect();
    const commandLabelBox = commandLabel?.getBoundingClientRect();
    return {
      noteBelowCommand: Boolean(commandLabelBox && noteBox.top >= commandLabelBox.bottom),
      dividerWidth: command ? getComputedStyle(command).borderBottomWidth : "missing",
      rowGap: list ? getComputedStyle(list).rowGap : "missing",
    };
  });
  expect(rmenuLayout.noteBelowCommand).toBe(true);
  expect(rmenuLayout.dividerWidth).toBe("0px");
  expect(rmenuLayout.rowGap).not.toBe("0px");
  const surface = await menu.evaluate((element) => {
    const overlay = element.closest(".rmenu-overlay");
    const box = element.getBoundingClientRect();
    return {
      overlayBackground: overlay ? getComputedStyle(overlay).backgroundColor : "missing",
      menuBackground: getComputedStyle(element).backgroundColor,
      left: box.left,
      top: box.top,
    };
  });
  expect(surface.overlayBackground).toBe("rgba(0, 0, 0, 0)");
  expect(surface.menuBackground).toBe("rgba(0, 0, 0, 0)");
  expect(surface.left).toBeGreaterThanOrEqual(68);
  expect(surface.top).toBeGreaterThanOrEqual(68);
  await page.keyboard.press("Escape");
  await expect(menu).toBeHidden();
});

test("title EXIT confirms safely, and RMenu arrows move the focused command", async ({ page }) => {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "EXIT" }).click();
  const confirm = page.getByRole("dialog", { name: "CONFIRM" });
  await expect(confirm).toBeVisible();
  await expect(confirm.getByText("アプリケーションを終了しますか？", { exact: true })).toBeVisible();
  await confirm.locator('[data-aria-action="confirm.cancel"]').click();
  await expect(page.getByRole("button", { name: "START" })).toBeVisible();

  await page.getByRole("button", { name: "START" }).click();
  const catalogue = page.getByRole("dialog", { name: "CHAPTERS" });
  await catalogue.getByRole("button", { name: /序章/ }).click();
  await expect(page.getByRole("button", { name: "次へ" })).toBeVisible();
  await page.keyboard.press("Escape");

  const menu = page.getByRole("dialog", { name: "メニュー" });
  await menu.getByRole("button", { name: "RESUME" }).focus();
  await page.keyboard.press("ArrowDown");
  await expect(menu.getByRole("button", { name: "AUTO" })).toBeFocused();
  await expect(menu.getByText("文章を自動で送る", { exact: true })).toBeVisible();
});

test("CONFIG uses explicit rails, supports arrows, and keeps its value while open", async ({ page }) => {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "CONFIG" }).click();
  const config = page.getByRole("dialog", { name: "CONFIG" });
  await expect(config).toBeVisible();
  await expect(config.locator('input[type="range"]')).toHaveCount(0);
  await expect(config.locator(".react-aria-Switch")).toHaveCount(0);
  await expect(config.getByRole("button", { name: "TEXT" })).toHaveAttribute("aria-pressed", "true");

  const textValue = config.locator(".setting-rail-value").first();
  const before = await textValue.textContent();
  await config.getByRole("button", { name: "文字速度: increase" }).focus();
  await page.keyboard.press("ArrowRight");
  await expect(textValue).not.toHaveText(before || "");
  const after = await textValue.textContent();

  await config.getByRole("button", { name: "SOUND" }).click();
  await expect(config.getByText("音", { exact: true })).toBeVisible();
  await config.getByRole("button", { name: "TEXT" }).click();
  await expect(textValue).toHaveText(after || "");
  await config.getByRole("button", { name: "閉じる" }).click();
  await page.getByRole("button", { name: "CONFIG" }).click();
  await expect(page.getByRole("dialog", { name: "CONFIG" }).locator(".setting-rail-value").first()).toHaveText(after || "");
});

test("CONFIG keeps high contrast and reduced-motion feedback deliberate", async ({ page }) => {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "CONFIG" }).click();
  const config = page.getByRole("dialog", { name: "CONFIG" });
  await config.getByRole("button", { name: "DISPLAY" }).click();
  await config.getByRole("group", { name: "高コントラスト" }).getByRole("button", { name: "ON" }).click();
  await config.getByRole("group", { name: "動きを抑える" }).getByRole("button", { name: "ON" }).click();
  await expect(page.locator(".umikaze")).toHaveClass(/high-contrast/);
  await expect(page.locator(".umikaze")).toHaveClass(/reduce-motion/);
  const motion = await config.locator(".stage-sheet-content").evaluate((element) => {
    const style = getComputedStyle(element);
    return { duration: style.animationDuration, name: style.animationName };
  });
  expect(motion.name).toBe("stage-fade");
  expect(motion.duration).toBe("0.12s");
});

test("chapter focus changes only the preview until a command is confirmed", async ({ page }) => {
  await beginJapaneseRecord(page);
  await page.getByRole("button", { name: "START" }).click();
  const catalogue = page.getByRole("dialog", { name: "CHAPTERS" });
  const firstPreview = await catalogue.locator(".chapter-preview-image").getAttribute("src");
  const dayOne = catalogue.getByRole("button", { name: "DAY 1", exact: true });
  await dayOne.focus();
  await expect(dayOne).toHaveClass(/is-preview/);
  await expect(page.getByRole("button", { name: "次へ" })).toHaveCount(0);
  await expect(catalogue.locator(".chapter-preview-image")).not.toHaveAttribute("src", firstPreview || "");
  await dayOne.press("Enter");
  await expect(page.getByRole("button", { name: "次へ" })).toBeVisible();
});

test("subtitle content and Next are separate, fixed-grid controls", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await beginFirstChapter(page);
  const band = page.getByRole("region", { name: "読書中" });
  const metrics = await band.evaluate((element) => {
    const text = element.querySelector<HTMLElement>(".dialogue-text");
    const content = element.querySelector<HTMLElement>(".subtitle-content");
    const next = element.querySelector<HTMLElement>(".reading-advance");
    return {
      contentContainsButton: Boolean(content?.querySelector("button")),
      fontFamily: text ? getComputedStyle(text).fontFamily : "",
      whiteSpace: text ? getComputedStyle(text).whiteSpace : "",
      textWrap: text ? getComputedStyle(text).textWrap : "",
      textOverflow: text ? text.scrollWidth > text.clientWidth : true,
      nextHeight: next?.getBoundingClientRect().height || 0,
    };
  });
  expect(metrics.contentContainsButton).toBe(false);
  expect(metrics.fontFamily).toContain("AriaBundledFont0");
  expect(metrics.whiteSpace).toBe("pre");
  expect(metrics.textWrap).not.toBe("balance");
  expect(metrics.textOverflow).toBe(false);
  expect(metrics.nextHeight).toBeGreaterThanOrEqual(44);
  await expect(band.getByRole("button", { name: "次へ" })).toBeVisible();
});

test("a completed page advances to the next page or source line only on the following input", async ({ page }) => {
  const advance = await beginFirstChapter(page);
  const band = page.locator(".reading-band");
  const firstPage = await band.getAttribute("data-page-id");
  await waitForCompletedPage(page);
  await advance.click();
  await expect(band).not.toHaveAttribute("data-page-id", firstPage || "");
  await expect(page.locator(".continue-mark")).toHaveCount(0);
});

test("H opens history and a history page resumes through an OK / NG confirmation", async ({ page }) => {
  const advance = await beginFirstChapter(page);
  const band = page.locator(".reading-band");
  const firstPage = await band.getAttribute("data-page-id");
  await waitForCompletedPage(page);
  await advance.click();
  await waitForCompletedPage(page);

  await page.keyboard.press("h");
  const backlog = page.getByRole("dialog", { name: "LOG" });
  await expect(backlog).toBeVisible();
  const ledger = backlog.locator(".backlog-list");
  await expect(ledger).toHaveAttribute("role", "region");
  await expect(ledger).toHaveAttribute("tabindex", "0");
  await expect(ledger).toHaveAttribute("aria-keyshortcuts", "PageUp PageDown Home End");
  const scrollSurface = await ledger.evaluate((element) => ({
    ownOverflow: getComputedStyle(element).overflowY,
    sheetOverflow: getComputedStyle(element.closest(".stage-sheet-content")!).overflowY,
  }));
  expect(scrollSurface.ownOverflow).toBe("auto");
  expect(scrollSurface.sheetOverflow).toBe("hidden");
  await ledger.focus();
  await page.keyboard.press("PageDown");
  await expect(ledger).toBeFocused();
  const firstEntry = backlog.locator(".backlog-entry").first();
  await expect(firstEntry).toBeVisible();
  await firstEntry.click();

  const confirm = page.getByRole("dialog", { name: "CONFIRM" });
  await expect(confirm).toBeVisible();
  await expect(confirm.getByText("このページから読み直しますか？ 先の本文と選択の記録は新しい分岐になります。"))
    .toBeVisible();
  await confirm.getByRole("button", { name: "NG" }).click();
  await expect(backlog).toBeVisible();

  await backlog.locator(".backlog-entry").first().click();
  await confirm.getByRole("button", { name: "OK" }).click();
  await expect(backlog).toBeHidden();
  await expect(band).toHaveAttribute("data-page-id", firstPage || "");
  await expect(page.locator(".continue-mark")).toBeVisible();
});

test("top edge, H, Escape, and right click use their intended topmost routes", async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await beginFirstChapter(page);
  await waitForCompletedPage(page);

  await page.mouse.click(12, 12);
  const backlog = page.getByRole("dialog", { name: "LOG" });
  await expect(backlog).toBeVisible();
  await backlog.locator(".backlog-list").click({ button: "right", position: { x: 8, y: 8 } });
  await expect(backlog).toBeHidden();
  await expect(page.getByRole("dialog", { name: "メニュー" })).toBeHidden();

  await page.keyboard.press("h");
  await expect(backlog).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(backlog).toBeHidden();
});

test("Escape opens rmenu on the chapter invitation reading surface", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English" }).click();
  await page.getByRole("button", { name: "START" }).click();
  await expect(page.getByRole("button", { name: "Next" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog", { name: "Menu" })).toBeVisible();
});

test("an unlocked memory opens full screen from rmenu and returns to its gallery", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "English" }).click();
  await page.getByRole("button", { name: "START" }).click();
  await waitForCompletedPage(page);
  await page.getByRole("button", { name: "Next" }).click();

  const chapters = page.getByRole("dialog", { name: "CHAPTERS" });
  await expect(chapters).toBeVisible();
  await chapters.getByRole("button", { name: "08 — Brightest Autumn" }).click();
  await expect(page.getByRole("button", { name: "Next" })).toBeVisible();

  await page.keyboard.press("Escape");
  const menu = page.getByRole("dialog", { name: "Menu" });
  await expect(menu).toBeVisible();
  await menu.getByRole("button", { name: "EXTRA" }).click();

  const gallery = page.getByRole("dialog", { name: "EXTRA" });
  await expect(gallery).toBeVisible();
  await gallery.getByRole("button", { name: "Fragment 01" }).click();
  const viewer = page.getByRole("dialog", { name: "Fragment 01" });
  await expect(viewer).toBeVisible();
  await expect(viewer.locator(".gallery-viewer-image")).toBeVisible();

  await page.keyboard.press("ArrowRight");
  await expect(viewer).toBeVisible();
  const box = await viewer.boundingBox();
  if (!box) throw new Error("gallery viewer has no bounding box");
  await page.mouse.move(box.x + box.width * 0.7, box.y + box.height * 0.5);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width * 0.3, box.y + box.height * 0.5);
  await page.mouse.up();
  await expect(viewer).toBeVisible();

  await viewer.click({ button: "right", position: { x: box.width * 0.5, y: box.height * 0.5 } });
  await expect(viewer).toBeHidden();
  await expect(gallery).toBeVisible();
});

test("narrow reading layout has no horizontal overflow and preserves a 44px Next target", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await beginFirstChapter(page);
  const metrics = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth > window.innerWidth,
    target: document.querySelector(".reading-advance")?.getBoundingClientRect().height || 0,
  }));
  expect(metrics.overflow).toBe(false);
  expect(metrics.target).toBeGreaterThanOrEqual(44);
});

test("settled title creates neither a hidden GPU context nor a continuous animation clock", async ({ page }) => {
  await page.addInitScript(() => {
    const monitored = window as Window & { draws?: number; frames?: number; contexts?: number };
    monitored.draws = 0;
    monitored.frames = 0;
    monitored.contexts = 0;
    const originalRaf = window.requestAnimationFrame.bind(window);
    window.requestAnimationFrame = (callback) => {
      monitored.frames = (monitored.frames || 0) + 1;
      return originalRaf(callback);
    };
    const prototype = HTMLCanvasElement.prototype as unknown as {
      getContext: (this: HTMLCanvasElement, contextId: string, options?: unknown) => unknown;
    };
    const originalContext = prototype.getContext;
    prototype.getContext = function getContext(contextId, options) {
      if (contextId === "webgl" || contextId === "webgl2" || contextId === "webgpu") {
        monitored.contexts = (monitored.contexts || 0) + 1;
      }
      return originalContext.call(this, contextId, options);
    };
  });
  await beginJapaneseRecord(page);
  await page.waitForTimeout(850);
  const before = await page.evaluate(() => ({
    frames: (window as Window & { frames?: number }).frames || 0,
    contexts: (window as Window & { contexts?: number }).contexts || 0,
  }));
  await page.waitForTimeout(500);
  const after = await page.evaluate(() => (window as Window & { frames?: number }).frames || 0);
  expect(before.contexts).toBe(0);
  expect(after).toBe(before.frames);
});
