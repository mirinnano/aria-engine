# AriaEngine Documentation Refresh - Notepad

## Conventions
- End-user docs: Japanese
- AI agent docs: English
- 1 file = 1 language (no mixing)

## Key Source Files
- `src/AriaEngine/Core/CommandRegistry.cs` - Opcode categories
- `src/AriaEngine/Core/OpCode.cs` - Opcode enum
- `src/AriaEngine/Core/Parser.cs` - Syntax implementation
- `src/AriaEngine/Core/VirtualMachine.cs` - Execution
- `src/AriaEngine/Core/GameState.cs` - State management
- `src/AriaEngine/Core/Sprite.cs` - Sprite properties
- `src/AriaEngine/Rendering/SpriteRenderer.cs` - Rendering
- `src/AriaEngine/Input/InputHandler.cs` - Input/button
- `src/AriaEngine/Audio/AudioManager.cs` - Audio
- `src/AriaEngine/Rendering/TweenManager.cs` - Tweens
- `src/AriaEngine/Core/ConfigManager.cs` - Config settings

## Critical Fixes Needed
- `gosub *label with 1, 1` → `gosub *label, arg1, arg2` (tutorials)
- Opcode count: "63" → actual count from CommandRegistry.cs
- `bg` command: add to sprite reference
- `while`/`wend`: add to syntax and script-control reference

## Diátaxis Structure
- tutorials/ - Learning-oriented (Japanese)
- how-to-guides/ - Problem-oriented (Japanese)
- reference/ - Information-oriented (Japanese)
- explanation/ - Understanding-oriented (Japanese)
- ai-agent/ - AI agent guide (English)

## docs/README.md Rewritten
- Bilingual hub: Japanese primary, English secondary (inline mixed, not separate files)
- Diátaxis decision tree: "初めてですか？" → tutorials, "スクリプトの書き方" → how-to-guides, "仕組みを理解したい" → explanation
- Links to all 5 sections: tutorials/, how-to-guides/, reference/, explanation/, ai-agent/
- Quick reference table: 5 most common commands (text, wait, lsp, msp, if)
- Kept concise (~113 lines), no detailed technical content
- Links instead of duplicating content
- Core → reference/opcodes/basic.md + script-control.md
- Script → reference/opcodes/script-control.md
- Render → reference/opcodes/sprite.md + animation.md
- Input → reference/opcodes/button.md
- Ui + Text → reference/opcodes/ui.md
- Audio → reference/opcodes/audio.md
- System + Save → reference/opcodes/system.md
- Flags → reference/opcodes/flag.md
- Compatibility → reference/opcodes/chapter.md + character.md + init.md

## docs/ai-agent/AGENTS.md Created (Task 5)
- Path: `docs/ai-agent/AGENTS.md`
- Unifies `AGENTS.md` and `CLAUDE.md` (were 100% identical at 117 lines each)
- Target audience: both Codex and Claude Code AI agents
- Language: English only
- Key changes from source files:
  - Header: "AI agents (Codex and Claude Code)" instead of just one agent
  - Opcode count: "~80 opcodes" → "101 opcodes" (counted from OpCode.cs enum entries)
  - Cross-references added: "For detailed documentation, see:" section linking to new doc structure
- Sections included: Build/Run, Architecture Overview (7 core components), Script Language, Engine Initialization, Main Loop, Directory Structure, Debug Mode, Documentation Structure
- All content is factual/concise - no narrative/fluff

## root/README.md Rewritten (Task 3)
- Total lines: 48 (was 313 - 84% reduction)
- Kept: project name, one-line description, key features (concise bullets)
- Kept: quick start (3 commands max)
- Kept: project structure, script example
- Kept: links to docs/README.md and docs/ai-agent/AGENTS.md
- Kept: license, contribution (brief)
- Removed: full opcode lists, tutorials, detailed usage sections
- Removed: installation details (init.aria creation, detailed setup)
- Removed: duplicate feature descriptions
- Removed: "謝辞" section

