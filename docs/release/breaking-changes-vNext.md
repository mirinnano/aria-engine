# Breaking Changes For Next Release

このメモは v1.0.0 以降へ進めるために入れた破壊的変更を記録します。

## Installer

- C# GUI installer (`src/AriaInstaller`) を廃止。
- Rust installer (`src/aria-installer`) を廃止。
- 公式 Windows installer は `installer/umikaze.nsi` から生成する NSIS setup exe に一本化。
- patch 用 GUI installer (`scripts/update-installer.ps1`) は廃止。patch 配布は `scripts/patch.ps1` による手動適用経路のみ残す。

## Engine API

- `VirtualMachine.LoadScript(List<Instruction>, Dictionary<string, int>, string)` を削除。
- `defsub` の `sub` alias を削除。`sub` は算術減算コマンドとして解決される。
- 呼び出し側は `LoadScript(ParseResult, string)` を使う。

## Aria v2 Language

- `struct` の `string` field は、数値レジスタ `%instance_field` ではなく文字列レジスタ `$instance_field` に展開する。
- `new Struct { ... }` は未知field、重複field、明らかな型不一致をParse Errorにする。
- `new Game.Point { ... }` のようなnamespace修飾struct名を許可する。

## Runtime Data

- `saves/persistent.ariasav` は source control から除外。
- runtime save data は配布入力や release source に含めない。

## Assets

- `src/AriaEngine/assets/fonts/JosefinSans-Thin.ttf` を削除。

## Config schema

- `AppConfig.SchemaVersion` を 1 → 2 にバンプ（Pak v3 redesign, Phase 5.1）。
  旧 `config.json`（`SchemaVersion` フィールドを持たないか `1`）はそのまま読み込めるが、
  ファイル保存時は `AssetGc` セクションと `SchemaVersion: 2` が出力される。
- 新セクション `AssetGc` を追加（既定値は `Enabled=false`、`TotalBudgetBytes=536870912`、
  `Gen1PromotionSeconds=1`、`Gen2PromotionSeconds=30`）。`Enabled` を `true` にすると
  世代別 GC が走り、未使用アセットが sweep で解放される。
- 詳細は [`reference/config.md`](../reference/config.md) の「アセット GC」節を参照。
- このファイルは TTF ではなく GitHub HTML が混入していたため、UI font は bundled `NotoSansJP-Regular.ttf` に統一。
