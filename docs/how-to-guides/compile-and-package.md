# リリースビルドの作成と配布

このガイドでは、AriaEngineで開発したゲームを配布用にビルド・パッケージングする手順を説明します。

## 開発モードとリリースモードの違い

AriaEngineには2つの実行モードがあります。

| 項目 | 開発モード（Dev） | リリースモード（Release） |
|------|------------------|------------------------|
| スクリプト読み込み | 平文の`.aria`ファイルを直接読み込む | コンパイル済み`.ariac` + `.pak`から読み込む |
| ライブリロード | スクリプト変更を即座に反映 | なし |
| アセット読み込み | ディスク上の`assets/`フォルダから読み込む | `.pak`アーカイブから読み込む |
| セキュリティ | スクリプトとアセットが丸見え | `data.pak` と `scripts/scripts.ariac` に収録 |

リリースモードでは、`.aria`スクリプトを`.ariac`へコンパイルし、画像・音声・アセットを`data.pak`へまとめます。通常の production package では raw `.aria` は同梱しません。

## 前提条件

- .NET 8.0 SDKがインストールされていること
- 暗号化する場合は `ARIA_PACK_KEY` 環境変数が設定されていること（未設定でも package は作成できます）
- `init.aria`とメインスクリプトが正しく配置されていること
- Windows installer を作成する場合は NSIS 3.x の `makensis.exe` が利用できること

## 推奨手順: release scripts を使う

通常は手動で `aria-compile` / `aria-pack` を直接呼ばず、release scripts を使います。これにより manifest、checksums、README、release notes、raw `.aria` 除外、NSIS installer まで同じ経路で生成できます。

```powershell
scripts/doctor.ps1 -Project src/AriaEngine/AriaEngine.csproj -InitScript init.aria -MainScript assets/scripts/main.aria -Strict
scripts/package.ps1 -Version v1.0.0-rc.2 -Runtime win-x64
scripts/installer.ps1 -Version v1.0.0-rc.2 -Runtime win-x64 -PackageDir artifacts/release/AriaEngine-v1.0.0-rc.2-win-x64/app
```

## 手順1: スクリプトのコンパイル

`aria-compile`コマンドで`.aria`スクリプトをコンパイル済みバイナリ`.ariac`に変換します。

```bash
cd src/AriaEngine
dotnet run -- aria-compile --init init.aria --main assets/scripts/main.aria --out build/scripts.ariac
```

### パラメータ

| パラメータ | 説明 | デフォルト値 |
|-----------|------|------------|
| `--init` | エンジン初期化スクリプトのパス | `init.aria` |
| `--main` | メインスクリプトのパス | `assets/scripts/main.aria` |
| `--out` | 出力先の`.ariac`ファイルパス | `build/scripts.ariac` |
| `--key` | 暗号化キー（直接指定する場合） | `ARIA_PACK_KEY`環境変数 |

### 実行例

```bash
# デフォルト設定でコンパイル
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-compile

# カスタムパスを指定
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-compile --init mygame/init.aria --main mygame/scripts/start.aria --out dist/scripts.ariac
```

コンパイル成功時、`Compiled scripts: N`と`Output: <パス>`が表示されます。エラーがある場合は`aria_error.log`を確認してください。

## 手順2: アセットのパッケージング

`aria-pack build`コマンドでアセットフォルダとコンパイル済みスクリプトを1つの`.pak`ファイルにまとめます。

```bash
cd src/AriaEngine
dotnet run -- aria-pack build --input assets --compiled build/scripts.ariac --output build/data.pak
```

注意: `assets/scripts` が残ったまま直接 `aria-pack build --input assets` を実行すると、raw `.aria` も `data.pak` に入ります。production package では `scripts/package.ps1` を使うか、pack 前に raw script を除外してください。

### パラメータ

| パラメータ | 説明 | 必須 |
|-----------|------|------|
| `--input` | アセットフォルダのパス | はい |
| `--compiled` | `aria-compile`で生成した`.ariac`ファイルのパス | いいえ |
| `--output` | 出力先の`.pak`ファイルパス | はい |
| `--key` | 暗号化キー（直接指定する場合） | いいえ |
| `--verbose` | 詳細出力を有効にする | いいえ |

### 実行例

```bash
# 基本のパッケージング（スクリプト込み）
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack build --input assets --compiled build/scripts.ariac --output dist/data.pak

# アセットのみパッケージング（スクリプト別管理の場合）
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack build --input assets --output dist/assets.pak

# 詳細出力付き
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack build --input assets --compiled build/scripts.ariac --output dist/data.pak --verbose
```

### パッチの作成と適用（差分配布）

アップデート配布時に全ファイルを再配布する代わりに、差分パッチを作成できます。

```bash
# 旧バージョンと新バージョンの差分パッチを作成
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack diff --base v1.0.pak --new v1.1.pak --out update-v1.0-to-v1.1.patch

# ユーザー側でパッチを適用
dotnet run --project src/AriaEngine/AriaEngine.csproj -- aria-pack apply --base v1.0.pak --patch update-v1.0-to-v1.1.patch --out v1.1.pak
```

## 手順3: 暗号化キーの管理

`.pak`ファイルと`.ariac`ファイルの暗号化に使用するキーは、`ARIA_PACK_KEY`環境変数で管理します。

### ローカル環境での設定

**Windows (PowerShell):**
```powershell
$env:ARIA_PACK_KEY="your-secret-key-here"
```