## docs/reference/opcodes/index.md Created (Task X)
- Path: `docs/reference/opcodes/index.md`
- Japanese language (as required)
- Category overview table with all 11 categories from CommandRegistry.cs
- Navigation section grouped by function (基本コマンド, スプライト・描画, etc.)
- Quick search table with ~150 command entries linking to category files
- Fixed issues found:
  - `goto` row had broken link: `[basic.md` → `[basic.md](basic.md)`
  - `if` row had wrong filename: `script.md` → `script-control.md`
  - Removed duplicate `spalpha` entry (SpAlpha and sp_alpha both map to same command)
- No opcode count claimed (following MUST NOT DO rule)
- Individual opcode documentation deferred to category files


## CODEBASE.md Deep Dive Findings
- VM dispatch is O(1) via _handlerTable array indexed by (int)OpCode
- GameState is a flat facade over 16 nested domain-specific state objects
- SpriteRenderer uses _renderLock because LiveReloadManager invalidates textures from a background thread
- Command handlers are stateless; all behavior mutates GameState via VM reference
- Text rendering has two paths: plain (single-style) and segmented (multi-style with effects)
- TweenProperty enum limits animatable fields; extending requires updating ApplyValue in TweenManager
- FastRegisters int[10] cache optimizes %0-%9 access
- Save/load works by serializing entire GameState; no delta system
- Scope frames support deferred instruction execution (LIFO) on exit
- Error suppression: SafeFrame logs only on 1st, 60th, and 600th occurrence of the same frame error

## init.aria Reference Findings

- `init.aria` is parsed and executed by Program.cs BEFORE window creation and main script loading.
- Truly init-only commands (values read once after init.aria execution):
  - `window` (width/height; title can be changed later via `window_title`/`caption`)
  - `font` (font path loaded once)
  - `font_atlas_size` (clamped 8-512)
  - `font_filter` (bilinear/trilinear/point/anisotropic)
  - `script` (main script path read once)
- Commands commonly in init.aria but also runtime-capable:
  - `debug` (toggles debug overlay; also F3 at runtime)
  - `compat_mode` (toggles CompatAutoUi; affects text/choice/yesnobox/mesbox auto-UI generation)
  - `textbox`, `fontsize`, `textcolor`, `textbox_color`, `textbox_style`, `choice_style`
- `compat_mode on` enables auto-generation of textbox background, choice buttons, yesnobox/mesbox UI.
- `compat_mode off` requires manual UI construction with `lsp`/`spbtn`/`btnwait`.
- Default values for textbox/choice styles come from `UIThemeDefaults.cs`.
- All init.aria commands are registered in `CommandRegistry.cs` and handled by `CoreCommandHandler`, `TextCommandHandler`, or `SystemCommandHandler`.

## docs/reference/config.md Created
- Path: `docs/reference/config.md`
- Language: Japanese (end-user docs)
- Documents all 10 AppConfig settings from ConfigManager.cs:
  - GlobalTextSpeedMs (int, 30) - text typing speed in ms
  - DefaultTextSpeedMs (int, 30) - engine default text speed
  - TextMode (string, "adv") - "adv" or "nvl" display mode
  - SkipUnread (bool, false) - skip unread text in skip mode
  - AutoModeWaitTimeMs (int, 2000) - auto mode page wait time
  - BgmVolume (int, 100) - background music volume 0-100
  - SeVolume (int, 100) - sound effect volume 0-100
  - IsFullscreen (bool, false) - fullscreen toggle
  - WindowWidth (int, 1280) - window width in pixels
  - WindowHeight (int, 720) - window height in pixels
- Grouped by category: テキスト表示, オーディオ, 画面表示
- Includes example config.json with all default values
- Notes: file location, invalid JSON handling, in-game menu modification

