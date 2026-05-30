# Installer

The official installer artifact is built with NSIS from `installer/umikaze.nsi`.

## Build

```powershell
scripts/package.ps1 -Version <version> -Runtime win-x64
scripts/installer.ps1 -Version <version> -Runtime win-x64 -PackageDir artifacts/release/AriaEngine-<version>-win-x64/app
```

NativeAOT installer candidate:

```powershell
scripts/installer.ps1 -Version <version> -Runtime win-x64 -PublishAot:$true -PublishFlavor win-x64-aot-experimental
```

Requirements:

- NSIS 3.x
- `makensis.exe` available on `PATH` or installed under `%ProgramFiles(x86)%\NSIS` / `%ProgramFiles%\NSIS`
- Code signing uses `scripts/sign.ps1` after NSIS when `-Sign` is provided, then `scripts/verify-signing.ps1 -RequireSigned` must accept the setup exe.

## Output

```text
artifacts/installer/AriaEngine-<version>-installer.zip
```

The zip contains:

- `umikaze-<version>-<runtime>-setup.exe`
- `signature-audit.json` when the setup is signed through `scripts/installer.ps1 -Sign`

Audit an unsigned or signed setup manually:

```powershell
scripts/verify-signing.ps1 -Path artifacts/installer/<candidate>/umikaze-<version>-<runtime>-setup.exe
scripts/verify-signing.ps1 -Path artifacts/installer/<candidate>/umikaze-<version>-<runtime>-setup.exe -RequireSigned
```

## NSIS Installer

Run:

```text
umikaze-<version>-<runtime>-setup.exe
```

The installer can:

- install bundled engine files, `data.pak`, and compiled scripts
- create Start Menu and desktop shortcuts
- set shortcut working directory to the install directory
- launch `umikaze` after install

Shortcuts launch `AriaEngine.exe` with:

```text
--run-mode release --pak data.pak --compiled scripts/scripts.ariac
```

Default install target:

```text
%LOCALAPPDATA%\Ponkotusoft\umikaze
```

The target can be changed on the directory page. The bundled payload excludes development-only files such as PDBs, logs, temporary scripts, save data, diagnostics, and build folders.

## Patch Flow

Patch publishing is developer-only and is not part of the NSIS installer path yet.

Publish a patch:

```powershell
scripts/patch.ps1 -BasePak old\data.pak -NewPak new\data.pak -Out update.patch
```

Manual apply command:

```powershell
AriaEngine.exe aria-pack apply --base data.pak --patch update.patch --out data.pak.updated
Move-Item data.pak data.pak.bak
Move-Item data.pak.updated data.pak
```

MSI/MSIX packaging should be added only after this NSIS installer path is stable.