**Windows (システム環境変数):**
```powershell
[Environment]::SetEnvironmentVariable("ARIA_PACK_KEY", "your-secret-key-here", "User")
```

**Linux/macOS:**
```bash
export ARIA_PACK_KEY="your-secret-key-here"
```

### キー生成の推奨

キーは推測されにくい長さの文字列にしてください。32文字以上の英数字記号の組み合わせを推奨します。

```powershell
# PowerShellでランダムなキーを生成
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object { [char]$_ })
```

### 注意事項

- キーは必ず安全な場所に保管してください。紛失するとパッケージを復元できません
- バージョンアップ時も同じキーを使用してください。異なるキーを使うとセーブデータ互換性が失われる場合があります
- キーをソースコードやリポジトリに含めないでください

## 手順4: CI/CD連携（GitHub Actions）

リポジトリに`.github/workflows/aria-cicd.yml`が含まれています。タグをプッシュすると自動的にリリースビルドが作成されます。

### ワークフローの動作

1. `master`ブランチへのプッシュ: ビルドとスモークテストを実行
2. release package を生成
3. NSIS installer zip を生成
4. 手動実行: `workflow_dispatch`でリリース作成を選択可能

### 必要な設定

GitHubリポジトリの「Settings > Secrets and variables > Actions」で以下を設定してください。

| シークレット名 | 内容 |
|--------------|------|
| `ARIA_PACK_KEY` | パッケージ暗号化用のキー |

### リリースの作成手順

```bash
# 1. バージョンタグを付ける
git tag -a v1.0.0 -m "Release version 1.0.0"

# 2. タグをプッシュする
git push origin v1.0.0
```

タグをプッシュすると、GitHub Actionsが自動的に以下を実行します。

1. リリースビルドのコンパイル
2. `aria-compile`によるスクリプトコンパイル
3. `aria-pack build`によるアセットパッケージング
4. 配布用ZIPの作成
5. NSIS installer zip の作成
6. GitHub ReleaseへのZIPアップロード

### 手動でのワークフロー実行

GitHubの「Actions」タブから「Aria CI/CD」を選択し、「Run workflow」をクリックします。`create_release`にチェックを入れると、リリースが作成されます。

### CIスクリプトのローカル実行

GitHub Actionsと同じ処理をローカルで実行するには、`scripts/cicd.ps1`を使用します。

```powershell
# フルパイプライン（キー設定時はパッケージングも実行）
./scripts/cicd.ps1 -Project src/AriaEngine/AriaEngine.csproj -OutDir artifacts/publish -InitScript init.aria -MainScript assets/scripts/main.aria

# パッケージングをスキップ（ビルドとスモークテストのみ）
./scripts/cicd.ps1 -Project src/AriaEngine/AriaEngine.csproj -OutDir artifacts/publish -InitScript init.aria -MainScript assets/scripts/main.aria -SkipPackage

# スモークテストもスキップ
./scripts/cicd.ps1 -Project src/AriaEngine/AriaEngine.csproj -OutDir artifacts/publish -InitScript init.aria -MainScript assets/scripts/main.aria -SkipSmoke -SkipPackage
```

## 手順5: リリースビルドの動作確認

### エンジンの実行

```bash
cd src/AriaEngine

# リリースモードで起動（.pakファイルを指定）
dotnet run -- --run-mode release --pak build/data.pak --compiled build/scripts.ariac

# キーを直接指定する場合
dotnet run -- --run-mode release --pak build/data.pak --compiled build/scripts.ariac --key "your-secret-key"
```

### 配布フォルダの構成例

```
MyGame/
├── AriaEngine.exe          # 公開用にリネーム可能
├── data.pak                # packed assets
├── scripts/
│   └── scripts.ariac       # compiled script bundle
├── README.md               # package usage notes
├── manifest.json           # build / compatibility / signing metadata
├── checksums.txt           # SHA-256 checksums
├── config.template.json    # default config reference
└── saves/                  # セーブデータフォルダ（自動作成）
```

### 確認項目

- [ ] タイトル画面が正常に表示される
- [ ] 各シーンの画像・音声が正しく読み込まれる
- [ ] セーブ・ロード機能が動作する
- [ ] ボタン入力・キー入力が正しく反応する
- [ ] `aria_error.log`が生成されていない（または警告のみ）

### よくある問題と対処

| 症状 | 原因 | 対処 |
|------|------|------|
| `BOOT_RELEASE_NO_PAK`警告 | `--pak`が指定されていない | `--pak`パラメータを確認 |
| `BOOT_PAK_OPEN`エラー | `.pak`ファイルが開けない | パス、キー、ファイル破損を確認 |
| `BOOT_COMPILED_LOAD`エラー | `.ariac`が読めない | `aria-compile`の出力確認、キー一致確認 |
| `BOOT_RELEASE_FALLBACK`警告 | リリースモードで読み込み失敗 | `.pak`内に`scripts/scripts.ariac`が含まれているか確認 |
| 画像・音声が読めない | `.pak`内のパス不一致 | `assets/`以下のフォルダ構造を確認 |

## 関連ドキュメント

- [スクリプト言語リファレンス](../reference/opcodes/) - スクリプト作成の詳細
- [シンタックスリファレンス](../reference/syntax.md) - 文法の詳細
- [設定リファレンス](../reference/config.md) - `config.json`の設定項目