## docs/reference/syntax.md Created (Task 6)
- Path: `docs/reference/syntax.md`
- Language: Japanese only
- Documents ALL syntax constructs from Parser.cs:
  - Labels, comments, variables (registers + scoped declarations)
  - String interpolation: `${$var}`, `${%var}`, `${expression}`
  - Commands including functional-style `command(args)` syntax
  - Subroutines: `defsub`, `gosub *label, arg1, arg2`, `return`, `return value`, `getparam`
  - Control flow: 1-line if, block if/else/endif, brace blocks `{}`, goto, beq/bne/bgt/blt
  - Loops: `while`/`wend` (with brace blocks), `for`/`next`, `break`, `continue`
  - Text: inline `Character「message」`, `\` page clear, `@` wait
  - Multiple statements per line with `:`
  - Constants: `const`, `#define`
- Key findings from Parser.cs:
  - `gosub` syntax is `gosub *label, arg1, arg2` -- no `with` keyword exists
  - `while`/`wend` is fully implemented with `break`/`continue`
  - `for`/`next` loops over ranges OR array length
  - String interpolation supports expressions via EvaluateString
  - `auto`/`var` are aliases for `let`
  - Brace blocks `{}` are preprocessor-translated to endif/wend

## script-control.md Findings (Task 8)

**File created:** `docs/reference/opcodes/script-control.md`

**Coverage:**
- Script category: `alias`/`numalias`, `gosub`/`call`, `return`/`ret`, `returnvalue`, `defsub`/`sub`, `getparam`, `include`
- Relevant Core category: `goto`/`jmp`, `cmp`, `beq`, `bne`, `bgt`, `blt`, `while`, `wend`, `break`, `continue`

**Key corrections from source code:**
- `gosub` syntax confirmed: `gosub *label, arg1, arg2` (args pushed in reverse order onto ParamStack, getparam pops in correct order)
- `ref` prefix for pass-by-reference: `gosub *label, ref %var` -- processed by getparam via `REF|` prefix
- `while`/`wend` implemented as block structure with auto-generated labels (`__while_start_N`, `__while_end_N`)
- `break`/`continue` jump to `__while_end_N` / `__while_start_N` respectively
- `if` has 3 forms: inline `if cond cmd`, one-line block `if cond { cmd }`, and block `if cond ... [else ...] endif`
- `returnvalue` sets `State.LastReturnValue` and performs same cleanup as `return`
- `defer` runs on scope exit for `goto`, `break`, `continue`, `return`

**Syntax fixes applied:**
- Removed misleading `%var` interpolation from `text` command examples (engine requires `${%var}` syntax via GetString)
- Fixed `\|\|` -> `||` in operator table (backslashes unnecessary inside code spans)

**Verification:**
- All examples are syntactically valid per Parser.cs
- No broken `gosub with` syntax present
- `while`/`wend` fully documented

## docs/reference/opcodes/sprite.md Created (Task 9)

**File created:** `docs/reference/opcodes/sprite.md`

**Coverage:**
- All Render category commands from CommandRegistry.cs:
  - Sprite creation: `lsp`, `lsp_text`, `lsp_rect`
  - Visibility/removal: `vsp`, `csp`
  - Movement: `msp`, `msp_rel`
  - Properties: `sp_z`, `sp_alpha`, `sp_scale`, `sp_fontsize`, `sp_color`, `sp_fill`, `sp_rotation`
  - Decoration: `sp_round`, `sp_border`, `sp_gradient`, `sp_shadow`
  - Text decoration: `sp_text_align`, `sp_text_valign`, `sp_text_shadow`, `sp_text_outline`
  - Hover effects: `sp_hover_color`, `sp_hover_scale`, `sp_cursor`
  - Animation: `amsp`, `afade`, `ascale`, `acolor`, `await`, `ease`
  - Screen effects: `fade_in`, `fade_out`, `quake`
- Plus Compatibility category: `bg` (widely used, same as `load_bg`)

