import { useEffect, useId, useRef, useState } from "react";
import {
  Button,
  Dialog,
  Heading,
  Modal,
  ModalOverlay,
} from "react-aria-components";
import {
  actionEnabled,
  routeName,
  type ActionView,
  type AriaStepOutput,
  type ChoiceView,
  type UiIntent,
  type UiViewModel,
} from "@aria/ui-sdk";

import { languageNames, localeFor, strings } from "./copy";
import { bootPresentation, type PresentationRuntime, type SaveSlotSummary } from "./runtime";
import coastRoad from "./assets/scenes/coast-road-dawn-v1.png";
import hospitalCorridor from "./assets/scenes/hospital-corridor-overcast-v1.png";
import rainWindow from "./assets/scenes/rain-window-dusk-v1.png";
import "./app.css";
import "./stage.css";

type Dispatch = (intent: UiIntent) => void;

function isOverlayRoute(route: string, view?: UiViewModel | null): boolean {
  // `chapter_select` first presents a line of narration on the reading
  // surface, then opens the actual chapter sheet after advance. Its route is
  // shared, but only the latter is a modal layer.
  if (route === "chapter_select" && view?.dialogue && view.choices.length === 0) return false;
  return ["pause", "save", "load", "settings", "backlog", "chapter_select", "gallery", "confirm"].includes(route);
}

function isInteractiveTarget(target: EventTarget | null): boolean {
  const element = target instanceof Element ? target : null;
  return Boolean(element?.closest("button, input, textarea, select, [contenteditable=true], [data-aria-action]"));
}

const sceneToneByColor: Record<string, string> = {
  "16,43,56": "tide",
  "40,75,89": "rooftop",
  "31,59,77": "platform",
  "61,70,85": "photograph",
  "36,78,90": "shore",
  "57,72,87": "rain",
  "23,37,59": "night",
  "49,85,101": "wind",
  "109,107,87": "autumn",
};

function toneForScene(output: AriaStepOutput | null): string {
  if (!output) return "loading";
  const route = routeName(output.view.route);
  if (route === "setup" || route === "title") return "title";
  const scene = output.scene as unknown as { commands?: unknown[] };
  const background = scene.commands?.find((command) => (
    Boolean(command)
    && typeof command === "object"
    && (command as { id?: unknown }).id === "scene.background"
  )) as { kind?: unknown; color?: { red?: unknown; green?: unknown; blue?: unknown } } | undefined;
  if (background?.kind === "rectangle" && background.color) {
    const color = background.color;
    const key = [color.red, color.green, color.blue].map(Number).join(",");
    return sceneToneByColor[key] || "tide";
  }
  return route === "chapter_select" ? "tide" : "night";
}

const sceneAssetByTone: Record<string, { source: string; name: "coast" | "corridor" | "rain" }> = {
  loading: { source: coastRoad, name: "coast" },
  title: { source: coastRoad, name: "coast" },
  tide: { source: coastRoad, name: "coast" },
  rooftop: { source: coastRoad, name: "coast" },
  platform: { source: hospitalCorridor, name: "corridor" },
  photograph: { source: hospitalCorridor, name: "corridor" },
  shore: { source: coastRoad, name: "coast" },
  rain: { source: rainWindow, name: "rain" },
  night: { source: rainWindow, name: "rain" },
  wind: { source: coastRoad, name: "coast" },
  autumn: { source: coastRoad, name: "coast" },
};

/**
 * The scene is a place before it is an interface.  The photos are original
 * project art; this component only decides which one belongs to the current
 * story state, without inventing another graphic language over it.
 */
function ScenePhotograph({ output }: { output: AriaStepOutput | null }) {
  const route = output ? routeName(output.view.route) : "loading";
  const tone = toneForScene(output);
  const asset = sceneAssetByTone[tone] || sceneAssetByTone.coast;
  return (
    <div key={`${route}-${tone}`} className={`scene-photograph scene-photograph--${asset.name} scene-photograph--tone-${tone}`} aria-hidden="true">
      <img src={asset.source} alt="" />
    </div>
  );
}

function ActionButton({
  label,
  id,
  active = false,
  disabled = false,
  onAction,
  className = "action-button",
}: {
  label: string;
  id: string;
  active?: boolean;
  disabled?: boolean;
  onAction: (id: string) => void;
  className?: string;
}) {
  return (
    <Button
      className={`${className}${active ? " is-active" : ""}`}
      data-aria-focusable
      data-aria-action={id}
      isDisabled={disabled}
      onPress={() => onAction(id)}
    >
      {label}
    </Button>
  );
}

function ChoiceButton({ choice, onAction, wide = false }: {
  choice: ChoiceView;
  onAction: (id: string) => void;
  wide?: boolean;
}) {
  return (
    <Button
      className={`choice-button${choice.selected ? " is-selected" : ""}${wide ? " is-wide" : ""}`}
      data-aria-focusable
      data-aria-action={choice.id}
      onPress={() => onAction(choice.id)}
    >
      <span>{choice.label}</span>
    </Button>
  );
}

type FocusMenuItem = {
  id: string;
  label: string;
  description: string;
  active?: boolean;
  disabled?: boolean;
  accessibleLabel?: string;
};

/**
 * Static visual material for every non-reading screen.  It is intentionally
 * CSS-only: the game already owns the photograph underneath, so this only
 * supplies the aged signal and margin light that make a system screen feel
 * like part of the same record rather than a web overlay.
 */
function StageBackdrop({ kind = "record" }: { kind?: string }) {
  const isTitle = kind === "title";
  const showsPaperSlips = kind !== "title" && kind !== "setup";
  return (
    <div className={`record-stage-backdrop record-stage-backdrop--${kind}`} aria-hidden="true">
      {isTitle && (
        <div className="record-stage-fragments">
          <span className="record-stage-fragment record-stage-fragment--tractatus-one" lang="de">
            1&nbsp; Die Welt ist alles, was der Fall ist.
          </span>
          <span className="record-stage-fragment record-stage-fragment--tractatus-facts" lang="de">
            1.2&nbsp; Die Welt zerfällt in Tatsachen.
          </span>
          <span className="record-stage-fragment record-stage-fragment--tractatus-silence" lang="de">
            7&nbsp; Wovon man nicht sprechen kann, darüber muß man schweigen.
          </span>
          <span className="record-stage-fragment record-stage-fragment--yodaka" lang="ja">
            よだかは、実にみにくい鳥です。
          </span>
          <span className="record-stage-fragment record-stage-fragment--galaxy" lang="ja">
            ではみなさんは、そういうふうに川だと云われたり、乳の流れたあとだと云われたりしていたこのぼんやりと白いものがほんとうは何かご承知ですか。
          </span>
        </div>
      )}
      <span className="record-stage-signal record-stage-signal--one" />
      <span className="record-stage-signal record-stage-signal--two" />
      {showsPaperSlips && <>
        <span className="record-stage-slip record-stage-slip--one" />
        <span className="record-stage-slip record-stage-slip--two" />
        <span className="record-stage-slip record-stage-slip--three" />
      </>}
    </div>
  );
}

