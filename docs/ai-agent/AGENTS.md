# AGENTS.md

This file provides guidance to AI agents (Codex and Claude Code) when working with code in this repository.

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

AriaEngine is a visual novel game engine built with .NET 8.0 and Raylib. It uses a custom scripting language (`.aria` files) with NScripter-compatible syntax.

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

The `.aria` scripting language supports NScripter-compatible syntax:

- Labels: `*label_name`
- Subroutines: `defsub name` ... call via `name` or `gosub *name`
- Control flow: `if %0 == 1 command`, `goto *label`, `beq *label`
- Text: `text "content"`, or inline: `Character「message」` (auto-inserts textclear and page wait)
- Text control: `\` = wait & clear page, `@` = wait only
- Variables: `%0`-`%9` for integers, `$name` for strings
- Sprites: `lsp id, "path", x, y`, `vsp id, on/off`, `msp id, x, y`

The complete command set is defined in `Core/OpCode.cs` and registered in `Core/CommandRegistry.cs`. The engine supports **101 opcodes** across categories: Core, Script, Render, Text, Input, Audio, Save, Flags, System, Ui, and Compatibility.

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
├── assets/
│   ├── fonts/      # TTF font files
│   ├── bg/         # Background images
│   ├── ch/         # Character sprites
│   └── scripts/    # .aria script files
├── init.aria       # Engine initialization script
├── config.json     # User settings (auto-generated)
└── Program.cs      # Entry point and main loop
```

### Debug Mode

Press `F3` to toggle debug mode, which displays:
- FPS counter
- Program counter (current instruction index)
- Sprite count
- Button hit areas (red outlines)

## Documentation Structure

For detailed documentation, see:

- **[Scripting Reference](docs/scripting/)**: Syntax, control structures, sprite operations
- **[API Documentation](docs/api/)**: Complete opcode reference
- **[Architecture Docs](docs/architecture/)**: Technical details on VM, parser, rendering
- **[Tutorials](docs/tutorials/)**: Getting started, UI creation, chapter systems
