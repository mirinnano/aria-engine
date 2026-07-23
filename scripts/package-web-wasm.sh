#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/src/AriaEngine.Wasm/AriaEngine.Wasm.csproj"
version="dev"
configuration="Release"
output_root="$repo_root/artifacts/web-wasm"
skip_restore=0
skip_raylib_build=0
skip_font_subset=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --configuration) configuration="$2"; shift 2 ;;
    --output-root) output_root="$2"; [[ "$output_root" = /* ]] || output_root="$repo_root/$output_root"; shift 2 ;;
    --skip-restore) skip_restore=1; shift ;;
    --skip-raylib-build) skip_raylib_build=1; shift ;;
    --skip-font-subset) skip_font_subset=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

for tool in dotnet python jq sha256sum zip; do
  command -v "$tool" >/dev/null || { echo "$tool is required." >&2; exit 1; }
done

lock_path="$repo_root/native/raylib-wasm/raylib-wasm.lock.json"
publish_dir="$repo_root/artifacts/obj/web-wasm-publish/$version"
package_dir="$output_root/AriaEngine-$version-raylib-wasm"
dist_dir="$output_root/dist"
zip_path="$dist_dir/AriaEngine-$version-raylib-wasm.zip"
raylib_archive="$repo_root/artifacts/native/raylib-5.5/libraylib.a"

if [[ "$skip_raylib_build" == 0 ]]; then
  RAYLIB_WASM_REQUIRE_LOCKED_HASH=1 "$repo_root/scripts/build-raylib-wasm.sh"
fi
[[ -f "$raylib_archive" ]] || { echo "raylib WASM archive not found: $raylib_archive" >&2; exit 1; }

rm -rf "$publish_dir" "$package_dir"
rm -f "$zip_path"
mkdir -p "$publish_dir" "$package_dir" "$dist_dir"

publish_args=(publish "$project" -c "$configuration" -o "$publish_dir"
  -p:NuGetAudit=false -p:WasmBuildNative=true -p:RaylibWasmArchive="$raylib_archive")
if [[ "$skip_restore" == 1 ]]; then publish_args+=(--no-restore); fi
dotnet "${publish_args[@]}"

wwwroot="$publish_dir/wwwroot"
[[ -d "$wwwroot" ]] || { echo "WASM publish output missing wwwroot: $wwwroot" >&2; exit 1; }

if [[ "$skip_font_subset" == 0 ]]; then
  command -v pyftsubset >/dev/null || { echo "pyftsubset is required." >&2; exit 1; }
  expected_fonttools="$(jq -r .fontToolsVersion "$lock_path")"
  actual_fonttools="$(python -c 'import fontTools; print(fontTools.__version__)')"
  [[ "$actual_fonttools" == "$expected_fonttools" ]] || {
    echo "fonttools version mismatch; expected $expected_fonttools, got $actual_fonttools." >&2
    exit 1
  }
  glyph_text="$publish_dir/web-font-glyphs.txt"
  python "$repo_root/scripts/web-wasm-package.py" glyphs --repo-root "$repo_root" --output "$glyph_text"
  pyftsubset "$repo_root/src/AriaEngine/assets/fonts/NotoSansJP-Regular.ttf" \
    --text-file="$glyph_text" \
    --output-file="$wwwroot/assets/fonts/NotoSansJP-Regular.ttf" \
    --layout-features='*' --glyph-names --symbol-cmap --legacy-cmap \
    --notdef-glyph --recommended-glyphs --name-IDs='*' --name-legacy --name-languages='*'
fi

package_metadata="$(python "$repo_root/scripts/web-wasm-package.py" finalize --wwwroot "$wwwroot")"
cp -a "$wwwroot/." "$package_dir/"

for required in index.html main.js service-worker.js manifest.webmanifest aria-web-assets.json _framework; do
  [[ -e "$package_dir/$required" ]] || { echo "Package output missing $required" >&2; exit 1; }
done

jq -n \
  --arg version "$version" \
  --arg project "src/AriaEngine.Wasm/AriaEngine.Wasm.csproj" \
  --arg raylibVersion "$(jq -r .raylibVersion "$lock_path")" \
  --arg emscriptenVersion "$(jq -r .emscriptenVersion "$lock_path")" \
  --arg generatedAtUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  --argjson package "$package_metadata" \
  '{version:$version,target:"raylib-wasm-pwa-preview",project:$project,raylibVersion:$raylibVersion,emscriptenVersion:$emscriptenVersion,assetManifestSha256:$package.assetManifestSha256,cacheVersion:$package.cacheVersion,generatedAtUtc:$generatedAtUtc}' \
  > "$package_dir/manifest.json"

(
  cd "$package_dir"
  find . -type f ! -name checksums.sha256 -print0 | sort -z | xargs -0 sha256sum | sed 's#  \./#  #' > checksums.sha256
  zip -qr "$zip_path" .
)

printf 'Raylib WASM package written: %s\nRaylib WASM package zip written: %s\n' "$package_dir" "$zip_path"