function FocusDescription({ id, description }: { id: string; description: string }) {
  return <p id={id} className="focus-menu-description" aria-live="off">{description}</p>;
}

/**
 * A deterministic vertical command list. Pointer movement merely previews a
 * command; focus, Enter, touch, and the gamepad still share the same button
 * path. The title, first-light, and transparent RMenu variants can attach
 * their note directly below the focused command, so an explanation never
 * becomes a separate web-like panel.
 */
function FocusMenu({
  label,
  items,
  onAction,
  className = "",
  initialFocusId,
  descriptionPlacement = "after-list",
}: {
  label: string;
  items: FocusMenuItem[];
  onAction: (id: string) => void;
  className?: string;
  initialFocusId?: string;
  descriptionPlacement?: "after-list" | "under-focused-item";
}) {
  const descriptionId = useId();
  const firstAvailable = items.find((item) => !item.disabled)?.id ?? "";
  const [focusedActionId, setFocusedActionId] = useState(initialFocusId ?? firstAvailable);
  const focused = items.find((item) => item.id === focusedActionId)
    ?? items.find((item) => !item.disabled)
    ?? items[0];

  useEffect(() => {
    if (items.some((item) => item.id === focusedActionId && !item.disabled)) return;
    setFocusedActionId(initialFocusId && items.some((item) => item.id === initialFocusId && !item.disabled)
      ? initialFocusId
      : firstAvailable);
  }, [firstAvailable, focusedActionId, initialFocusId, items]);

  const moveFocus = (container: HTMLElement, direction: -1 | 1) => {
    const controls = [...container.querySelectorAll<HTMLButtonElement>("[data-stage-menu-item]")]
      .filter((control) => !control.disabled);
    if (!controls.length) return;
    const activeIndex = controls.findIndex((control) => control === document.activeElement);
    const next = activeIndex < 0
      ? (direction > 0 ? 0 : controls.length - 1)
      : (activeIndex + direction + controls.length) % controls.length;
    controls[next]?.focus({ preventScroll: true });
  };

  return (
    <nav
      className={`focus-menu ${className}`.trim()}
      aria-label={label}
      onKeyDown={(event) => {
        if (event.key === "ArrowUp") {
          event.preventDefault();
          moveFocus(event.currentTarget, -1);
        }
        if (event.key === "ArrowDown") {
          event.preventDefault();
          moveFocus(event.currentTarget, 1);
        }
      }}
    >
      <div className="focus-menu-list">
        {items.map((item) => {
          const isFocused = item.id === focused?.id;
          const noteFollowsItem = descriptionPlacement === "under-focused-item";
          return (
            <Button
              key={item.id}
              className={`focus-menu-item${isFocused ? " is-focused" : ""}${item.active ? " is-active" : ""}`}
              data-aria-focusable
              data-aria-action={item.id}
              data-stage-menu-item
              aria-describedby={noteFollowsItem ? undefined : descriptionId}
              aria-label={item.accessibleLabel}
              isDisabled={item.disabled}
              onFocus={() => setFocusedActionId(item.id)}
              onPointerEnter={() => setFocusedActionId(item.id)}
              onPress={() => onAction(item.id)}
            >
              <span className="focus-menu-command">{item.label}</span>
              {item.active && <span className="focus-menu-state" aria-label="ON">ON</span>}
              {noteFollowsItem && <span className="focus-menu-inline-description">{item.description}</span>}
            </Button>
          );
        })}
      </div>
      {descriptionPlacement === "after-list" && <FocusDescription id={descriptionId} description={focused?.description ?? ""} />}
    </nav>
  );
}

function steppedValue(value: number, direction: -1 | 1, min: number, max: number, step: number) {
  const precision = Math.max(0, String(step).split(".")[1]?.length ?? 0);
  const index = Math.round((value - min) / step) + direction;
  return Number(Math.min(max, Math.max(min, min + index * step)).toFixed(precision));
}

/** A game-like, explicit left/value/right rail. No native browser controls. */
function SettingRail({
  label,
  value,
  min,
  max,
  step,
  valueLabel,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  valueLabel: string;
  onChange(value: number): void;
}) {
  const labelId = useId();
  const decrease = () => onChange(steppedValue(value, -1, min, max, step));
  const increase = () => onChange(steppedValue(value, 1, min, max, step));
  return (
    <div
      className="setting-rail"
      onKeyDown={(event) => {
        if (event.key === "ArrowLeft") {
          event.preventDefault();
          decrease();
        }
        if (event.key === "ArrowRight") {
          event.preventDefault();
          increase();
        }
      }}
    >
      <span id={labelId} className="setting-rail-label">{label}</span>
      <div className="setting-rail-controls" role="group" aria-labelledby={labelId}>
        <Button className="setting-rail-button" data-aria-focusable aria-label={`${label}: decrease`} onPress={decrease}>◀</Button>
        <output className="setting-rail-value" aria-live="off">{valueLabel}</output>
        <Button className="setting-rail-button" data-aria-focusable aria-label={`${label}: increase`} onPress={increase}>▶</Button>
      </div>
    </div>
  );
}

function BinarySetting({
  label,
  selected,
  onSelectedChange,
}: {
  label: string;
  selected: boolean;
  onSelectedChange(selected: boolean): void;
}) {
  const labelId = useId();
  return (
    <div className="binary-setting">
      <span id={labelId} className="setting-rail-label">{label}</span>
      <div className="binary-setting-controls" role="group" aria-labelledby={labelId}>
        <Button className="binary-setting-button" data-aria-focusable aria-pressed={!selected}
          onPress={() => onSelectedChange(false)}>OFF</Button>
        <Button className="binary-setting-button" data-aria-focusable aria-pressed={selected}
          onPress={() => onSelectedChange(true)}>ON</Button>
      </div>
    </div>
  );
}