**Key findings from source code:**
- `bg` and `load_bg` execute identical code (same switch case in RenderCommandHandler)
- `csp -1` clears ALL sprites, button map, focused button, AND sprite lifetime stacks
- `sp_alpha` takes 0-255 but internally divides by 255.0f (Sprite.Opacity is float 0.0-1.0)
- `acolor` is NOT animated despite the `a` prefix — it sets color immediately (no TweenProperty.Color exists)
- `sp_gradient` requires 4 args but only uses args[0], [2], [3]; arg[1] is unused (start color comes from FillColor)
- `quake` defaults: amplitude=5, time=500ms
- `fade_in`/`fade_out` handled by SystemCommandHandler, not RenderCommandHandler
- `vsp` accepts both "on"/"off" strings and 1/0 integers
- Sprite type compatibility table included showing which commands work with Image/Text/Rect

**Notable omissions (per MUST NOT DO):**
- `spbtn` and `btnwait` are Input category → documented in button.md
- `clr` is Compatibility category → documented in init.md
- `load_bg` is just an alias for `bg`

**Verification:**
- All command signatures match handler ValidateArgs requirements
- All examples use valid .aria syntax
- File is 882 lines, Japanese language only

## docs/reference/opcodes/animation.md Created (Task 10)

**File created:** `docs/reference/opcodes/animation.md`

**Coverage:**
- Tween commands: `ease`, `amsp`, `afade`, `ascale`, `acolor`, `await`
- Screen effects: `fade_in`, `fade_out`, `quake`

**Key findings from source code:**
- `acolor` is NOT animated — it immediately sets `Sprite.Color` with no tween (confirmed in TweenCommandHandler.cs)
- `fade_in`/`fade_out` are Render category in CommandRegistry.cs but handled by SystemCommandHandler (sets `VmState.FadingIn`/`FadingOut`)
- `quake` defaults: amplitude=5, time=500ms (handled by RenderCommandHandler)
- `amsp` creates TWO tweens (X and Y) with same duration and ease
- `ascale` creates TWO tweens (ScaleX and ScaleY) with same duration and ease
- `afade` tween target is `Opacity` (0.0-1.0), input `toAlpha` is divided by 255.0f
- `await` sets `VmState.WaitingForAnimation`; auto-resumes when all tweens complete
- `ease` is global state (`Tweens.CurrentEaseType`); affects all subsequent tween commands
- Easing functions: Linear, EaseIn (p²), EaseOut (p(2-p)), EaseInOut (piecewise quadratic)
- `quakex` is an alias for `quake` (same OpCode.Quake)

**Notable omissions (per MUST NOT DO):**
- Non-animation Render commands (`lsp`, `msp`, `sp_alpha`, etc.) remain in sprite.md
- `effect` and `print` are no-ops in RenderCommandHandler → not documented

**Verification:**
- All command signatures match handler ValidateArgs requirements
- All examples use valid .aria syntax
- File is 244 lines, Japanese language only
- Includes easing function detail table derived from TweenManager.cs

## docs/reference/opcodes/audio.md Created (Task 11)

**File created:** `docs/reference/opcodes/audio.md`

**Coverage:**
- All 12 Audio category commands from CommandRegistry.cs:
  - BGM: `play_bgm`/`bgm`, `stop_bgm`, `play_mp3`/`mp3loop`
  - SE: `play_se`
  - Voice: `dwave`, `dwaveloop`, `dwavestop`
  - Volume: `bgmvol`, `sevol`, `mp3vol`
  - Fade: `bgmfade`, `mp3fadeout`

