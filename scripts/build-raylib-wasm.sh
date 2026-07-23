#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
lock_path="$repo_root/native/raylib-wasm/raylib-wasm.lock.json"
source_path="${RAYLIB_WASM_SOURCE_DIR:-$repo_root/artifacts/obj/raylib-5.5-src}"
output_path="${RAYLIB_WASM_OUTPUT_DIR:-$repo_root/artifacts/native/raylib-5.5}"

for tool in git jq emcc emar sha256sum; do
  command -v "$tool" >/dev/null || {
    echo "$tool is required. Activate the locked Emscripten toolchain first." >&2
    exit 1
  }
done

raylib_tag="$(jq -r .raylibTag "$lock_path")"
raylib_commit="$(jq -r .raylibCommit "$lock_path")"
emscripten_version="$(jq -r .emscriptenVersion "$lock_path")"
expected_hash="$(jq -r .expectedArchiveSha256 "$lock_path")"

if ! emcc --version | head -n 1 | grep -Fq "$emscripten_version"; then
  echo "Emscripten version mismatch; expected $emscripten_version." >&2
  exit 1
fi

if [[ ! -d "$source_path/.git" ]]; then
  mkdir -p "$(dirname "$source_path")"
  git clone --depth 1 --branch "$raylib_tag" https://github.com/raysan5/raylib.git "$source_path"
fi

source_commit="$(git -C "$source_path" rev-parse HEAD)"
if [[ "$source_commit" != "$raylib_commit" ]]; then
  echo "raylib source mismatch; expected $raylib_commit, got $source_commit." >&2
  exit 1
fi

rm -rf "$output_path"
mkdir -p "$output_path/obj"
source_root="$source_path/src"
mapfile -t compile_flags < <(jq -r '.compileFlags[]' "$lock_path")
modules=(rcore rshapes rtextures rtext rmodels raudio)
objects=()

for module in "${modules[@]}"; do
  object="$output_path/obj/$module.o"
  (
    cd "$source_root"
    emcc -c "$module.c" -o "$object" "${compile_flags[@]}" -I. -Iexternal/glfw/include
  )
  objects+=("$object")
done

web_archive="$output_path/libraylib.web.a"
emar rcsD "$web_archive" "${objects[@]}"
cp "$web_archive" "$output_path/libraylib.a"
archive_hash="$(sha256sum "$web_archive" | awk '{print $1}')"

if [[ -n "$expected_hash" && "$archive_hash" != "$expected_hash" ]]; then
  echo "raylib archive hash mismatch; expected $expected_hash, got $archive_hash." >&2
  exit 1
fi
if [[ "${RAYLIB_WASM_REQUIRE_LOCKED_HASH:-0}" == "1" && -z "$expected_hash" ]]; then
  echo "expectedArchiveSha256 is empty (actual: $archive_hash)." >&2
  exit 1
fi

jq -n \
  --arg version "$(jq -r .raylibVersion "$lock_path")" \
  --arg commit "$source_commit" \
  --arg emscripten "$emscripten_version" \
  --arg archiveSha256 "$archive_hash" \
  --argjson flags "$(jq .compileFlags "$lock_path")" \
  '{raylibVersion:$version,raylibCommit:$commit,emscriptenVersion:$emscripten,compileFlags:$flags,archive:"libraylib.web.a",archiveSha256:$archiveSha256}' \
  > "$output_path/build-record.json"
printf '%s  libraylib.web.a\n' "$archive_hash" > "$output_path/libraylib.web.a.sha256"

printf 'raylib WASM archive: %s\nSHA-256: %s\n' "$web_archive" "$archive_hash"