function OverlaySheet({
  title,
  kicker,
  dismissLabel,
  children,
  onDismiss,
  variant = "side",
  surface,
}: {
  title: string;
  kicker?: string;
  dismissLabel: string;
  children: React.ReactNode;
  onDismiss: () => void;
  variant?: "side" | "chapter" | "confirm" | "archive";
  surface?: "save" | "load" | "settings" | "backlog" | "gallery";
}) {
  return (
    <ModalOverlay
      className={`sheet-overlay sheet-overlay--${variant}`}
      isOpen
      isDismissable
      onOpenChange={(open) => { if (!open) onDismiss(); }}
    >
      <Modal className={`sheet-modal sheet-modal--${variant}${surface ? ` sheet-modal--${surface}` : ""}`}>
        <Dialog className={`sheet stage-sheet sheet--${variant}${surface ? ` sheet--${surface}` : ""}`} aria-label={title}>
          <StageBackdrop kind={surface ?? variant} />
          <div className="stage-sheet-content">
            <header className="sheet-header">
              <div className="sheet-heading">
                {kicker && <p className="sheet-kicker">{kicker}</p>}
                <Heading slot="title">{title}</Heading>
              </div>
              <Button className="stage-close" aria-label={dismissLabel} data-aria-focusable onPress={onDismiss}>CLOSE</Button>
            </header>
            <div className="sheet-tide-line" aria-hidden="true" />
            {children}
          </div>
        </Dialog>
      </Modal>
    </ModalOverlay>
  );
}

function SettingsSheet({ view, copy, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  dispatch: Dispatch;
}) {
  type SettingsSection = "text" | "sound" | "display";
  const [section, setSection] = useState<SettingsSection>("text");
  const set = (name: string, value: number) => dispatch({ kind: "set_setting", name, value });
  const toggle = (name: string) => dispatch({ kind: "toggle_setting", name });
  const setBoolean = (name: string, current: boolean, next: boolean) => {
    if (current !== next) toggle(name);
  };
  const sections: Array<{ id: SettingsSection; label: string; description: string }> = [
    { id: "text", label: "TEXT", description: copy.readingControls },
    { id: "sound", label: "SOUND", description: copy.sound },
    { id: "display", label: "DISPLAY", description: copy.display },
  ];
  const selectedSection = sections.find((item) => item.id === section) ?? sections[0];
  return (
    <OverlaySheet title="CONFIG" kicker={copy.settings} dismissLabel={copy.close} variant="archive" surface="settings" onDismiss={() => dispatch({ kind: "dismiss" })}>
      <div className="settings-stage">
        <nav className="settings-index" aria-label={copy.settings}>
          {sections.map((item) => (
            <Button key={item.id} className={`settings-index-item${section === item.id ? " is-selected" : ""}`}
              data-aria-focusable aria-pressed={section === item.id} onPress={() => setSection(item.id)}>
              {item.label}
            </Button>
          ))}
        </nav>
        <section className="settings-deck" aria-label={selectedSection.description}>
          <p className="settings-deck-kicker">{selectedSection.label}</p>
          <h3>{selectedSection.description}</h3>
          {section === "text" && (
            <div className="settings-rails">
              <SettingRail label={copy.textSpeed} value={view.settings.text_speed_ms} min={0} max={120} step={4}
                valueLabel={copy.valueMs(view.settings.text_speed_ms)} onChange={(value) => set("text_speed_ms", value)} />
              <SettingRail label={copy.autoDelay} value={view.settings.auto_delay_ms} min={100} max={3000} step={100}
                valueLabel={copy.valueMs(view.settings.auto_delay_ms)} onChange={(value) => set("auto_delay_ms", value)} />
              <SettingRail label={copy.textSize} value={view.settings.text_scale} min={0.85} max={1.35} step={0.05}
                valueLabel={copy.valuePercent(view.settings.text_scale)} onChange={(value) => set("text_scale", value)} />
              <BinarySetting label={copy.skipUnread} selected={view.settings.skip_unread}
                onSelectedChange={(next) => setBoolean("skip_unread", view.settings.skip_unread, next)} />
            </div>
          )}
          {section === "sound" && (
            <div className="settings-rails">
              <SettingRail label={copy.music} value={view.settings.bgm_volume} min={0} max={1} step={0.05}
                valueLabel={copy.valuePercent(view.settings.bgm_volume)} onChange={(value) => set("bgm_volume", value)} />
              <SettingRail label={copy.effects} value={view.settings.sound_effect_volume} min={0} max={1} step={0.05}
                valueLabel={copy.valuePercent(view.settings.sound_effect_volume)} onChange={(value) => set("sound_effect_volume", value)} />
              <SettingRail label={copy.voice} value={view.settings.voice_volume} min={0} max={1} step={0.05}
                valueLabel={copy.valuePercent(view.settings.voice_volume)} onChange={(value) => set("voice_volume", value)} />
            </div>
          )}
          {section === "display" && (
            <div className="settings-rails">
              <BinarySetting label={copy.fullscreen} selected={view.settings.fullscreen}
                onSelectedChange={(next) => setBoolean("fullscreen", view.settings.fullscreen, next)} />
              <BinarySetting label={copy.contrast} selected={view.settings.high_contrast}
                onSelectedChange={(next) => setBoolean("high_contrast", view.settings.high_contrast, next)} />
              <BinarySetting label={copy.reducedMotion} selected={view.settings.reduced_motion}
                onSelectedChange={(next) => setBoolean("reduced_motion", view.settings.reduced_motion, next)} />
            </div>
          )}
        </section>
      </div>
    </OverlaySheet>
  );
}

function RMenu({ view, copy, onAction, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
}) {
  const action = (id: string) => view.actions.find((item) => item.id === id);
  const items: FocusMenuItem[] = [
    { id: "dismiss", label: "RESUME", description: copy.menuDescription.resume },
    { id: "menu.auto", label: "AUTO", description: copy.menuDescription.auto, active: action("menu.auto")?.active, disabled: !action("menu.auto")?.enabled },
    { id: "menu.skip", label: "SKIP", description: copy.menuDescription.skip, active: action("menu.skip")?.active, disabled: !action("menu.skip")?.enabled },
    { id: "menu.backlog", label: "LOG", description: copy.menuDescription.log, disabled: !action("menu.backlog")?.enabled },
    { id: "menu.save", label: "SAVE", description: copy.menuDescription.save, disabled: !action("menu.save")?.enabled },
    { id: "menu.load", label: "LOAD", description: copy.menuDescription.load, disabled: !action("menu.load")?.enabled },
    { id: "menu.gallery", label: "EXTRA", description: copy.menuDescription.extra, disabled: !action("menu.gallery")?.enabled },
    { id: "menu.settings", label: "CONFIG", description: copy.menuDescription.config, disabled: !action("menu.settings")?.enabled },
    { id: "menu.reset", label: "TITLE", description: copy.menuDescription.title, disabled: !action("menu.reset")?.enabled },
    { id: "menu.quit", label: "EXIT", description: copy.menuDescription.exit, disabled: !action("menu.quit")?.enabled },
  ];
  return (
    <ModalOverlay className="rmenu-overlay" isOpen isDismissable onOpenChange={(open) => { if (!open) dispatch({ kind: "dismiss" }); }}>
      <Modal className="rmenu-modal">
        <Dialog className="pause-ledger stage-rmenu" aria-label={copy.menu}>
          <FocusMenu label={copy.menu} items={items} initialFocusId="dismiss" descriptionPlacement="under-focused-item" className="rmenu-command-list"
            onAction={(id) => {
              if (id === "dismiss") dispatch({ kind: "dismiss" });
              else onAction(id);
            }} />
        </Dialog>
      </Modal>
    </ModalOverlay>
  );
}

