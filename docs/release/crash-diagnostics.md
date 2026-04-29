# Crash Diagnostics

## Outputs

Crash diagnostics are written as:

```text
diagnostics/aria-diagnostics-<timestamp>.zip
```

The zip contains:

- `summary.json`
- `aria_error.log`
- `aria_error_ai.txt`
- `aria_error_ai.json`
- `config.json`
- `saves/`

## Manual Collection

```powershell
scripts/diagnostics.ps1
```

## Release Rule

Any fatal startup or runtime exception must produce a diagnostics zip before the process exits.
