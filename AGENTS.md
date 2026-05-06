# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Build and Run

```bash
# Build the project
dotnet build

# Run the engine (from src/AriaEngine directory)
cd src/AriaEngine
dotnet run

# Run with specific working directory (if not in AriaEngine folder)
dotnet run --project src/AriaEngine/AriaEngine.csproj
```

The engine requires:
- `.NET 8.0` SDK
- `init.aria` file in the working directory (engine configuration and initialization)
- `config.json` for user settings (auto-generated if missing)
- Font file at path specified in init.aria
- Main script file at path specified in init.aria

## Architecture Overview

AriaEngine is a visual novel game engine built with .NET 8.0 and Raylib. It uses a custom scripting language (`.aria` files) with NScripter-like syntax.

### Core Components

**Parser** (`Core/Parser.cs`): Converts `.aria` script files into a list of `Instruction` objects. Supports NScripter-compatible commands, labels (`*name`), subroutines (`defsub`), conditional execution (`if`), and inline control flow (`\` for page clear, `@` for wait).

**VirtualMachine** (`Core/VirtualMachine.cs`): Executes parsed instructions. Maintains `GameState` containing registers, sprites, call stack, and engine state. Handles control flow, sprite operations, audio commands, text rendering, and input waiting states.

**GameState** (`Core/GameState.cs`): Central state container including:
- Registers (`%0`, `%1`, etc.) and string registers (`$name`)
- Sprite dictionary (all visual elements)
- VM state (Running, WaitingForClick, WaitingForButton, etc.)
- Textbox/text display state
- Audio state

**Sprite** (`Core/Sprite.cs`): Represents visual elements. Types: `Image`, `Text`, `Rect`. Supports extensive properties: position, scale, rotation, opacity, z-order, decoration (borders, shadows, gradients), button behaviors, and hover effects.

**SpriteRenderer** (`Rendering/SpriteRenderer.cs`): Renders sprites using Raylib. Handles text wrapping, font loading, color parsing, and applies quake/transition effects. Sprites are rendered in Z-order.

**InputHandler** (`Input/InputHandler.cs`): Processes mouse/keyboard input, triggers VM state changes (click resumption, button selection), handles right-click menu, and F3 debug toggle.

**AudioManager** (`Audio/AudioManager.cs`): Manages BGM/SE playback using Raylib audio.

**TweenManager** (`Rendering/TweenManager.cs`): Interpolates sprite properties over time (position, opacity, scale, color) with easing functions.

### Script Language

The `.aria` scripting language supports NScripter-compatible syntax, with v2 strict extensions:

**v1.x (Compatibility Mode)**:
- Labels: `*label_name`
- Subroutines: `defsub name` ... call via `name` or `gosub *name`
- Control flow: `if %0 == 1 command`, `goto *label`, `beq *label`
- Text: `text "content"`, or inline: `Character「message」` (auto-inserts textclear and page wait)
- Text control: `\` = wait & clear page, `@` = wait only
- Variables: `%0`-`%9` for integers, `$name` for strings
- Sprites: `lsp id, "path", x, y`, `vsp id, on/off`, `msp id, x, y`

**v2 strict (`# aria-version: 2.0` + `strict on`)**:
- Type-safe registers: `%` (int), `$` (string), `@` (sprite, new), `&` (flag, new)
- Function definitions: `func name(params) -> return_type` / `endfunc`
- Scope-based resource management: `scope "name"` / `end_scope` with `owned sprite`
- Ownership model: `owned` (auto-drop), `borrow` (temporary), `move` (transfer)
- Mutability: `readonly` / `mut` / `local` / `global` / `persistent` / `volatile`
- Structuring: `func` / `namespace` / `struct` / `enum`
- Static analysis via `aria-lint` with error codes E001-E012

See `Core/OpCode.cs` for the complete command set (~100 opcodes including sprites, animations, audio, UI, scope management, and system commands).

### Engine Initialization

1. Parse and execute `init.aria` (sets window size, font, main script path, default textbox config)
2. Initialize Raylib window and audio device
3. Parse main script file
4. Load font with character set from script
5. Enter main game loop

### Main Loop (Program.cs)

```
while (!WindowShouldClose):
    Update(deltaTime)
        - VM processes delays, timers, text typewriter effect, auto-mode
        - InputHandler processes user input
        - AudioManager updates playback
        - TransitionManager updates screen transitions
        - TweenManager updates animations
    Step VM if in Running state
    Render:
        - Clear screen
        - Draw sprites in Z-order (SpriteRenderer)
        - Draw transition overlay
        - Draw debug info if enabled
```

### Directory Structure

```
src/AriaEngine/
├── Core/           # VM, parser, opcodes, state, sprites, errors, config, save/load
├── Rendering/      # Sprite rendering, transitions, tweens
├── Input/          # Input handling
├── Audio/          # Audio playback
├── Tools/          # CLI tools (aria-lint, aria-compile, aria-pack, etc.)
├── assets/
│   ├── fonts/      # TTF font files
│   ├── bg/         # Background images
│   ├── ch/         # Character sprites
│   └── scripts/    # .aria script files (main.aria, scenario_01-06, UI scripts)
├── init.aria       # Engine initialization script
├── config.json     # User settings (auto-generated)
└── Program.cs      # Entry point, splash screen, and main loop
```

### Static Analysis & Linting

`aria-lint` provides static analysis for `.aria` scripts with error codes E001-E012 and warnings W001-W008:

- Type checking (int/string/sprite/flag)
- Ownership tracking (owned/borrow/move)
- Sprite lifetime analysis (scope exit detection, double-drop)
- Readonly enforcement and undefined variable detection
- Unreachable code and unused variable detection

Run: `dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-lint <path/to/script.aria>`

See `docs/spec/aria-v2-strict.md` for the complete v2 strict specification.

### Debug Mode

Press `F3` to toggle debug mode, which displays:
- FPS counter
- Program counter (current instruction index)
- Sprite count
- Button hit areas (red outlines)