function ConfirmationSheet({ view, copy, onAction, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
}) {
  const action = view.confirmation?.action;
  const message = action === "quit"
    ? copy.confirmQuit
    : action === "resume_backlog"
      ? copy.confirmResume
      : copy.confirmReset;
  const resume = action === "resume_backlog";
  return (
    <OverlaySheet title="CONFIRM" kicker={copy.confirm} dismissLabel={copy.close} variant="confirm" onDismiss={() => dispatch({ kind: "activate", id: "confirm.cancel" })}>
      <p className="confirmation-message">{message}</p>
      <div className="confirmation-actions">
        <ActionButton id="confirm.accept" label={resume ? copy.ok : copy.proceed} onAction={onAction} className="confirmation-accept" />
        <ActionButton id="confirm.cancel" label={resume ? copy.ng : copy.cancel} onAction={onAction} className="confirmation-cancel" />
      </div>
    </OverlaySheet>
  );
}

function SaveLoadSheet({ kind, view, copy, onAction, dispatch, saveSlots }: {
  kind: "save" | "load";
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
  saveSlots: SaveSlotSummary[];
}) {
  const label = kind === "save" ? "SAVE" : "LOAD";
  const records = new Map(saveSlots.map((record) => [record.slot, record]));
  return (
    <OverlaySheet title={label} kicker={kind === "save" ? copy.save : copy.load} dismissLabel={copy.close} variant="archive" surface={kind} onDismiss={() => dispatch({ kind: "dismiss" })}>
      <p className="sheet-intro">{kind === "save" ? copy.saveLead : copy.loadLead}</p>
      <div className="save-slots">
        {Array.from({ length: 10 }, (_, index) => index + 1).map((slot) => {
          const id = `${kind}.slot.${slot}`;
          const slotLabel = kind === "save" ? copy.saveSlot(slot) : copy.loadSlot(slot);
          const record = records.get(slot);
          const timestamp = record?.timestampMs
            ? new Intl.DateTimeFormat(localeFor(view.game.locale), { dateStyle: "medium", timeStyle: "short" }).format(new Date(record.timestampMs))
            : null;
          const description = record
            ? [record.speaker, record.excerpt, timestamp].filter((value): value is string => Boolean(value)).join(" · ") || copy.previousRecord
            : kind === "load" ? copy.emptyRecord : slotLabel;
          const descriptionId = `record-slot-${kind}-${slot}`;
          return (
            <Button key={id} data-aria-focusable data-aria-action={id} className="record-slot"
              aria-label={slotLabel} aria-describedby={descriptionId}
              isDisabled={!actionEnabled(view, id) || (kind === "load" && !record)} onPress={() => onAction(id)}>
              <span className="record-slot-index">{copy.recordIndex(slot)}</span>
              <span className="record-slot-action">{record?.speaker || (kind === "save" ? copy.writeRecord : copy.openRecord)}</span>
              <span id={descriptionId} className="record-slot-label">{description}</span>
            </Button>
          );
        })}
      </div>
    </OverlaySheet>
  );
}

function BacklogSheet({ view, copy, onAction, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
}) {
  const list = useRef<HTMLDivElement>(null);
  const lastScrollTop = useRef(0);
  const backlogRowHeight = 104;
  const scrollPage = (element: HTMLDivElement, direction: -1 | 1) => {
    // Keep log navigation in whole readable chunks.  The list owns its own
    // scroll frame, so this does not move the surrounding system sheet.
    const pageHeight = Math.max(
      backlogRowHeight,
      Math.floor(element.clientHeight / backlogRowHeight) * backlogRowHeight,
    );
    element.scrollBy(0, pageHeight * direction);
  };
  useEffect(() => {
    const element = list.current;
    if (!element) return;
    const rowHeight = backlogRowHeight;
    const target = view.backlog_start * rowHeight;
    if (Math.abs(element.scrollTop - target) > rowHeight) element.scrollTop = target;
    lastScrollTop.current = element.scrollTop;
  }, [view.backlog_start, backlogRowHeight]);

  return (
    <OverlaySheet title="LOG" kicker={copy.history} dismissLabel={copy.close} variant="archive" surface="backlog" onDismiss={() => dispatch({ kind: "dismiss" })}>
      <div
        ref={list}
        className="backlog-list"
        role="region"
        tabIndex={0}
        aria-label={copy.history}
        aria-keyshortcuts="PageUp PageDown Home End"
        onKeyDown={(event) => {
          if (event.key === "PageDown") {
            event.preventDefault();
            scrollPage(event.currentTarget, 1);
          } else if (event.key === "PageUp") {
            event.preventDefault();
            scrollPage(event.currentTarget, -1);
          } else if (event.key === "Home") {
            event.preventDefault();
            event.currentTarget.scrollTo(0, 0);
          } else if (event.key === "End") {
            event.preventDefault();
            event.currentTarget.scrollTo(0, event.currentTarget.scrollHeight);
          }
        }}
        onScroll={(event) => {
          const top = event.currentTarget.scrollTop;
          const delta = top - lastScrollTop.current;
          lastScrollTop.current = top;
          // Core's semantic scroll unit is 48px (the native wheel step),
          // while this virtual list uses fixed 104px rows. Convert rather
          // than sending physical pixels so the window advances one entry
          // per visible row on every host.
          if (Math.abs(delta) >= 1) dispatch({ kind: "scroll", region: "backlog", delta_y: delta * 48 / backlogRowHeight });
        }}
      >
        {view.backlog_total === 0 && <p className="empty-state">{copy.noEntries}</p>}
        {view.backlog_total > 0 && (
          <div className="backlog-virtual" style={{ height: `${Math.max(view.backlog_total * backlogRowHeight, 1)}px` }}>
            <div className="backlog-window" style={{ transform: `translateY(${view.backlog_start * backlogRowHeight}px)` }}>
              {view.backlog.map((entry, index) => {
          const id = `backlog:${entry.id}`;
          return (
            <Button key={entry.id} data-aria-focusable data-aria-action={id}
              className={`backlog-entry${entry.selected ? " is-selected" : ""}`} onPress={() => onAction(id)}
              aria-posinset={view.backlog_start + index + 1} aria-setsize={view.backlog_total}>
              <span className="backlog-index">{String(view.backlog_start + index + 1).padStart(2, "0")}</span>
              <span className="backlog-copy">
                {entry.speaker && <span className="backlog-speaker">{entry.speaker}</span>}
                <span className="backlog-text">{entry.text}</span>
              </span>
            </Button>
          );
              })}
            </div>
          </div>
        )}
      </div>
    </OverlaySheet>
  );
}

