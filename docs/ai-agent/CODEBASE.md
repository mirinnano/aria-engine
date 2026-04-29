# AriaEngine Codebase Deep Dive

This document is a technical reference for AI agents and developers who need to modify, extend, or debug AriaEngine. It assumes familiarity with the high-level architecture described in `AGENTS.md` and focuses on implementation details, data flows, and extension mechanics.

---

## Architecture at a Glance

AriaEngine is a script-driven visual novel engine. The architecture follows a classic interpreter pattern: text scripts are parsed into an instruction stream, which a virtual machine executes against a centralized game state. Rendering, audio, and input are managed as peripheral systems that consume and mutate this state.

```
+----------------+     Parse      +------------------+
|  .aria script  | --------------> | Instruction[]    |
+----------------+                 +------------------+
                                         |
                                         | LoadScript
                                         v
+----------------+     Step()      +------------------+
|   InputHandler | <--------------> |  VirtualMachine  |
+----------------+   Update(dt)    +------------------+
        |                                    |
        | Mutates                            | Reads/Writes
        v                                    v
+------------------+               +------------------+
|   GameState      | <-----------> | CommandHandlers  |
| (single source   |               | (14 handler      |
|  of truth)       |               |  classes)        |
+------------------+               +------------------+
        |                                    |
        | Reads                              | Reads
        v                                    v
+------------------+               +------------------+
| SpriteRenderer   |               | AudioManager     |
| TweenManager     |               | TransitionManager|
+------------------+               +------------------+
```

The `GameState` object is the single source of truth. Every subsystem reads from it, and only the VM (via command handlers) and `InputHandler` write to it. This design makes save/load trivial: serialize `GameState`, restore it, and the next frame renders the captured world exactly.

---

## File-by-File Breakdown

### `Core/OpCode.cs`

Defines the `OpCode` enum, which is the canonical instruction set. There are roughly 100 enum members covering 11 categories registered in `CommandRegistry.cs`. The enum spans:

- **Core**: arithmetic, jumps, subroutine control, timers, arrays, exceptions (`Add`, `Sub`, `Beq`, `Gosub`, `Return`, `Throw`, `Assert`, etc.)
- **Script**: flow control aliases, `include`, modern syntax support (`While`, `Wend`, `Break`, `Continue`, `ReturnValue`)
- **Render**: sprite lifecycle and visual properties (`Lsp`, `Csp`, `Vsp`, `Msp`, `SpAlpha`, `SpScale`, `SpRotation`, etc.)
- **Animation**: tween directives (`Amsp`, `Afade`, `Ascale`, `Acolor`, `Await`, `Ease`)
- **Input**: button and hit-area management (`Btn`, `BtnWait`, `SpBtn`, `BtnTime`)
- **Text / UI**: text window, styling, backlog, menus, modern UI components (`Textbox`, `TextSpeed`, `UiRect`, `UiButton`, `UiSlider`, etc.)
- **Audio**: BGM/SE/MP3 playback and volume (`PlayBgm`, `PlaySe`, `BgmVol`, `FadeIn`, `Dwave`, etc.)
- **System**: engine settings, save/load, gallery, config (`Save`, `Load`, `Window`, `Debug`, `GalleryEntry`, `CgUnlock`)
- **Flags**: persistent, save-bound, volatile flags and counters (`SetFlag`, `GetPFlag`, `IncCounter`, etc.)
- **Chapter / Character**: compatibility wrappers for chapter select and character systems
- **Init**: boot-time-only opcodes (`Window`, `Font`, `Script`, `Caption`)

Every opcode must be registered in `CommandRegistry.cs` with a canonical name, category, aliases, and minimum argument count. The parser resolves script tokens to opcodes via `CommandRegistry.TryGet`.

### `Core/CommandRegistry.cs`

Static registry that maps opcode names (case-insensitive) to `CommandInfo` records, and also provides a reverse lookup from `OpCode` enum value to metadata. This is the authority on what text commands are valid in `.aria` scripts. The `GetDefaultMinArgs` method encodes argument-count validation logic that `VirtualMachine.ValidateArgs` delegates to.

### `Core/Instruction.cs`

