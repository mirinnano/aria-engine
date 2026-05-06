# AriaEngine アセットパッケージング v2.2（最終設計）

## 設計変更履歴

- v2.0: カテゴリ別pak分割
- v2.1: 独自拡張子（.arib/.aris等）+ 完全暗号化を検討
- **v2.2**: 既存.pakフォーマット維持、拡張子は統一、暗号化は既存方式を継続

---

## 基本方針

- **拡張子**: `.pak` のまま（全カテゴリ共通）
- **フォーマット**: 既存のPakフォーマットを維持
- **暗号化**: 既存方式を継続（必要に応じて）
- **識別**: ファイル名でカテゴリを識別

---

## ファイル構成

### 命名規則

```
boot.pak              # 起動に必要な最低限
scenario.pak          # シナリオスクリプト
data.pak              # 画像素材
stream.pak            # BGM・環境音（v1）
stream2.pak           # BGM・環境音（v2、サイズ超過で自動生成）
voice.pak             # ボイス（v1）
voice2.pak            # ボイス（v2）
voice3.pak            # ボイス（v3）
update.pak            # 差分パッチ
```

### サイズ制限

| ファイル | サイズ制限 | 超過時の動作 |
|---------|-----------|------------|
| boot.pak | 50MB | エラー |
| scenario.pak | 10MB | エラー |
| data.pak | 500MB | エラー |
| stream.pak | 500MB | stream2.pak を生成 |
| voice.pak | 200MB | voice2.pak を生成 |
| update.pak | 100MB | update2.pak を生成 |

---

## Pak内部構造（既存方式継続）

```
[Magic: 5bytes "ARPK1"]          # 既存の識別子
[Manifest-Length: 4bytes]        
[Manifest JSON]                  # カテゴリ情報を追加
[Payload bytes...]               # 暗号化/圧縮済みデータ
```

### Manifest拡張

```json
{
  "version": "1",
  "created_at": "2026-05-06T12:00:00Z",
  "category": "voice",           // カテゴリ識別（新設）
  "pak_version": 1,              // 連番（voice2.pakなら2）
  "entries": [
    {
      "path": "voice/mio_001.ogg",
      "type": "audio",
      "category": "voice",       // エントリー単位でも記録
      "offset": 1024,
      "size": 4096,
      "hash": "sha256:abc..."
    }
  ]
}
```

---

## 自動分類ロジック

### カテゴリ判定（パッキング時）

```
1. ディレクトリ構造で事前判定
   assets/boot/*     → boot.pak
   assets/scenario/* → scenario.pak
   assets/data/*     → data.pak
   assets/stream/*   → stream.pak
   assets/voice/*    → voice.pak

2. ファイルサイズで voice/stream を判定（音声のみ）
   assets/stream/*.ogg  → stream.pak（BGM、5MB以上）
   assets/voice/*.ogg   → voice.pak（ボイス、5MB未満）

3. 拡張子補助判定
   .aria, .ariac  → scenario
   .png, .jpg     → data
   .ttf, .otf     → boot
```

### 開発時のディレクトリ構成

```
assets/
├── boot/           # 起動に必要な最低限
│   ├── manifest.json
│   ├── fonts/
│   └── branding/
├── scenario/       # シナリオスクリプト
│   ├── main.aria
│   └── scenario_*.aria
├── data/           # 画像素材
│   ├── bg/
│   ├── ch/
│   └── ui/
├── stream/         # BGM・環境音（5MB以上の音声）
│   └── bgm/
└── voice/          # ボイス・SE（5MB未満の音声）
    └── v/
```

---

## PakAssetProvider の検索順序

```
1. カテゴリ判定（ファイルパスから推定）
   "bgm/title.ogg" → streamカテゴリ

2. update.pak を優先検索
   - update.pak にパスがあれば、それを返す

3. 連番pakを新しい順に検索
   - stream2.pak → stream.pak
   - voice3.pak → voice2.pak → voice.pak

4. 各pak内でバイナリサーチ
```

---

## 実装タスク

### フェーズ1: aria-pack --split

- [ ] `AriaPackCommand.cs` に `--split` オプションを追加
- [ ] カテゴリ分類ロジックを実装（ディレクトリ + ファイルサイズ）
- [ ] サイズ制限チェック（超過時に連番pak生成）
- [ ] Manifestに `category` と `pak_version` フィールドを追加

### フェーズ2: PakAssetProvider 拡張

- [ ] 複数pak対応（連番pakの自動検出）
- [ ] update.pak 優先検索
- [ ] カテゴリ別キャッシュ戦略

### フェーズ3: テスト

- [ ] カテゴリ分類のテスト
- [ ] サイズ制限による連番生成テスト
- [ ] update.pak 差し替えテスト

---

## メリット

1. **拡張子統一**: `.pak` のままなので既存ツールと互換性あり
2. **既存フォーマット継続**: 暗号化/圧縮方式を変更しない
3. **識別容易**: ファイル名（boot.pak, voice2.pak等）でカテゴリ判別
4. **差分更新**: update.pak で簡単に差し替え
5. **サイズ管理**: 連番pakでファイルサイズを分散