function ChapterSheet({ view, copy, onAction, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
}) {
  type ChapterCard = {
    id: string;
    label: string;
    description: string;
    unlocked: boolean;
    progress: number;
    selected: boolean;
  };
  const cards: ChapterCard[] = view.choices.length ? view.choices.map((choice, index) => ({
    id: choice.id, label: choice.label, description: "", unlocked: true, progress: Math.min(100, index ? 0 : 12), selected: choice.selected,
  })) : view.chapters.map((chapter) => ({
    id: `chapter:${chapter.id}`, label: chapter.title || chapter.id, description: chapter.description, unlocked: chapter.unlocked,
    progress: chapter.progress, selected: false,
  }));
  const initialPreviewId = cards.find((card) => card.selected)?.id
    ?? cards.find((card) => card.unlocked)?.id
    ?? cards[0]?.id
    ?? "";
  const [previewChapterId, setPreviewChapterId] = useState(initialPreviewId);
  useEffect(() => {
    if (!cards.some((card) => card.id === previewChapterId)) setPreviewChapterId(initialPreviewId);
  }, [cards, initialPreviewId, previewChapterId]);
  const featured = cards.find((card) => card.id === previewChapterId) ?? cards.find((card) => card.id === initialPreviewId);
  const featuredIndex = Math.max(0, cards.findIndex((card) => card.id === featured?.id));
  const previewScenes = [coastRoad, hospitalCorridor, rainWindow];
  const moveIndexFocus = (container: HTMLElement, direction: -1 | 1) => {
    const controls = [...container.querySelectorAll<HTMLButtonElement>("[data-chapter-index-item]")]
      .filter((control) => !control.disabled);
    if (!controls.length) return;
    const activeIndex = controls.findIndex((control) => control === document.activeElement);
    const next = activeIndex < 0
      ? (direction > 0 ? 0 : controls.length - 1)
      : (activeIndex + direction + controls.length) % controls.length;
    controls[next]?.focus({ preventScroll: true });
  };
  return (
    <OverlaySheet title="CHAPTERS" kicker={copy.chapters} dismissLabel={copy.close} variant="chapter" onDismiss={() => dispatch({ kind: "dismiss" })}>
      <div className="chapter-stage">
        {featured && (
          <section className="chapter-preview" aria-label={featured.unlocked ? featured.label : copy.locked}>
            <img className="chapter-preview-image" src={previewScenes[featuredIndex % previewScenes.length]} alt="" />
            <div className="chapter-preview-record">
              <p className="chapter-preview-code">CHAPTER {String(featuredIndex + 1).padStart(2, "0")}</p>
              <span className="chapter-preview-line" aria-hidden="true" />
              <h3>{featured.unlocked ? featured.label : "SEALED"}</h3>
              <p className="chapter-preview-description">{featured.unlocked ? featured.description || copy.progress : copy.locked}</p>
              {featured.unlocked && <p className="chapter-preview-progress">{copy.progress} {Math.round(featured.progress)}%</p>}
            </div>
          </section>
        )}
        <nav className="chapter-index-menu" aria-label={copy.chapters} onKeyDown={(event) => {
          if (event.key === "ArrowUp") {
            event.preventDefault();
            moveIndexFocus(event.currentTarget, -1);
          }
          if (event.key === "ArrowDown") {
            event.preventDefault();
            moveIndexFocus(event.currentTarget, 1);
          }
        }}>
          {cards.map((card, index) => (
            <Button key={card.id} data-aria-focusable data-aria-action={card.id} data-chapter-index-item
              className={`chapter-index-row${card.unlocked ? "" : " is-locked"}${card.id === featured?.id ? " is-preview" : ""}`}
              aria-label={card.unlocked ? `${card.label}${card.progress > 0 ? ` — ${copy.progress}` : ""}` : copy.locked}
              isDisabled={!card.unlocked} onFocus={() => setPreviewChapterId(card.id)} onPointerEnter={() => setPreviewChapterId(card.id)}
              onPress={() => onAction(card.id)}>
              <span className="chapter-index-code">CHAPTER {String(index + 1).padStart(2, "0")}</span>
              <span className="chapter-index-name">{card.unlocked ? card.label : "SEALED"}</span>
              <span className="chapter-index-rule" aria-hidden="true" />
            </Button>
          ))}
        </nav>
      </div>
    </OverlaySheet>
  );
}