Immutable struct representing a single VM instruction. Fields:
- `Op`: the opcode enum value
- `Arguments`: tokenized string arguments (unresolved; registers and string interpolation are evaluated at runtime)
- `SourceLine`: 1-based line number in the original `.aria` file for error reporting
- `Condition`: optional conditional guard evaluated before execution
- `ScriptFile`: source file path (set during `AppendScript` for multi-file includes)
- `Scope`: storage scope hint (`Local`, `Global`, `Persistent`, `Save`, `Volatile`) for variable declarations

### `Core/Parser.cs`

The parser is a two-pass preprocessor and tokenizer. Its responsibilities are:

1. **Preprocess modern syntax**: transpiles C++-like constructs (`func`, `struct`, `enum`, `namespace`, `use`, `match`/`case`, `switch`, `try`/`catch`, `let` with `Ok`/`Err`/`Some`/`None`) into lower-level `.aria` instructions. It also handles constant substitution and namespace label rewriting.
2. **Build label table**: resolves `*label_name` declarations to instruction indices.
3. **Generate instructions**: converts each line into one or more `Instruction` structs. Inline text (e.g., `Character「dialogue」`) is auto-expanded into `textclear`, text instructions, and wait commands.
4. **Control flow flattening**: block-level `if`/`else`/`endif`, `while`/`wend`, `for`/`next` are compiled into conditional jumps (`JumpIfFalse`, `Jmp`) with synthetic labels.
5. **Validation**: post-parse checks for undefined jump targets, unclosed blocks, readonly reassignment, and function call type consistency.

The parser returns a `ParseResult` containing the instruction list, label dictionary, function signatures, struct definitions, enum definitions, owned-sprite declarations, and explicit variable declarations.

### `Core/VirtualMachine.cs`

The VM is the heart of the engine. It maintains:
- `_instructions`: the current script's instruction array
- `_labels`: label name -> program counter mapping
- `State`: the `GameState` singleton
- `_handlerTable`: an array-indexed dispatch table mapping `OpCode` values to `ICommandHandler` instances
- `_fastRegisters`: a fixed-size `int[10]` cache for `%0`-`%9` to avoid dictionary lookups
- `_stringCache`: interned string literals to reduce GC pressure

**Execution model**: `Step()` runs a bounded number of instructions per frame (500 by default, unlimited during skip mode). For each instruction, it evaluates the condition, dispatches to the handler table, and increments the PC. If the PC runs off the end, the VM transitions to `VmState.Ended`.

**Scope management**: the VM supports function-local scopes via `PushFunctionScope`/`PopFunctionScope`. Local integer and string registers are stored in stacks of dictionaries (`LocalIntStacks`, `LocalStringStacks`). Sprites created within a scope are tracked in `SpriteLifetimeStacks` and auto-removed when the scope exits. Explicit `scope` / `end_scope` blocks use `ScopeFrame`, which also supports deferred instruction execution (LIFO) on scope exit.

**State mutation helpers**: `SetReg`, `GetReg`, `GetVal`, `GetFloat`, `SetStr`, `GetString` provide register resolution with local-scope priority, fast-path `%0`-`%9`, and string interpolation (`${...}`) support. `GetString` also evaluates simple expressions inside interpolations.

**Save/Load**: `SaveGame` captures a thumbnail and persists `GameState` via `SaveManager`. `LoadGame` restores state and jumps to `*load_restore` if the label exists, otherwise normalizes UI state and continues.

**Subsystems owned by VM**:
- `TweenManager` (animations)
- `SaveManager` / `ConfigManager`
- `MenuSystem`
- `SpritePool`
- `UiThemeManager`, `CompatUiManager`, `SkipModeManager`
- `ChapterManager`, `GameFlowManager`, `CharacterManager`
- `FunctionTable`, `StructManager`, `NamespaceManager`
- `VersionManager`, `AriaCheck`

### `Core/GameState.cs`

A flat facade over 16 specialized state objects. Rather than one enormous class, `GameState` uses nested sealed classes grouped by domain:

| Nested Class | Responsibility |
|--------------|----------------|
| `RegisterState` | Integer registers, string registers, arrays |
| `VmExecutionState` | PC, call stack, loop stack, local scope stacks, delay timer, try stack |
| `InteractionState` | Button timeout, sprite-button map, focused button ID |
| `RenderState` | Sprite dictionary, fade/quake state |
| `AudioState` | Current BGM, pending SE list, volumes, fade timers |
| `TextWindowState` | Textbox position, size, padding, colors, visibility |
| `ChoiceStyleState` | Default styling for choice buttons |
| `TextRuntimeState` | Typewriter buffer, displayed length, speed, history backlog, read keys |
| `PlaybackControlState` | Auto-mode, skip-mode, force-skip, timing |
| `MenuRuntimeState` | Right-click menu entries, system button visibility, menu dimensions |
| `UiRuntimeState` | Window close/reset requests, click cursor config, MSAA |
| `UiCompositionState` | UI groups, layouts, anchors, events, hotkeys, hover tracking |
| `EngineSettingsState` | Window dimensions, title, font path, atlas size, main script path |
| `UiQualityState` | Quality mode, smooth motion, rounded-rect segments, motion response |
| `SceneRuntimeState` | Current `GameScene` enum, transition flag, scene data dictionary |
| `SaveRuntimeState` | Total play time, session start, current chapter, progress |
| `FlagRuntimeState` | Flags (persistent/save/volatile), counters, CG gallery entries |

`GameState` exposes flat properties that proxy into these nested objects, keeping the rest of the codebase simple while allowing domain-specific serialization and reset logic.

Notable types:
- `FastSpriteDictionary`: the sprite container (keyed by `int` ID)
- `BacklogEntry` / `BacklogStateSnapshot`: supports jumping back to prior text with full state restoration
- `ScopeFrame`: tracks local variables, sprite IDs, and deferred instructions for explicit scope blocks

### `Core/Sprite.cs`

Defines the `Sprite` class, a unified visual entity with three `SpriteType` variants: `Image`, `Text`, `Rect`. Sprites are render-agnostic bags of properties; the renderer interprets them.

**Core transform properties**: `X`, `Y`, `Z`, `ScaleX`, `ScaleY`, `Rotation`, `Opacity`, `Visible`

**Type-specific properties**:
- `Image`: `ImagePath`
- `Text`: `Text`, `FontSize`, `Color`, `TextAlign`, `TextVAlign`, plus decoration (`TextShadowColor`, `TextOutlineColor`, `TextEffect`, etc.)
- `Rect`: `Width`, `Height`, `FillColor`, `FillAlpha`, plus decoration (`CornerRadius`, `BorderColor`, `GradientTo`, `ShadowColor`, etc.)

**Interactivity properties**: `IsButton`, `ClickAreaX/Y/W/H`, `HoverFillColor`, `HoverScale`, `IsHovered`, `Cursor`

**Widget properties**: `SliderMin/Max/Value/TrackColor/FillColor/ThumbColor`, `CheckboxValue`, `CheckboxLabel`

**Runtime render state** (json-ignored): `RenderScaleX/Y`, `RenderOpacity`, `HoverProgress`, `RenderStateInitialized`. These are updated by `SpriteRenderer.UpdateUiPresentation` each frame to drive smooth hover and opacity transitions independently of the script-facing properties.

### `Rendering/SpriteRenderer.cs`

The renderer consumes `GameState` each frame and draws all visible sprites in Z-order. Key internals:

- **LRU texture cache**: `_textureCache` stores loaded `Texture2D` objects with a byte-size budget. Evicted textures are unloaded from GPU memory.
- **Font management**: Loads a primary font via `LoadFontEx` with a dynamic codepoint set derived from the script text. Supports size-specific font caches and a separate UI font (`JosefinSans-Thin.ttf`).
- **Color cache**: `_colorCache` avoids re-parsing hex strings every frame.
- **Render lock**: `lock (_renderLock)` around `Draw()` because live reload may invalidate textures from another thread.

Per-frame `Draw()` flow:
1. Cache `GameState` reference for the frame.
2. Run `UpdateUiPresentation` to smooth-hover-scale and opacity lerp.
3. Collect visible sprites, sort by `Z`.
4. Compute quake offset if active.
5. For each sprite:
   - `Image` -> resolve/load texture, apply scale/rotation, draw with `DrawTexturePro`
   - `Rect` -> draw shadow, fill (solid/gradient/rounded), border
   - `Text` -> wrap text, measure, align, draw with shadow/outline/effects
6. Draw transition overlay.
7. Draw debug overlay (FPS, PC, sprite count, cache stats) if `DebugMode` is on.

Text rendering has two paths:
- **Plain**: single-style text with character-level wrapping for typewriter accuracy
- **Segmented**: multi-style text with per-segment fonts, colors, bold (outline simulation), underline, strikethrough, fade, and shake effects

