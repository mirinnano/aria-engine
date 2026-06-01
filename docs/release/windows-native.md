# Windows Native Build Profiles

Official release artifacts:

- `win-x64-fd-singlefile`: framework-dependent, single-file, NSIS installer requires the .NET 8 runtime.
- `win-x64-sc-singlefile`: self-contained, single-file, larger package, no runtime prerequisite.

Windows native remains the primary desktop runtime target for this release line.
Web/PWA is an official browser target for non-Windows and mobile browser play.

Cross-target requirements:

- Native build keeps `win-x64-fd-singlefile` and `win-x64-sc-singlefile` packages.
- Browser releases ship as a static Web/PWA package with the Wasm core and browser renderer; no server process is required after download.
- NativeAOT package generation is tracked through `scripts/package.ps1 -PublishAot:$true -Runtime win-x64`; the verified package shape is `AriaEngine.exe` with `raylib.dll` and `libzstd.dll`, and without `coreclr.dll`, `AriaEngine.dll`, or `AriaEngine.runtimeconfig.json`.
- NativeAOT publish, launch smoke, JSON source-generation coverage, trim/AOT warnings, and save/load JSON paths are locally verified.
- NativeAOT installer candidates flow through `scripts/installer.ps1 -PublishAot:$true -PublishFlavor win-x64-aot-experimental`; unsigned local NSIS generation, silent install, installed launch smoke, and silent uninstall are verified.
- NativeAOT stays experimental until the signing release gate is clean or explicitly waived.
- Save/config compatibility is preserved by keeping packed native saves on Windows.

Signing requirements:

- Release signing must be explicitly configured; `scripts/sign.ps1` fails with `Code signing is not configured` when no production signing source is available.
- CI PFX signing uses `WINDOWS_CODESIGN_PFX_BASE64` and `WINDOWS_CODESIGN_PFX_PASSWORD`.
- Local certificate-store signing uses `ARIA_SIGN_CERT_THUMBPRINT`; custom signtool and timestamp endpoints may be supplied with `ARIA_SIGNTOOL_PATH` and `ARIA_SIGN_TIMESTAMP_URL`.
- `ARIA_SIGN_ALLOW_SELF_SIGNED=1` or `-AllowSelfSigned` is local-only and does not satisfy the trusted release audit.

Experimental artifacts:

- `win-x64-trimmed-experimental`
- `win-x64-aot-experimental`

Experimental artifacts are not public defaults until publish, launch, smoke, package, installer, signing, and trim/AOT warnings are clean or explicitly waived.