function GallerySheet({ view, copy, onAction, dispatch }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  dispatch: Dispatch;
}) {
  const galleryScenes = [coastRoad, hospitalCorridor, rainWindow];
  const swipeStart = useRef<{ x: number; y: number } | null>(null);
  const selectedIndex = Math.max(0, view.gallery.findIndex((item) => item.id === view.gallery_viewer));
  const selected = view.gallery[selectedIndex];
  if (view.gallery_viewer && selected) {
    return (
      <ModalOverlay className="gallery-viewer-overlay" isOpen isDismissable onOpenChange={(open) => {
        if (!open) onAction("gallery.close");
      }}>
        <Modal className="gallery-viewer-modal">
          <Dialog className="gallery-viewer" aria-label={copy.memory(selectedIndex + 1)}
            onPointerDown={(event) => { swipeStart.current = { x: event.clientX, y: event.clientY }; }}
            onPointerUp={(event) => {
              const start = swipeStart.current;
              swipeStart.current = null;
              if (!start) return;
              const deltaX = event.clientX - start.x;
              const deltaY = event.clientY - start.y;
              if (Math.abs(deltaX) >= 48 && Math.abs(deltaX) > Math.abs(deltaY)) {
                onAction(deltaX < 0 ? "gallery.next" : "gallery.previous");
              }
            }}>
            <img className="gallery-viewer-image" src={galleryScenes[selectedIndex % galleryScenes.length]} alt={copy.memory(selectedIndex + 1)} />
            <div className="gallery-viewer-shade" aria-hidden="true" />
            <header className="gallery-viewer-header">
              <span>{copy.memory(selectedIndex + 1)}</span>
              <Button className="icon-button" data-aria-focusable data-aria-action="gallery.close" aria-label={copy.close} onPress={() => onAction("gallery.close")}>×</Button>
            </header>
            <div className="gallery-viewer-controls" aria-label={copy.gallery}>
              <ActionButton id="gallery.previous" label={copy.previousMemory} onAction={onAction} className="gallery-viewer-control" />
              <span aria-live="off">{String(selectedIndex + 1).padStart(2, "0")} / {String(view.gallery.length).padStart(2, "0")}</span>
              <ActionButton id="gallery.next" label={copy.nextMemory} onAction={onAction} className="gallery-viewer-control gallery-viewer-control--next" />
            </div>
          </Dialog>
        </Modal>
      </ModalOverlay>
    );
  }
  return (
    <OverlaySheet title="EXTRA" kicker={copy.gallery} dismissLabel={copy.close} variant="archive" surface="gallery" onDismiss={() => dispatch({ kind: "dismiss" })}>
      <div className="gallery-grid">
        {view.gallery.length === 0 && <p className="empty-state">{copy.noEntries}</p>}
        {view.gallery.map((item, index) => (
          <Button key={item.id} data-aria-focusable data-aria-action={`gallery:${item.id}`}
            className={`gallery-card gallery-card--${index % 3}${item.unlocked ? "" : " is-locked"}${item.selected ? " is-selected" : ""}`}
            aria-label={item.unlocked ? copy.memory(index + 1) : copy.locked}
            isDisabled={!item.unlocked || !actionEnabled(view, `gallery:${item.id}`)}
            onPress={() => onAction(`gallery:${item.id}`)}>
            <span className="gallery-image" aria-hidden="true" style={{ backgroundImage: `linear-gradient(180deg, rgb(5 16 22 / 8%), rgb(5 16 22 / 64%)), url(${galleryScenes[index % galleryScenes.length]})` }} />
            <span className="gallery-index">{String(index + 1).padStart(2, "0")}</span>
            <span className="gallery-label">{item.unlocked ? copy.memory(index + 1) : copy.locked}</span>
          </Button>
        ))}
      </div>
    </OverlaySheet>
  );
}

function Dialogue({ view, copy, onAction, chromeVisible, onRevealChrome }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
  chromeVisible: boolean;
  onRevealChrome: () => void;
}) {
  const dialogue = view.dialogue;
  const completedAnnouncement = dialogue?.complete
    ? [dialogue.speaker, dialogue.full_page_text].filter(Boolean).join(" ")
    : "";
  const modeMark = view.auto_mode === "on" ? copy.auto : view.skip_mode !== "off" ? copy.skip : null;
  return (
    <>
      <header className={`quiet-chrome${chromeVisible ? " is-visible" : ""}`} onPointerEnter={onRevealChrome}>
        <span className="chrome-title">{copy.title}</span>
        <div className="chrome-actions">
          <ActionButton id="chrome.backlog" label={copy.history} disabled={!actionEnabled(view, "chrome.backlog")} onAction={onAction} className="chrome-button" />
          <ActionButton id="chrome.menu" label={copy.menu} disabled={!actionEnabled(view, "chrome.menu")} onAction={onAction} className="chrome-button" />
        </div>
      </header>
      {view.choices.length > 0 && (
        <nav className="choice-rail" aria-label={copy.choices}>
          {view.choices.map((choice) => <ChoiceButton key={choice.id} choice={choice} onAction={onAction} wide />)}
        </nav>
      )}
      <section className="reading-band" aria-label={copy.reading}
        data-page-id={dialogue?.page_id ?? ""}
        data-page-number={dialogue?.page_number ?? 0}
        data-page-count={dialogue?.page_count ?? 0}
        style={{ "--subtitle-columns": dialogue?.columns ?? 80 } as React.CSSProperties}>
        <div className="subtitle-content">
          {dialogue?.speaker && <span className="subtitle-speaker">{dialogue.speaker}</span>}
          <span className="dialogue-text" aria-live="off">{dialogue?.text || ""}</span>
          <span className="sr-only" aria-live="polite" aria-atomic="true">{completedAnnouncement}</span>
        </div>
        <Button className="reading-advance" data-aria-focusable data-aria-action="dialogue.advance" aria-label={copy.next} onPress={() => onAction("dialogue.advance")}>
          {dialogue?.complete && <span className="continue-mark" aria-hidden="true">·</span>}
          <span className="sr-only">{copy.next}</span>
        </Button>
        {modeMark && <span className="reading-mode-mark" aria-label={modeMark}>{modeMark}</span>}
      </section>
    </>
  );
}

function Title({ view, copy, onAction }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
}) {
  const begin = view.choices[0];
  const items: FocusMenuItem[] = [
    ...(begin ? [{ id: begin.id, label: "START", description: copy.menuDescription.start }] : []),
    { id: "route:load", label: "LOAD", description: copy.menuDescription.load, disabled: !actionEnabled(view, "route:load") },
    { id: "route:gallery", label: "EXTRA", description: copy.menuDescription.extra, disabled: !actionEnabled(view, "route:gallery") },
    { id: "route:settings", label: "CONFIG", description: copy.menuDescription.config, disabled: !actionEnabled(view, "route:settings") },
    // The VM already owns this stable action for RMenu. It intentionally
    // remains the same action here so no title-only API is needed.
    { id: "menu.quit", label: "EXIT", description: copy.menuDescription.exit },
  ];
  return (
    <section className="record-title-screen record-title-screen--home" aria-label={copy.title}>
      <StageBackdrop kind="title" />
      <header className="title-identity">
        <p className="title-record-code">{copy.eyebrow}</p>
        <div className="title-masthead">
          <h1>{copy.title}</h1>
          <p className="title-subtitle">{copy.subtitle}</p>
        </div>
      </header>
      <div className="title-selection title-selection--home">
        <FocusMenu
          label={copy.title}
          items={items}
          onAction={onAction}
          className="title-command-list"
          initialFocusId={begin?.id}
          descriptionPlacement="under-focused-item"
        />
      </div>
    </section>
  );
}

async function reopenPresentation() {
  // A player can safely recover from a partially updated PWA shell without
  // touching their IndexedDB saves.  The service worker only owns disposable
  // presentation assets; save data is deliberately outside this cleanup.
  try {
    if ("serviceWorker" in navigator) {
      const scope = new URL("./", document.baseURI).href;
      const registrations = await navigator.serviceWorker.getRegistrations();
      await Promise.all(registrations
        .filter((registration) => registration.scope === scope)
        .map((registration) => registration.unregister()));
    }
    if ("caches" in window) {
      const keys = await caches.keys();
      await Promise.all(keys
        .filter((key) => key.startsWith("umikaze-shell"))
        .map((key) => caches.delete(key)));
    }
  } finally {
    window.location.reload();
  }
}

