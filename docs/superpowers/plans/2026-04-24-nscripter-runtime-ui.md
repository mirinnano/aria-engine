# NScripter Runtime UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add NScripter-style engine-default save/load, right-click menu, backlog/read-skip, click cursor, window metadata, and make `00_converted.aria` the main script.

**Architecture:** Keep advanced UI configurable from Aria scripts while providing safe default engine behavior. Add lightweight state/config properties, parser opcodes, VM handlers, and MenuSystem actions without hardcoding story-specific UI.

**Tech Stack:** .NET 8, Raylib-cs, AriaEngine VM/parser/menu system.

---

### Task 1: Script Surface

**Files:**
- Modify: `src/AriaEngine/Core/OpCode.cs`
- Modify: `src/AriaEngine/Core/Parser.cs`
- Modify: `src/AriaEngine/Core/GameState.cs`

- [ ] Add opcodes for rmenu entries, cursor, backlog, read-state, skip mode, window title, and system buttons.
- [ ] Add GameState properties for configured menu entries, backlog, read labels, click cursor, and window UI toggles.

### Task 2: VM Behavior

**Files:**
- Modify: `src/AriaEngine/Core/VirtualMachine.cs`

- [ ] Implement new opcodes.
- [ ] Add backlog entries when text is shown.
- [ ] Mark labels/PC as read during execution.
- [ ] Make `end` request process/window close through GameState.

### Task 3: Engine Menu/UI

**Files:**
- Modify: `src/AriaEngine/UI/MenuSystem.cs`
- Modify: `src/AriaEngine/Input/InputHandler.cs`
- Modify: `src/AriaEngine/Rendering/SpriteRenderer.cs`
- Modify: `src/AriaEngine/Program.cs`

- [ ] Render configurable right-click menu.
- [ ] Implement save/load/backlog/skip/reset/end actions.
- [ ] Draw click-wait cursor with default fallback.
- [ ] Apply runtime title changes and window close requests.

### Task 4: Template Main

**Files:**
- Modify: `src/AriaEngine/assets/scripts/main.aria`
- Optionally create: `src/AriaEngine/assets/scripts/main.sample.aria`

- [ ] Preserve existing main as sample.
- [ ] Replace main with converted NScripter script.
- [ ] Enable rmenu and click cursor defaults.

### Task 5: Verification

- [ ] Run `dotnet build src/AriaEngine/AriaEngine.csproj -c Release --no-restore`.
- [ ] Run `scripts/cicd.ps1` with `ARIA_PACK_KEY`.