**Key findings from source code:**
- Volume range is 0-100 (NOT 0-255 as incorrectly stated in old docs/api/opcodes.md)
- `mp3vol` sets `State.BgmVolume` — it is NOT independent from `bgmvol`; they share the same state
- `play_mp3` and `play_bgm` both set `State.CurrentBgm`; they share the same BGM slot
- `mp3fadeout` and `bgmfade` execute identical code (both set `BgmFadeOutDurationMs`/`BgmFadeOutTimerMs`)
- `play_se`, `dwave`, `dwaveloop` all add to `State.PendingSe` queue; actual playback happens in next `AudioManager.Update`
- 2-arg forms (`play_se ch, "path"`, `dwave ch, "path"`) exist for NScripter compat but channel arg is ignored
- `dwave` additionally sets `State.LastVoicePath` for backlog voice replay
- `dwavestop` clears `PendingSe` queue only; does not stop already-playing sounds
- BGM cache max 8, SE cache max 16; LRU eviction
- Failed loads are tracked and not retried in same session
- `fade_in`/`fade_out` are Render category (screen transitions), NOT audio commands — excluded per MUST NOT DO
- `sestop` does not exist as a command

**Verification:**
- All 12 Audio category opcodes documented
- No non-Audio category opcodes included
- Language: Japanese only
- File is ~180 lines

## docs/reference/opcodes/flag.md Created (Task 12)

**File created:** `docs/reference/opcodes/flag.md`

**Coverage:**
- All 20 Flags category commands from CommandRegistry.cs:
  - 通常フラグ: `set_flag`, `get_flag`, `clear_flag`, `toggle_flag`
  - セーブフラグ: `set_pflag`, `get_pflag`, `clear_pflag`, `toggle_pflag`, `set_sflag`, `get_sflag`, `clear_sflag`, `toggle_sflag`
  - 揮発フラグ: `set_vflag`, `get_vflag`, `clear_vflag`, `toggle_vflag`
  - カウンター: `inc_counter`, `dec_counter`, `set_counter`, `get_counter`

**Key findings from source code:**
- `pflag` and `sflag` share the SAME underlying storage (`State.SaveFlags`). `set_pflag` writes to `SaveFlags`, `get_sflag` reads from `SaveFlags`
- `vflag` uses `State.VolatileFlags` and does NOT call `MarkPersistentDirty()` — never persisted
- `flag`, `pflag`/`sflag`, and `counter` all call `MarkPersistentDirty()`, saving to `saves/persistent.ariasav`
- `persistent.ariasav` is encrypted (AES-CBC) and compressed — manual editing is impossible
- `inc_counter` / `dec_counter` accept optional second argument for amount (default 1)
- All `get_*` commands return 0 for undefined keys (TryGetValue with fallback)
- `set_*flag` treats any non-zero value as `true`, zero as `false`
- Flag/counter names are string keys, not numeric IDs like sprites

**Persistence semantics documented:**
- Overview table at top of file showing all 4 storage types and their persistence
- Explicit note that `pflag` and `sflag` are aliases for same storage
- Explicit note that `vflag` is lost on game exit / save / load

**Verification:**
- All 20 Flags category opcodes documented
- No opcodes from other categories included
- Language: Japanese only
- File is ~471 lines

## docs/reference/opcodes/system.md Created (Task 13)

**File created:** `docs/reference/opcodes/system.md`

**Coverage:**
- Save category (5 commands): `saveon`, `saveoff`, `save`, `load`, `saveinfo`
- System category (21 commands): `end`, `window`, `caption`, `window_title`, `debug`, `systemcall`, `system_button`, `yesnobox`, `mesbox`, `scope`, `end_scope`, `backlog_count`, `backlog_entry`, `gallery_entry`, `cgunlock`, `gallery_count`, `gallery_info`, `getconfig`, `setconfig`, `saveconfig`
- Unimplemented: `automode_time` (registered in CommandRegistry.cs but no handler exists)