function RuntimeProblem({ copy, detail }: { copy: ReturnType<typeof strings>; detail: string }) {
  return (
    <section className="runtime-problem" aria-labelledby="runtime-problem-title" role="alert">
      <p className="runtime-problem-kicker">{copy.records}</p>
      <h1 id="runtime-problem-title">{copy.startupIssue}</h1>
      <Button className="runtime-retry" data-aria-focusable onPress={() => { void reopenPresentation(); }}>
        {copy.reopenRecord}
      </Button>
      <p className="runtime-problem-detail">{detail}</p>
    </section>
  );
}

function Setup({ view, copy, onAction }: {
  view: UiViewModel;
  copy: ReturnType<typeof strings>;
  onAction: (id: string) => void;
}) {
  const items: FocusMenuItem[] = view.choices.map((choice, index) => {
    const known = languageNames[index];
    return {
      id: choice.id,
      label: (known?.sublabel || choice.label).toUpperCase(),
      description: copy.languagePrompt,
      accessibleLabel: choice.label,
    };
  });
  return (
    <section className="record-title-screen record-setup-screen" aria-label={copy.firstLight}>
      <StageBackdrop kind="setup" />
      <header className="title-identity">
        <p className="title-record-code">{copy.eyebrow}</p>
        <div className="title-masthead">
          <h1>{copy.title}</h1>
          <p className="title-subtitle">{copy.subtitle}</p>
        </div>
      </header>
      <div className="title-selection title-selection--setup">
        <p className="title-selection-kicker">{copy.firstLight}</p>
        <FocusMenu
          label={copy.languagePrompt}
          items={items}
          onAction={onAction}
          className="title-command-list setup-command-list"
          initialFocusId={items[0]?.id}
          descriptionPlacement="under-focused-item"
        />
      </div>
    </section>
  );
}

function Screen({ view, dispatch, chromeVisible, onRevealChrome, saveSlots }: {
  view: UiViewModel;
  dispatch: Dispatch;
  chromeVisible: boolean;
  onRevealChrome: () => void;
  saveSlots: SaveSlotSummary[];
}) {
  const copy = strings(view.game.locale);
  const route = routeName(view.route);
  const onAction = (id: string) => dispatch({ kind: "activate", id });
  if (route === "setup") return <Setup view={view} copy={copy} onAction={onAction} />;
  if (route === "title") return <Title view={view} copy={copy} onAction={onAction} />;
  if (route === "pause") return <RMenu view={view} copy={copy} onAction={onAction} dispatch={dispatch} />;
  if (route === "save" || route === "load") return <SaveLoadSheet kind={route} view={view} copy={copy} onAction={onAction} dispatch={dispatch} saveSlots={saveSlots} />;
  if (route === "settings") return <SettingsSheet view={view} copy={copy} dispatch={dispatch} />;
  if (route === "backlog") return <BacklogSheet view={view} copy={copy} onAction={onAction} dispatch={dispatch} />;
  // The invitation to choose a chapter is story text, not a modal. Let it
  // occupy the same quiet reading surface as the novel before the catalogue
  // itself arrives on the following advance.
  if (route === "chapter_select" && view.dialogue && view.choices.length === 0) {
    return <Dialogue view={view} copy={copy} onAction={onAction} chromeVisible={chromeVisible} onRevealChrome={onRevealChrome} />;
  }
  if (route === "chapter_select") return <ChapterSheet view={view} copy={copy} onAction={onAction} dispatch={dispatch} />;
  if (route === "gallery") return <GallerySheet view={view} copy={copy} onAction={onAction} dispatch={dispatch} />;
  if (route === "confirm") return <ConfirmationSheet view={view} copy={copy} onAction={onAction} dispatch={dispatch} />;
  return <Dialogue view={view} copy={copy} onAction={onAction} chromeVisible={chromeVisible} onRevealChrome={onRevealChrome} />;
}

