# v1.0.0 Cleanup Learnings

## Conventions
- Use `_logger.LogDebug()` for development logging (not Console.WriteLine)
- CLI progress output should go to `Console.Error` or use a proper progress API
- Empty catch blocks must at minimum log the error
- TODO comments should have Issue numbers or be resolved

## Decisions
- TBD as work progresses

## Console.WriteLine / Debug.WriteLine Cleanup (2026-04-29)

### Files Modified
- `Core/ExpressionParser.cs` — Removed `Debug.WriteLine` in catch block (debug artifact)
- `Core/ErrorReporter.cs` — Removed `Console.WriteLine` calls in `Report()` (errors already written to log files)
- `Core/Commands/RenderCommandHandler.cs` — Removed `Console.WriteLine` for `OpCode.Print` (debug artifact)
- `Tools/AriaPackCommand.cs` — Redirected verbose progress and result diagnostics to `Console.Error`
- `Tools/AriaLintCommand.cs` — Redirected verbose stats and summary to `Console.Error`; kept lint issues on stdout
- `Tools/AriaFormatCommand.cs` — Redirected file-not-found error and formatted confirmation to `Console.Error`; kept formatted output on stdout
- `Tools/AriaBytecodeCompileCommand.cs` — Redirected compile progress, stats, and warnings to `Console.Error`; kept help text on stdout
- `Tools/AriaDocCommand.cs` — Redirected errors and output-path diagnostics to `Console.Error`; kept usage on stdout
- `Tools/AriaCompileCommand.cs` — Redirected compile-failed message and stats to `Console.Error`

### Classification Rules Applied
- **Core/**, **Rendering/**, **Input/**, **Audio/**: Pure debug prints → removed entirely
- **Tools/** CLI commands:
  - Help text (`--help`, usage) → legitimate primary output, keep on stdout
  - Tool result data (lint issues, formatted script) → primary output, keep on stdout
  - Progress, verbose diagnostics, stats, warnings, errors → redirect to `Console.Error`

### Verification
- `dotnet build src/AriaEngine`: 0 warnings, 0 errors
- `dotnet test src/AriaEngine.Tests`: 180/180 passed
- Remaining `Console.WriteLine` in production code: only CLI help text and primary tool output
- Remaining in test files: intentionally preserved

## TODO/FIXME Resolution in OptimizedBytecodeVM.cs (2026-04-29)

### Files Modified
- VM/OptimizedBytecodeVM.cs ? Implemented 5 TODO opcodes via event firing

### Opcodes Implemented
- TextClear (0x61) ? Added OnTextClear event; fires when opcode executes
- WaitClick (0x62) ? Added OnWaitClick event; fires when opcode executes
- WaitClickClear (0x63) ? Chains OnTextClear then OnWaitClick
- Save (0xC0) ? Added OnSave event; fires when opcode executes
- Load (0xC1) ? Added OnLoad event; fires when opcode executes

### Design Pattern
- VM stays stateless and signal-only (fires events, does not manage game state)
- Game engine hooks OnSave/OnLoad/OnWaitClick to trigger actual save/load/wait behavior
- Consistent with existing events like OnText, OnSpriteLoad, OnBGMPlay etc.

### Verification
- dotnet build src/AriaEngine: 0 warnings, 0 errors
- dotnet test src/AriaEngine.Tests: 180/180 passed
