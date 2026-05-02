# Installer

The installer artifact includes both a portable PowerShell installer and a simple GUI installer.

## Build

```powershell
scripts/package.ps1 -Version <version>
scripts/installer.ps1 -Version <version> -PackageDir artifacts/release/AriaEngine-<version>-portable/app
```

## Output

```text
artifacts/installer/AriaEngine-<version>-installer.zip
```

The zip contains:

- `AriaInstaller.exe`
- `app/`

## GUI Installer

Run:

```text
gui/AriaInstaller.exe
```

The GUI can:

- install or update by copying bundled `app/`
- apply a `.patch` to installed `data.pak`
- show `In Progress` while the operation is running
- use the same package from a downloaded zip or DVD media

Default install target:

```text
%ProgramFiles%\ponkotusoft\umikaze
```

The target can be changed before starting, but it is locked while install/update is in progress. The GUI requests administrator elevation automatically when needed for Program Files.

The bundled `app/` payload excludes development-only files such as PDBs, logs, temporary scripts, save data, diagnostics, and build folders.

After install or update completes, the GUI enables `Launch umikaze`.

The GUI reads `app/manifest.json` when present and shows the package version in the window title/source line. The install receipt records version, installed file count, pak encryption state, and signature state.

## Patch Flow

Patch publishing is developer-only.

Publish a patch:

```powershell
scripts/patch.ps1 -BasePak old\data.pak -NewPak new\data.pak -Out update.patch
```

Build an Aria Update installer:

```powershell
scripts/update-installer.ps1 -Patch update.patch -Version <version>
```

The update zip contains:

- `AriaInstaller.exe`
- `update.patch`

Users apply updates by launching `AriaInstaller.exe` from the update zip. If `update.patch` exists beside the GUI, it runs as `Aria Update` and applies that patch to installed `data.pak`.

For DVD media, place the installer folder on the disc as-is. The GUI detects the source path and copies from the read-only media into the local install target.

Manual apply command:

```powershell
AriaEngine.exe aria-pack apply --base data.pak --patch update.patch --out data.pak.updated
Move-Item data.pak data.pak.bak
Move-Item data.pak.updated data.pak
```

The GUI performs the same apply flow and keeps a timestamped backup.

MSI/MSIX packaging should be added only after this GUI installer path is stable.