**Key findings from source code:**
- `save` / `load` check `State.SaveMode` flag; `saveon`/`saveoff` toggle it
- `save` captures 320x180 thumbnail from screen via `Raylib.LoadImageFromScreen()`
- `load` jumps to `*load_restore` label if it exists after restoring state
- `saveinfo` returns preview text, save datetime (`yyyy/MM/dd HH:mm`), and existence flag
- `yesnobox` and `mesbox` require `compat_mode on`; they auto-generate compat UI sprites
- `yesnobox` result: 1=Yes, 0=No; `mesbox` result always 1
- `systemcall` supports: `rmenu`, `autosave`, `autoload`/`load_auto`, `lookback`, `load`
- `system_button` names: `close`/`end`, `reset`, `skip`, `save`, `load`
- `scope` pushes local int/string stacks and sprite lifetime tracker; `end_scope` pops all
- `defer` instructions execute LIFO on scope exit (handled by `ExitScopesUntil`)
- `backlog_count` / `backlog_entry` access `State.TextHistory` (max 300 entries, auto-trimmed)
- `backlog_entry` replaces `\r` and `\n` with ` / `
- `cgunlock` adds to `State.UnlockedCgs` and marks persistent dirty; survives game restarts
- `gallery_info` returns title, path, and unlock status (1/0) for given index
- `getconfig` / `setconfig` keys are case-insensitive and `_` is stripped; supports aliases
- `setconfig "fullscreen", 1` calls `Vm.ToggleFullscreen()` immediately
- `setconfig "textspeed", N` updates both `State.TextSpeedMs` and `Config.Config.GlobalTextSpeedMs`
- `saveconfig` writes persistent data to `config.json`; also auto-triggered every 1000ms when dirty
- `automode_time` is registered as System category but has NO handler implementation
- `caption` uses raw argument (no string interpolation); `window_title` uses `GetString` (supports `${...}`)

**Verification:**
- All 26 System + Save category opcodes documented (21 System + 5 Save)
- `automode_time` explicitly documented as unimplemented with workaround (`setconfig "automodewait"`)
- Language: Japanese only
- File is 711 lines


## docs/reference/opcodes/character.md Created (Task 14)

**File created:** `docs/reference/opcodes/character.md`

**Coverage:**
- All 8 Compatibility category character commands from CommandRegistry.cs:
  - `char_load` — load character definitions from JSON
  - `char_show` — display character with optional expression/pose
  - `char_hide` — hide character with optional fade duration
  - `char_move` — move character to coordinates with tween
  - `char_expression` — change character expression image
  - `char_pose` — change character pose image
  - `char_z` — set character Z-order
  - `char_scale` — set character scale (uniform)

**Key findings from source code:**
- Character sprite IDs auto-allocated starting from 5000
- `char_show` image resolution priority: pose > expression > "normal" fallback
- `char_hide` with fadeDuration > 0 tweens opacity to 0 then removes sprite; duration=0 removes immediately
- `char_move` creates two tweens (X and Y) with fixed EaseOut easing
- `char_expression` and `char_pose` change `Sprite.ImagePath` immediately (no tween)
- `char_scale` applies uniform scale to both ScaleX and ScaleY
- Character data JSON schema documented inline with all fields (Id, Name, Expressions, Poses, DefaultX/Y/Scale/Z)
- `char_load` with no args loads `characters.json`; missing file is silent (warning logged)

**Verification:**
- All 8 character opcodes documented
- No non-character Compatibility commands included
- Language: Japanese only
- File is 330 lines

## docs/reference/opcodes/chapter.md Created (Task 15)

**File created:** `docs/reference/opcodes/chapter.md`

**Coverage:**
- All 12 Compatibility category chapter commands from CommandRegistry.cs:
  - `defchapter` — start chapter definition block
  - `chapter_id` — set chapter numeric ID
  - `chapter_title` — set chapter display title
  - `chapter_desc` — set chapter description text
  - `chapter_script` — set chapter script file path
  - `endchapter` — finalize definition and register with ChapterManager
  - `chapter_select` — auto-generate chapter selection UI (sprites 2000-2099)
  - `unlock_chapter` — unlock chapter by ID and save to chapters.json
  - `chapter_thumbnail` — set thumbnail path for chapter
  - `chapter_card` — manually create chapter card sprite (background + title + description)
  - `chapter_scroll` — no-op (placeholder for future scrolling)
  - `chapter_progress` — update chapter progress 0-100 and save