### `Rendering/TweenManager.cs`

Simple tween system. `Tween` objects specify a `SpriteId`, `TweenProperty` (`X`, `Y`, `ScaleX`, `ScaleY`, `Opacity`), `From`/`To` values, `DurationMs`, and `EaseType` (`Linear`, `EaseIn`, `EaseOut`, `EaseInOut`).

`Update` iterates backwards over `_activeTweens`, advances elapsed time, applies eased values directly to the target sprite, and removes completed tweens. `FinishAll` snaps every tween to its end value immediately. The VM blocks on `Await` until `IsAnimating` is false.

### `Input/InputHandler.cs`

Processes all user input every frame and mutates VM state directly:

- **Debug keys**: F3 toggles debug overlay; F5 quick-saves to slot 0; F9 quick-loads.
- **Skip**: Ctrl held -> `ForceSkipMode`; advance key during skip -> stops skip.
- **Hotkeys**: evaluates `UiHotkeys` dictionary against `Raylib.IsKeyPressed`.
- **WaitingForClick**: advance key completes typewriter text instantly, or clears page wait and resumes VM.
- **WaitingForAnimation**: advance key may fast-forward remaining text.
- **WaitingForButton**: performs hit-test against all `IsButton` sprites using click-area rectangles. Supports mouse hover, click, keyboard focus navigation (arrow keys/tab), and keyboard activation (enter/space). Also handles slider drag and checkbox toggle.

`MoveButtonFocus` sorts focusable buttons by Y, then X, then Z, then ID, and cycles focus.

### `Audio/AudioManager.cs`

Manages two LRU caches: `_bgmCache` (max 8) and `_seCache` (max 16). On `Update`:

1. If `state.CurrentBgm` changed, stop old music, load new music from `IAssetProvider`, play stream.
2. Update current BGM stream volume (including fade-out multiplier), call `UpdateMusicStream`.
3. Drain `PendingSe` queue: load sounds, play at `SeVolume`.

`PlayVoice` is a direct SE play used by backlog voice replay. `Unload` stops and disposes all cached audio.

### `Program.cs`

Entry point and main loop. Responsibilities:

1. Parse CLI args (`--run-mode`, `--pak`, `--key`, `--compiled`, `--init`, `--bytecode`).
2. Create `IAssetProvider`: `PakAssetProvider` for release mode, `DiskAssetProvider` for dev.
3. Attempt to load compiled/encrypted script bundle if in release mode.
4. Parse and execute `init.aria` inside a temporary VM step loop to set window size, font, etc.
5. Initialize Raylib window and audio.
6. Load main script, load fonts, initialize renderer, input, audio, transition manager.
7. Register dynamic include resolver on the VM.
8. Enable `LiveReloadManager` in dev mode with disk provider.
9. Main loop:
   - `liveReload.Update()`
   - `vm.Update(dtMs)`
   - `input.Update(vm)`
   - `menu.Update()`
   - `audio.Update(state)`
   - `transition.Update(vm, dt)`
   - `tweens.Update(state, dtMs)`
   - `vm.ProcessSkipFrame` or `vm.Step()`
   - `BeginDrawing` -> `renderer.Draw` -> `renderer.DrawClickCursor` -> `menu.Draw` -> `EndDrawing`
10. Graceful shutdown: save persistent state, write error log, unload renderer/audio, close Raylib.

All frame callbacks are wrapped in `SafeFrame`, which suppresses repeated errors (logs only on 1st, 60th, and 600th occurrence) to prevent log spam.

---

## Data Flow Diagrams

### Script Load & Execution Flow

```
Disk / Pak
   |
   v
ScriptLoader.LoadScript(path)
   |-> reads raw text
   v
Parser.Parse(lines)
   |-> preprocess modern syntax
   |-> tokenize & build instructions
   |-> validate labels & blocks
   v
ParseResult
   | Instructions[]
   | Labels{ name -> index }
   | Functions[], Structs[], Enums[]
   v
VirtualMachine.LoadScript(result, file)
   |-> stores instructions & labels
   |-> registers functions/structs/enums
   |-> resets PC to 0, state = Running
   v
VM.Step() per frame
   |-> evaluate condition
   |-> dispatch via _handlerTable[opcode]
   |-> command handler mutates GameState
   v
GameState
   |-> sprites updated
   |-> audio state updated
   |-> text buffer updated
```

