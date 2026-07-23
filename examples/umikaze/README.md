# 海風 Aria sample

This is the single-`aria;` semantic-runtime sample for the umikaze vertical
slice. The historical NScripter/C# sources under `src/AriaEngine/assets/scripts`
are not an executable compatibility path: `aria migrate` and the legacy parser
were removed. New scenario sources are compiled directly by the current Aria
front end.

The sample keeps the umikaze visual language (deep indigo, sea fog, pale
paper, restrained gold, quiet panels, chapter cards, and non-colour-only
focus states) and exposes the shared runtime features:
locale selection for Japanese, English, Simplified Chinese, and Traditional
Chinese; persistent chapter/CG progress; textbox and text-speed settings;
tween and screen effects; choices; save/load; menu; backlog; auto; and skip
input. The Japanese route is generated from the prose-first canonical
Markdown source rather than a one-line demo scenario.

Its React presentation package at [`ui`](ui) separates story data from the
title rail, reading panel, right/bottom sheets, settings sliders, backlog,
chapter cards, and gallery. The package receives only the semantic view model
from the shared WASM runtime and is also embedded by the Tauri desktop shell;
there is no second native layout implementation. The text-free sea-fog,
paper-grain, wave-divider, and chapter-ornament raster assets complement the
existing seaside scene without baking any player-facing words into images.
It deliberately uses color backgrounds and vector-like rectangles, so the
project can be compiled and replayed without shipping the original artwork.
The bundled Noto Sans JP UI face and M PLUS 1 Code reading face make the
same project runnable on desktop and web without relying on a host font.
M PLUS 1 Code is distributed under the SIL Open Font License; see
[`licenses/MPLUS1Code-OFL.txt`](licenses/MPLUS1Code-OFL.txt).

## 原本シナリオの同期

`/home/mirin/Desktop/Novel/src` が『海風』の正本です。原文を加筆・要約せずに
Aria の送り単位へ変換するには、リポジトリのルートから次を実行します。

```sh
cargo run -p aria-cli -- import-novel /home/mirin/Desktop/Novel/src \
  --out examples/umikaze/scripts/scenario/ja-JP.aria
```

生成された `ja-JP.aria` は編集対象ではありません。原本の各非空行を明示的な
クリック待ちとして保持し、`名前「台詞」` 形式だけを話者メタデータに分離します。
原本に未執筆の章は生成も表示もしません。

## Run

Build the browser package from the repository root:

```sh
cargo build -p aria-web --target wasm32-unknown-unknown
wasm-bindgen --target web --out-dir target/aria-web-runtime-local --out-name aria_web \
  target/wasm32-unknown-unknown/debug/aria_web.wasm
ARIA_WEB_RUNTIME_DIR=target/aria-web-runtime-local \
  cargo run -p aria-cli -- build examples/umikaze --target web --out target/umikaze-web
```

To run the desktop shell, install the operating system dependencies required
by Tauri/WebKit first, then run:

```sh
cd examples/umikaze/ui
npm install
npm run tauri -- dev
```

On this Gentoo-based development host, the missing packages are provided by
`net-libs/webkit-gtk:4.1` (which supplies `webkit2gtk-4.1.pc` and
`javascriptcoregtk-4.1.pc`). The package may bring a substantial dependency
set, so it is intentionally not installed by the project build scripts.