export default function App() {
  const canvas = useRef<HTMLCanvasElement>(null);
  const runtime = useRef<PresentationRuntime | null>(null);
  const focusBeforeOverlay = useRef<HTMLElement | null>(null);
  const focusRestoreAction = useRef<string | null>(null);
  const lastFocusable = useRef<HTMLElement | null>(null);
  const previousWasOverlay = useRef(false);
  const chromeTimer = useRef<number | null>(null);
  const [output, setOutput] = useState<AriaStepOutput | null>(null);
  const [status, setStatus] = useState("Opening the record…");
  const [error, setError] = useState<string | null>(null);
  const [chromeVisible, setChromeVisible] = useState(false);
  const [saveSlots, setSaveSlots] = useState<SaveSlotSummary[]>([]);

  useEffect(() => {
    const target = canvas.current;
    if (!target) return;
    let alive = true;
    void bootPresentation(target, {
      onOutput(next) { if (alive) setOutput(next); },
      onStatus(message) { if (alive) setStatus(message); },
      onError(cause) { if (alive) setError(cause.message); },
      onSaveSlots(next) { if (alive) setSaveSlots(next); },
    }).then((controller) => {
      if (alive) runtime.current = controller;
      else controller.dispose();
    }).catch((cause: unknown) => {
      if (alive) setError(cause instanceof Error ? cause.message : String(cause));
    });
    return () => {
      alive = false;
      runtime.current?.dispose();
      runtime.current = null;
    };
  }, []);

  const view = output?.view;
  const fallbackCopy = strings(view?.game.locale ?? navigator.language);
  const route = view ? routeName(view.route) : "loading";
  const tone = toneForScene(output);

  useEffect(() => () => {
    if (chromeTimer.current !== null) window.clearTimeout(chromeTimer.current);
  }, []);

  useEffect(() => {
    if (route !== "dialogue" && route !== "chapter_select") {
      if (chromeTimer.current !== null) window.clearTimeout(chromeTimer.current);
      chromeTimer.current = null;
      setChromeVisible(false);
    }
  }, [route]);

  const revealChrome = () => {
    if (route !== "dialogue" && route !== "chapter_select") return;
    setChromeVisible(true);
    if (chromeTimer.current !== null) window.clearTimeout(chromeTimer.current);
    chromeTimer.current = window.setTimeout(() => {
      setChromeVisible(false);
      chromeTimer.current = null;
    }, 1800);
  };
  useEffect(() => {
    if (!view) return;
    document.documentElement.lang = localeFor(view.game.locale);
    if (view.settings.fullscreen && !document.fullscreenElement) {
      void document.documentElement.requestFullscreen?.().catch(() => {});
    }
    if (!view.settings.fullscreen && document.fullscreenElement) {
      void document.exitFullscreen?.().catch(() => {});
    }
  }, [view?.game.locale, view?.settings.fullscreen]);

  // React Aria presents sheets in a document-level overlay container. Mirror
  // the two visual accessibility states onto <html> so those non-reading
  // layers receive the same treatment without widening any reading selector.
  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle("umikaze-high-contrast", Boolean(view?.settings.high_contrast));
    root.classList.toggle("umikaze-reduced-motion", Boolean(view?.settings.reduced_motion));
    return () => {
      root.classList.remove("umikaze-high-contrast", "umikaze-reduced-motion");
    };
  }, [view?.settings.high_contrast, view?.settings.reduced_motion]);

  useEffect(() => {
    const wasOverlay = previousWasOverlay.current;
    const nowOverlay = isOverlayRoute(route, view);
    if (!wasOverlay && nowOverlay) {
      // React Aria moves focus into its modal during the commit. Retain the
      // last focused control from the presentation layer rather than sampling
      // `activeElement` after that move, so Escape returns to the opener.
      focusBeforeOverlay.current = lastFocusable.current
        ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
      focusRestoreAction.current ??= focusBeforeOverlay.current?.getAttribute("data-aria-action") ?? null;
    }
    if (wasOverlay && !nowOverlay) {
      const target = focusBeforeOverlay.current;
      const action = focusRestoreAction.current;
      window.requestAnimationFrame(() => {
        // React Aria restores the focus scope during its own first frame.
        // Run immediately afterwards and only when a new sheet has not
        // opened, preserving the originating control for keyboard users.
        window.requestAnimationFrame(() => {
          if (document.querySelector('[role="dialog"]')) return;
          const recreatedTarget = action
            ? [...document.querySelectorAll<HTMLElement>("[data-aria-action]")]
              .find((element) => element.getAttribute("data-aria-action") === action)
            : null;
          if (recreatedTarget && !recreatedTarget.matches(":disabled")) {
            recreatedTarget.focus({ preventScroll: true });
            return;
          }
          if (target?.isConnected && !target.matches(":disabled")) {
            target.focus({ preventScroll: true });
            return;
          }
          document.querySelector<HTMLElement>("[data-aria-focusable]")?.focus({ preventScroll: true });
        });
      });
      focusBeforeOverlay.current = null;
      focusRestoreAction.current = null;
    }
    previousWasOverlay.current = nowOverlay;
  }, [route, view]);

  const dispatch: Dispatch = (intent) => {
    if (intent.kind === "activate" && view && !isOverlayRoute(route, view) && intent.id !== "dialogue.advance") {
      const active = document.activeElement;
      if (active instanceof HTMLElement && active.matches("[data-aria-focusable]")) {
        focusBeforeOverlay.current = active;
        focusRestoreAction.current = active.getAttribute("data-aria-action");
      }
    }
    runtime.current?.intent(intent);
  };
  const rememberFocusable = (target: EventTarget | null) => {
    const element = target instanceof Element ? target : null;
    const control = element?.closest<HTMLElement>("[data-aria-focusable]");
    if (control) lastFocusable.current = control;
  };
  const openRMenu = (event: React.MouseEvent<HTMLElement>) => {
    const target = event.target instanceof Element ? event.target : null;
    if (target?.closest("input, textarea, select, [contenteditable=true]")) return;
    if (!isOverlayRoute(route, view) && view && actionEnabled(view, "chrome.menu")) {
      event.preventDefault();
      dispatch({ kind: "activate", id: "chrome.menu" });
    } else if (isOverlayRoute(route, view)) {
      event.preventDefault();
      // A secondary click closes only the foremost semantic layer.  In
      // particular, it returns from a CG viewer to its grid instead of
      // discarding every sheet underneath it.
      dispatch({ kind: "dismiss" });
    }
  };
  const capturePointer = (event: React.PointerEvent<HTMLElement>) => {
    if (event.button === 2) {
      // React Aria's press abstraction must never treat a secondary click as
      // a reading advance. The following contextmenu event opens rmenu.
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    rememberFocusable(event.target);
  };
  const observePointer = (event: React.PointerEvent<HTMLElement>) => {
    // The two quiet controls belong to the top edge rather than the reading
    // surface. This keeps the scene clear, while remaining discoverable with
    // a mouse or pen and fully reachable from the keyboard.
    if (event.clientY <= 84) revealChrome();
  };
  const useReadingEdge = (event: React.MouseEvent<HTMLElement>) => {
    // The film has two mouse-only edges: the upper black border recalls the
    // record, while the lower border turns the next subtitle. Buttons retain
    // their own press semantics; these zones only cover the intentionally
    // empty space around them.
    if (!view || event.detail === 0 || event.button !== 0 || isInteractiveTarget(event.target)) return;
    const isReadingSurface = route === "dialogue"
      || (route === "chapter_select" && Boolean(view.dialogue) && view.choices.length === 0);
    if (!isReadingSurface) return;

    const topEdge = Math.min(112, Math.max(52, window.innerHeight * 0.095));
    const bottomEdge = Math.min(116, Math.max(92, window.innerHeight * 0.11));
    if (event.clientY <= topEdge && actionEnabled(view, "chrome.backlog")) {
      event.preventDefault();
      event.stopPropagation();
      dispatch({ kind: "activate", id: "chrome.backlog" });
      return;
    }
    if (
      view.choices.length === 0
      && event.clientY >= window.innerHeight - bottomEdge
    ) {
      event.preventDefault();
      event.stopPropagation();
      dispatch({ kind: "activate", id: "dialogue.advance" });
    }
  };
  return (
    <main
      className={`umikaze route-${route} scene-tone-${tone}${view?.choices.length ? " has-choices" : ""}${view?.settings.high_contrast ? " high-contrast" : ""}${view?.settings.reduced_motion ? " reduce-motion" : ""}`}
      style={{ "--text-scale": view?.settings.text_scale ?? 1 } as React.CSSProperties}
      onContextMenuCapture={openRMenu}
      onPointerDownCapture={capturePointer}
      onPointerMoveCapture={observePointer}
      onClickCapture={useReadingEdge}
      onFocusCapture={(event) => {
        rememberFocusable(event.target);
        if (event.target instanceof Element && event.target.closest(".quiet-chrome")) revealChrome();
      }}
    >
      <canvas ref={canvas} className="scene-canvas" data-aria-stage="dom" aria-hidden="true" />
      <ScenePhotograph output={output} />
      <div className="atmosphere" aria-hidden="true" />
      <div className="presentation-layer">
        {view && <Screen view={view} dispatch={dispatch} chromeVisible={chromeVisible} onRevealChrome={revealChrome} saveSlots={saveSlots} />}
        {error && <RuntimeProblem copy={fallbackCopy} detail={error} />}
      </div>
      {!error && status && <p className="runtime-status" role="status" aria-live="polite">{status}</p>}
    </main>
  );
}