### Frame Update Flow

```
Raylib.GetFrameTime()
   |
   v
+-------------------+
| VM.Update(dtMs)   |
| - quake timer     |
| - typewriter tick |
| - delay timer     |
| - auto-mode timer |
| - tween completion|
+-------------------+
   |
   v
+-------------------+
| InputHandler      |
| - keyboard/mouse  |
| - button hit-test |
| - resume VM state |
+-------------------+
   |
   v
+-------------------+
| MenuSystem.Update |
| - open/close logic|
+-------------------+
   |
   v
+-------------------+
| AudioManager      |
| - BGM stream mgmt |
| - SE playback     |
+-------------------+
   |
   v
+-------------------+
| TransitionManager |
| - fade in/out     |
+-------------------+
   |
   v
+-------------------+
| TweenManager      |
| - active tweens   |
+-------------------+
   |
   v
VM.Step() (if Running)
   |
   v
Render Phase
```

### Render Pipeline Flow

```
SpriteRenderer.Draw(state, transition)
   |
   +-> UpdateUiPresentation(state)
   |      +-> lerp RenderScaleX/Y toward ScaleX/Y * hoverScale
   |      +-> lerp RenderOpacity toward Opacity
   |      +-> lerp HoverProgress toward IsHovered ? 1 : 0
   |
   +-> Collect visible sprites -> List<Sprite>
   +-> Sort by Z ascending
   |
   +-> Compute quake offset (qx, qy)
   |
   +-> For each sprite in Z order:
   |      Image  -> GetOrLoadTexture -> DrawTexturePro
   |      Rect   -> Draw shadow -> fill -> border
   |      Text   -> WrapText -> Measure -> Align -> DrawTextEx
   |                           (with shadow/outline/effects)
   |
   +-> transition.Draw(state)
   +-> DrawDebugInfo(state) [if DebugMode]
```

---

## Key Abstractions and Their Relationships

### `ICommandHandler` / `BaseCommandHandler`

All VM-side opcode behavior is implemented in command handlers that inherit `BaseCommandHandler`. The VM builds a `_handlerTable` array at startup by iterating over all handlers and their `HandledCodes`. This means:

- **Dispatch is O(1)**: `int index = (int)inst.Op; _handlerTable[index].Execute(inst)`
- **Handlers are stateless**: they read/write `GameState` via the VM reference
- **A handler can cover multiple opcodes**: the `switch` inside `Execute` routes to the correct behavior

There are 14 handler classes: `CoreCommandHandler`, `ScriptCommandHandler`, `InputCommandHandler`, `RenderCommandHandler`, `SpriteDecoratorCommandHandler`, `TweenCommandHandler`, `TextCommandHandler`, `UiCommandHandler`, `SaveCommandHandler`, `FlowCommandHandler`, `AudioCommandHandler`, `SystemCommandHandler`, `FlagCommandHandler`, `CompatibilityCommandHandler`.

### `Condition` / Expression Evaluation

Instructions can carry a `Condition`. The modern parser emits `Condition` objects with an `Expression` AST (for complex expressions) or a legacy `ConditionTerm` list (for simple comparisons). `VirtualMachine.EvaluateCondition` evaluates the AST first, falling back to term-based evaluation. The result is a boolean; if false, the instruction is skipped.

### `IAssetProvider`

Abstraction over asset loading. Two implementations:
- `DiskAssetProvider`: reads from filesystem relative to working directory
- `PakAssetProvider`: reads from an encrypted `.pak` archive

Both provide `OpenRead`, `Exists`, and `MaterializeToFile`. The renderer and audio manager use `MaterializeToFile` to get a real filesystem path for Raylib APIs that require file paths rather than streams.

### `ErrorReporter`

Central error collection. All subsystems report `AriaError` objects with a level (`Info`, `Warning`, `Error`, `Fatal`), code, message, source line, file, details, and hint. The reporter batches messages and writes them to `aria_error_ai.txt` on shutdown. `SafeFrame` uses it to suppress repetitive frame errors.

### `TransitionManager`

Handles full-screen fade-in/fade-out effects by drawing a colored rectangle over the entire screen with alpha based on `FadeProgress`. It reads `GameState.IsFading` and `FadeDurationMs` to drive the animation.

---

## Extension Guide: Adding a New Opcode

To add a new engine capability exposed to `.aria` scripts, follow these steps:

