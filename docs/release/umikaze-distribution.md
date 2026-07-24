# Umikaze V3 distribution

The release line has one content bundle and several thin delivery shells. The
compiled program and split PAKs are produced once; Windows, Linux, macOS, and
Web only wrap that same bundle. This keeps save compatibility and download
deduplication stable across targets.

## Fast local build

`prepare-desktop.mjs` fingerprints the Aria VM/runtime and the presentation.
Unchanged runs reuse `target/aria-web-runtime-tauri` and
`target/aria-presentation-tauri`; set `ARIA_FORCE_REBUILD=true` when diagnosing
toolchain changes. The normal development command remains:

```sh
npm --prefix examples/umikaze/ui run prepare:desktop
```

It uses an unsigned `dev` PAK. A WebView release uses a `signed` PAK (the
public verification key is safe to ship; the private key stays in CI):

```sh
ARIA_PAK_PROFILE=signed \
ARIA_PAK_SIGNING_KEY='publisher:<64-byte-hex-key>' \
ARIA_PAK_VERIFICATION_KEY_ID=publisher \
ARIA_PAK_VERIFICATION_KEY_HEX='<32-byte-public-key-hex>' \
npm --prefix examples/umikaze/ui run release:desktop
```

The script selects `deb` on Linux, `dmg` on macOS, and `nsis` on Windows. Set
`ARIA_TAURI_BUNDLES` to override the platform default (for example,
`deb,appimage` when `appimagetool` is installed). Platform signing and macOS
notarization happen after the unsigned Tauri bundle is created.

## CLI installers

For a native `aria build` bundle, the CLI can produce the portable archive and
installers without a GUI:

```sh
cargo run --release -p aria-cli -- build examples/umikaze \
  --target linux-x64 --profile signed --release --out dist/umikaze-linux
cargo run --release -p aria-cli -- package dist/umikaze-linux \
  --format auto --out dist/releases/linux
```

`auto` emits a deterministic ZIP, a self-contained user-level Linux `.run`,
and, when the host tools exist, `.deb` and AppImage. On macOS it emits an
`.app.tar.gz` and a `.dmg` when `hdiutil` is available. `--format installer`
turns missing native tools into an error; `--format zip` is portable-only.
Every package writes `release-manifest.json` and `checksums.sha256`.

The Linux `.run` installs under `~/.local/share/<game-id>` by default and
accepts `--install-dir DIR`. It does not require root. `.deb` remains the
system-integrated option for distributions that ship `dpkg-deb`.

## Web release

```sh
ARIA_PAK_PROFILE=signed \
ARIA_PAK_SIGNING_KEY='publisher:<64-byte-hex-key>' \
ARIA_PAK_VERIFICATION_KEY_ID=publisher \
ARIA_PAK_VERIFICATION_KEY_HEX='<32-byte-public-key-hex>' \
npm --prefix examples/umikaze/ui run release:web
```

The result is a static PWA archive plus `web-release.json`, `_headers`,
`release-manifest.json`, and `checksums.sha256`. Deploy the archive contents to
any static host. Immutable runtime/PAK files receive a one-year immutable
cache policy; `index.html`, the manifest, and the service worker are always
revalidated. The service worker keeps the first-load path small by fetching
the hot pack only when the reader needs it.

The GitHub Actions workflow `.github/workflows/umikaze-release.yml` builds the
Web artifact and the three native installer families from the same tag. It
expects the production PAK signing key in the `ARIA_PAK_SIGNING_KEY` secret;
the matching public verification key is supplied through
`ARIA_PAK_VERIFICATION_KEY_ID` and `ARIA_PAK_VERIFICATION_KEY_HEX`. No private
key is stored in the repository.
