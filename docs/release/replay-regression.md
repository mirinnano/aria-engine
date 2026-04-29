# Replay Regression

Replay regression is command-based in this phase. It runs deterministic tool or engine commands and records output.

## Spec Format

```json
{
  "cases": [
    {
      "name": "compile-main",
      "command": "dotnet run -c Release --project src/AriaEngine/AriaEngine.csproj -- aria-compile --init init.aria --main assets/scripts/main.aria --out build/replay.ariac",
      "expectedExitCode": 0
    }
  ]
}
```

## Command

```powershell
scripts/replay.ps1 -Spec artifacts/replay/replay.json
```

On failure, the script writes replay outputs and creates a diagnostics zip.