### 1. Add the enum member

Open `Core/OpCode.cs` and add a new member to the enum. Place it near related opcodes for readability.

```csharp
public enum OpCode
{
    // ... existing members ...
    SpFlipHorizontal,  // new
}
```

### 2. Register the command name

Open `Core/CommandRegistry.cs` and register the canonical name and aliases in the static constructor.

```csharp
Register(CommandCategory.Render, OpCode.SpFlipHorizontal, "sp_flip_h");
```

If the opcode requires arguments, add a case to `GetDefaultMinArgs`:

```csharp
OpCode.SpFlipHorizontal => 1,  // sprite id
```

### 3. Implement the handler logic

Choose the appropriate `BaseCommandHandler` subclass. For sprite properties, `RenderCommandHandler` or `SpriteDecoratorCommandHandler` are typical. Add the opcode to the handler's `HandledCodes` set, then add a `case` in `Execute`.

Example in `RenderCommandHandler.cs`:

```csharp
public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
{
    // ... existing codes ...
    OpCode.SpFlipHorizontal
};

public override bool Execute(Instruction inst)
{
    switch (inst.Op)
    {
        // ... existing cases ...
        case OpCode.SpFlipHorizontal:
            if (!ValidateArgs(inst, 1)) return true;
            {
                int id = GetVal(inst.Arguments[0]);
                if (State.Sprites.TryGetValue(id, out var sp))
                {
                    sp.ScaleX = -sp.ScaleX;  // or add a dedicated FlipX property
                }
            }
            return true;
    }
    return true;
}
```

Use `ValidateArgs` to enforce minimum argument counts. Use `GetVal` for integer/register arguments, `GetString` for string arguments, and `GetFloat` for fractional values.

### 4. Update the parser (if introducing new syntax)

If the opcode uses a new top-level keyword that does not match the existing `CommandRegistry` name, ensure the parser recognizes it. Usually this is automatic because the parser's main dispatch loop calls `CommandRegistry.TryGet(firstToken, out OpCode)`. However, if the syntax is irregular (e.g., a new inline shorthand or a block statement), you may need to add special handling in `Parser.cs`.

### 5. Rebuild and test

Run `dotnet build` and exercise the new command in a test script. Use debug mode (F3) to confirm the instruction is reached by observing the program counter.

---

## Extension Guide: Adding a New Sprite Property

To add a visual property that `.aria` scripts can manipulate on sprites:

### 1. Add the property to `Sprite.cs`

```csharp
public class Sprite
{
    // ... existing properties ...
    public float SkewX { get; set; } = 0f;
    public float SkewY { get; set; } = 0f;
}
```

If the property is purely runtime (not serialized in save files), mark it with `[JsonIgnore]`.

### 2. Add an opcode to set the property

Follow the "Adding a New Opcode" guide above. Create a setter opcode:

```csharp
// OpCode.cs
SpSkew,

// CommandRegistry.cs
Register(CommandCategory.Render, OpCode.SpSkew, "sp_skew");
// GetDefaultMinArgs:
OpCode.SpSkew => 3,  // id, skewX, skewY

// RenderCommandHandler.cs
case OpCode.SpSkew:
    if (!ValidateArgs(inst, 3)) return true;
    {
        int id = GetVal(inst.Arguments[0]);
        if (State.Sprites.TryGetValue(id, out var sp))
        {
            sp.SkewX = GetFloat(inst.Arguments[1], inst);
            sp.SkewY = GetFloat(inst.Arguments[2], inst);
        }
    }
    return true;
```

### 3. Teach the renderer to use the property

Open `Rendering/SpriteRenderer.cs` and modify the relevant draw method.

For `Image` sprites, `DrawTexturePro` accepts an `origin` and `rotation` but not skew. To implement skew, you would need to decompose the sprite into a quad and apply a custom transform. A simpler approach for many properties is to adjust the draw rectangle or source rectangle.

If the property affects layout (e.g., a new text margin), update `DrawTextSprite` or `GetTextEndCursorPosition` accordingly.

### 4. Add tween support (optional)

If the property should be animatable, extend `TweenProperty` in `TweenManager.cs`:

```csharp
public enum TweenProperty
{
    // ... existing ...
    SkewX,
    SkewY
}
```

Update `ApplyValue` in `TweenManager`:

