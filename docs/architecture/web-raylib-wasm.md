# Raylib WASMプレビュー

`AriaEngine.Wasm` は、現行の `AriaEngine.Web` を残したままNative版と同じRaylib描画・入力・音声経路をブラウザで検証する別ターゲットです。既定のGitHub Pagesと `package-web.ps1` はまだ切り替えません。

## 固定バージョン

- .NET SDK 8.0.420 / `browser-wasm`
- Raylib-cs 7.0.2（共有エンジンプロジェクトから参照）
- raylib 5.5、Emscripten 3.1.34
- fonttools 4.59.0

正確なソースタグ、完全なコミットID、コンパイル・リンクフラグ、期待するアーカイブSHA-256は `native/raylib-wasm/raylib-wasm.lock.json` が一次ソースです。`scripts/build-raylib-wasm.sh` と `scripts/build-raylib-wasm.ps1` はバージョンとハッシュを検査し、`libraylib.web.a`、リンカ向け `libraylib.a`、SHA-256付きのbuild recordを生成します。CIで正とする再現ビルド環境はUbuntuです。

## フレームライフサイクル

`Runtime/RaylibRuntimeHost.cs` が次を所有します。

1. `Initialize`: init/main読込、VM・Raylib・フォント・音声の初期化
2. `Update(deltaTime)`: VM、入力、メニュー、音声、遷移、パーティクル、Tweenの更新
3. `Render`: `SpriteRenderer` とRaylibによる描画
4. `Shutdown`: 永続化、GPU/音声/アセットリソースの解放

Desktopは既存のwhileループ、WASMは `main.js` の `requestAnimationFrame` から同じAPIを1フレームずつ呼びます。

## アセット配布

`aria-web-assets.json` の各エントリは `group`、`logicalPath`、`url`、`size`、`sha256` を持ちます。起動時は `boot` と `ui` だけをMEMFSへ配置し、章ラベル先頭の `asset_preload "scenario_NN"` が必要なグループを追加取得します。Service Workerは取得済みレスポンスをバージョン付きキャッシュに保持します。パッケージ時には.NET frameworkシェルを列挙して初回インストールで事前キャッシュし、framework・アプリコード・アセットを含む全成果物のハッシュからキャッシュ世代を決定します。

Web配布では `NotoSansJP-Regular.ttf` をスクリプト・ローカライズ文面の使用文字へサブセット化し、同じ論理パスで配布します。Native開発用の元フォントは変更しません。

## 保存互換

起動前にIndexedDB `aria-engine` の `saves` / `settings` ストアをMEMFSへ復元します。保存後は既存キー `save:NNN` へ書き戻します。設定は同じ `settings` ストアの `settings:config` と `settings:persistent` を使用します。SaveFile自体はportable JSONで、旧Canvas版が保存したペイロードをそのまま読み込めます。

## ビルド

Linux (GNU coreutils):

```bash
dotnet workload install wasm-tools
./scripts/build-raylib-wasm.sh
./scripts/package-web-wasm.sh --version preview --skip-raylib-build
```

PowerShell Core:

```powershell
dotnet workload install wasm-tools
./scripts/build-raylib-wasm.ps1 -RequireLockedHash
./scripts/package-web-wasm.ps1 -Version preview -SkipRaylibBuild
```

`pyftsubset` はlock記載のfonttools版を使用してください。成果物は `artifacts/web-wasm/AriaEngine-preview-raylib-wasm` に生成されます。

## 昇格条件

日常のビルド・VMテスト・Chromium QAはUbuntu CIで完結させ、Windows実機は既定ターゲット昇格前のリリーススモークだけに限定します。Chrome系CI、Firefox/Safariのリリース前確認、日本語、マウス・キーボード・タッチ、右クリック、音声アンロック、リサイズ、旧セーブ読込、オフライン再起動、グループ失敗からの再試行を通過してから、既定WebパッケージとPagesを切り替えます。
