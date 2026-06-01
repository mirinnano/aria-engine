# AriaEngine / umikaze

.NET 8.0 + Raylibで動くビジュアルノベルゲームエンジンと、その同梱作品 `umikaze` のリリース作業リポジトリです。NScripter互換の `.aria` スクリプト言語をサポートします。

> **v2.0.0-rc.1**: Aria v2 strict（型安全・寿命管理・構造化）の基盤実装が完了。`strict on` で有効化。詳細は [`docs/spec/aria-v2-strict.md`](docs/spec/aria-v2-strict.md) を参照。

## 方針

- **スクリプト駆動**: `.aria`ファイルでゲーム全体を記述
- **NScripter互換**: テキスト、スプライト、ボタン、セーブ、互換UI、`effect` / `print` をサポート
- **v2 strict**: `# aria-version: 2.0` + `strict on` で型安全・所有権・スコープ管理を有効化
- **作者主導UI**: 高レベル自動生成に寄せすぎず、描画命令と script-owned screen を重視
- **Release品質**: `data.pak` + `scripts/scripts.ariac` + NSIS installer を正式配布経路にする
- **Windows配布**: `scripts/package.ps1` と `scripts/installer.ps1` で win-x64 artifact を生成

## クイックスタート

```powershell
dotnet build
dotnet run --project src/AriaEngine/AriaEngine.csproj
```

## リリースビルド

```powershell
scripts/doctor.ps1 -Project src/AriaEngine/AriaEngine.csproj -InitScript init.aria -MainScript assets/scripts/main.aria -Strict
scripts/package.ps1 -Version v1.0.0-rc.2 -Runtime win-x64
scripts/installer.ps1 -Version v1.0.0-rc.2 -Runtime win-x64 -PackageDir artifacts/release/AriaEngine-v1.0.0-rc.2-win-x64/app
```

主な成果物:

```text
artifacts/release/AriaEngine-v1.0.0-rc.2-win-x64/app
artifacts/release/AriaEngine-v1.0.0-rc.2-win-x64/dist/AriaEngine-v1.0.0-rc.2-win-x64.zip
artifacts/installer/AriaEngine-v1.0.0-rc.2-win-x64-installer.zip
```

production launch args:

```text
--run-mode release --pak data.pak --compiled scripts/scripts.ariac
```

## プロジェクト構成

```
engine/
├── src/AriaEngine/     # エンジン本体
│   ├── Core/           # VM、Parser、状態管理、CommandHandler
│   ├── Rendering/      # スプライト描画、アニメーション
│   ├── Input/          # 入力処理
│   ├── Audio/          # オーディオ管理
│   └── assets/         # フォント、背景、キャラクター、scripts
├── installer/          # NSIS installer
├── scripts/            # doctor/package/installer/release tooling
├── docs/               # ドキュメント
└── init.aria           # engine initialization
```

## スクリプト例

```aria
# aria-version: 2.0
strict on

*start
    bg "forest.png", 0
    textclear
    ミオ「ようこそ！」

    ; scope でUIリソースの寿命を管理
    scope "menu"
        owned @btn_start = lsp_rect(100, 400, 300, 200, 50)
        spbtn @btn_start, 1
        btnwait %result
    end_scope
    ; @btn_start はここで自動解放

    if %result == 1
        text "クリックされました"
    endif

    ; func で構造化
    func show_message(msg: string) -> void
        textclear
        text $msg
    endfunc

    show_message("次の章へ")
```

## ツール

| ツール | 説明 |
|--------|------|
| `aria-lint` | 静的解析（型チェック、寿命チェック、未使用変数検出） |
| `aria-compile` | スクリプトの暗号化コンパイル |
| `aria-pack` | アセットのパッケージング |
| `aria-doc` | ドキュメント生成 |
| `aria-format` | コードフォーマット |
| `aria-save` | セーブデータ操作 |

## ドキュメント

- [ドキュメント一覧](docs/README.md) - チュートリアル、リファレンス、ガイド (Diátaxis 構成)
- [Aria v2 Strict 仕様書](docs/spec/aria-v2-strict.md) - 型安全・寿命管理・構造化の詳細仕様
- [スクリプト言語リファレンス](docs/reference/opcodes/) - 全オペコード詳細
- [アーキテクチャ: 概要](docs/architecture/overview.md) - エンジン構成と責務分担
- [アーキテクチャ: プラットフォーム](docs/architecture/platform.md) - `.pak` / `.ariac` と dev/release モード
- [リリースビルドの作成](docs/how-to-guides/compile-and-package.md)
- [NSIS installer](docs/release/installer.md)
- [v1.0.0 compatibility contract](docs/release/compatibility-v1.0.0.md)

### 🆕 UX Quick Wins (T1/T2/T3)

- **T1: セーブサムネイル** — セーブメニュー open 時にゲーム画面をキャプチャ → ゲーム本編の画像が記録される ([詳細](docs/tutorials/save-load.md#ステップ6-セーブサムネイルの仕組みを理解する))
- **T2: ボタンの押下感** — `theme "soft"` などで全ボタンに押下アニメーション (`ButtonFeel`) を自動適用 ([詳細](docs/reference/ui/button-feel.md))
- **T3: ADV テキスト垂直配置** — `textbox_align center` / `top` / `bottom` で垂直位置を制御 ([詳細](docs/reference/opcodes/textbox_align.md))

## AIエージェント向け

- [AGENTS.md](docs/ai-agent/AGENTS.md) - プロジェクト構造、コードパターン、貢献方法

## ライセンス

MIT License