```csharp
case TweenProperty.SkewX: sp.SkewX = value; break;
case TweenProperty.SkewY: sp.SkewY = value; break;
```

Then add a corresponding animation opcode (e.g., `Askew`) in the same pattern as `Amsp`/`Ascale`.

### 5. Update save/load compatibility

If the property is serialized, ensure `BacklogStateSnapshot` and any manual sprite-copying code (e.g., in `CaptureBacklogSnapshot`) copies the new field. `GameState` is serialized via `System.Text.Json`; public auto-properties on `Sprite` are captured automatically unless marked `[JsonIgnore]`.

### 6. Update normalization code

If the property is part of runtime-generated UI sprites (textbox, text target), update `NormalizeRuntimeTextSprites` in `VirtualMachine.cs` to set the default value.

---

## Rendering Pipeline Deep Dive

The rendering pipeline is single-threaded with one exception: live reload may call `InvalidateTexture` from a background file-watcher thread, which acquires `_renderLock`. The main thread holds this lock during the entire `Draw()` call.

### Z-Order and Sorting

Sprites are stored in `FastSpriteDictionary` (an `int`-keyed dictionary). Each frame, `SpriteRenderer` allocates a `List<Sprite>` of visible sprites and sorts them with `List.Sort` using `Z` ascending. There is no spatial index; hit-testing in `InputHandler` iterates over all sprites.

### Texture Lifecycle

Textures are loaded lazily on first draw. The renderer tries the LRU cache first; on miss, it calls `IAssetProvider.MaterializeToFile` to get a path, loads via `Raylib.LoadTexture`, applies a texture filter (bilinear or trilinear based on quality settings), and inserts into the cache. Failed loads are tracked in `_failedTextures` to avoid repeated error spam.

The cache eviction callback (`OnTextureEvicted`) calls `Raylib.UnloadTexture`, which frees GPU memory. The cache has both an item count limit and a byte-size limit based on texture dimensions.

### Font Lifecycle

The primary font is loaded with `Raylib.LoadFontEx`, passing an array of codepoints gathered from the script source lines plus common ASCII and Japanese punctuation. This ensures the font atlas contains every glyph the game might display. For high-quality rendering with bilinear filtering, the atlas is generated at 2x the requested size and downsampled by Raylib.

Size-specific fonts are cached in `_fontCache`. When a text sprite requests a size that isn't cached, `LoadSizedFont` generates a new atlas. The UI font uses a smaller codepoint set (ASCII only) for menu text.

### Pixel Snapping vs Subpixel

`SnapPixel` rounds coordinates to integers unless `GameState.SubpixelUiRendering` is enabled. This prevents text and UI elements from appearing blurry on non-fractional positions. The quake effect bypasses snapping to allow subpixel shaking.

### Hover and Animation Smoothing

`UpdateUiPresentation` runs before drawing. For every sprite, it lerps `RenderScaleX/Y` toward the target scale (including hover multiplier) and `RenderOpacity` toward `Opacity`. The blend factor is derived from frame time and `UiMotionResponse`. This means scripts can set properties instantly, but the visual result is always smooth.

### Typewriter Accuracy

The typewriter effect increments `DisplayedTextLength` in `VM.Update`. The renderer then truncates `sp.Text` to this length. Because text wrapping depends on font metrics, the renderer must re-wrap the full buffer every frame even though only a prefix is shown. `SpriteRenderer` caches the text-end cursor position to avoid re-measuring the entire wrapped string for the click-cursor sprite.

---

## Conventions and Patterns

- **Case-insensitive everywhere**: script tokens, label names, register names, and command names are all compared with `StringComparer.OrdinalIgnoreCase`.
- **Register normalization**: `RegisterStoragePolicy.Normalize` strips `%` and lowercases names. `%0`-`%9` use a fast array path.
- **String interpolation**: `${$name}` resolves string registers; `${%name}` resolves integer registers; `${expression}` evaluates a mini expression.
- **Error codes**: every reported error has a PascalCase code (e.g., `VM_ARG_MISSING`, `RENDER_TEXTURE_LOAD`) for greppability.
- **Scope safety**: sprites created inside `func` or explicit `scope` blocks are auto-cleaned on exit. Use `TrackSpriteLifetime` in handlers when creating sprites dynamically.
- **Handler return value**: `Execute` returns `true` to indicate the instruction was handled (even if it failed validation). Returning `false` is not currently used for control flow.
