# Save Migration

## Current Schemas

- Normal save: `AriaSave` version `3`
- Persistent save: `PersistentGameData.SchemaVersion` version `2`
- Config: `AppConfig.SchemaVersion` version `1`

## Command

```powershell
dotnet run -c Release --project src/AriaEngine/AriaEngine.csproj -- aria-save migrate
```

Use `--dir <path>` to validate or migrate an explicit save directory:

```powershell
dotnet run -c Release --project src/AriaEngine/AriaEngine.csproj -- aria-save --dir .tmp/release-save-validation validate
```

The command:

- loads existing save slots
- backs up original files under `saves/migration-backup/<timestamp>/`
- rewrites saves in the current packed format
- writes `aria_error.log` if migration errors occur

Use `--no-backup` only for disposable test saves.

## Release Rule

Any save schema change requires:

- migration code
- backup behavior
- validation with `aria-save validate`
- release note entry