**Key findings from source code:**
- Chapter data persisted to `chapters.json` via `ChapterManager`
- `defchapter` creates `ChapterInfo` in `State.CurrentChapterDefinition`; `endchapter` calls `ChapterManager.AddChapter()`
- `chapter_select` clears sprites 2000-2099 and button map, then creates 3 sprites per chapter (rect + title + desc)
- `chapter_select` unlock check uses BOTH `chapter.IsUnlocked` AND `State.Flags[$"chapter{N}_unlocked"]`
- `unlock_chapter`, `chapter_thumbnail`, `chapter_progress` all auto-call `SaveChapters()`
- `chapter_progress` clamps to 0-100 range via `Math.Max(0, Math.Min(100, progress))`
- Default 3 chapters auto-created if `chapters.json` missing
- `chapter_scroll` is explicitly no-op in CompatibilityCommandHandler

**Verification:**
- All 12 chapter opcodes documented
- No non-chapter Compatibility commands included (char_*, change_scene, etc. excluded)
- Language: Japanese only
- File is 521 lines

## docs/reference/opcodes/init.md Created (Task 16)

**File created:** `docs/reference/opcodes/init.md`

**Coverage:**
- Init-only commands (5 commands):
  - `window` — window size/title, read once after init.aria execution
  - `font` — TTF font path, loaded once after init.aria
  - `font_atlas_size` — font atlas texture size, clamped 8-512
  - `font_filter` — bilinear/trilinear/point/anisotropic filtering mode
  - `script` — main script path, read once after init.aria (alias: `include`)
- Init-common runtime-capable commands (8 commands):
  - `debug` — debug overlay toggle (also F3 at runtime)
  - `compat_mode` — NScripter compat auto-UI generation toggle
  - `textbox` — text display area position/size
  - `fontsize` — default text font size
  - `textcolor` — default text color
  - `textbox_color` — textbox background color + alpha
  - `textbox_style` — textbox decoration (corner radius, border, shadow)
  - `choice_style` — choice button styling for `choice` command
- Compatibility commands linked from index.md (brief coverage):
  - `bg` / `load_bg` — background loading (detailed in sprite.md)
  - `clr` — clear all sprites (equivalent to `csp -1`)
  - `print` — no-op (unimplemented compatibility command)
  - `effect` — no-op (unimplemented compatibility command)

**Key findings from source code:**
- Truly init-only commands are those whose values are read AFTER init.aria execution in Program.cs:
  - `window` → `vm.State.WindowWidth`, `WindowHeight`, `Title` (line 140)
  - `font` → `vm.State.FontPath` (line 162)
  - `font_atlas_size` → `vm.State.FontAtlasSize` (line 164)
  - `font_filter` → `vm.State.FontFilter` (line 164)
  - `script` → `vm.State.MainScript` (line 156)
- `debug` and `compat_mode` are runtime-capable but commonly set in init.aria
- `print` and `effect` are registered in CommandRegistry.cs but their handlers are empty `return true;`
- `bg` and `load_bg` execute identical code (same switch case in RenderCommandHandler)
- `clr` clears all sprites, button map, focused button, sprite lifetime stacks, and compat UI sprites

**Cross-references:**
- Links to `../init-aria.md` in intro and related docs section
- Links to `ui.md` for textbox/font/text commands
- Links to `system.md` for debug/window_title commands
- Links to `sprite.md` for bg/clr commands

**Verification:**
- All 5 truly init-only commands documented with init-only warnings
- All 8 init-common runtime commands documented
- All commands from task's explicit list (`window`, `font`, `font_atlas_size`, `font_filter`, `script`, `debug`, `compat_mode`, `textbox`, etc.) covered
- Language: Japanese only
- File is 468 lines
