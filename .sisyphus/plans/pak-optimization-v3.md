# AriaEngine Pakフォーマット最適化 v3.0

## 設計方針

1. **独自拡張子**: `.ari{b,s,d,m,v,u}` でカテゴリ識別
2. **pak最適化**: メモリマップ、バイナリサーチ、プリフェッチ
3. **暗号化**: 既存方式を継続（必要に応じて）
4. **圧縮**: カテゴリ別に最適な方式を選択

---

## 独自拡張子

| 拡張子 | 読み | カテゴリ | 内容 |
|--------|------|---------|------|
| `.arib` | Aria Boot | boot | 起動に必要な最低限 |
| `.aris` | Aria Scenario | scenario | シナリオスクリプト |
| `.arid` | Aria Data | data | 画像素材（bg/ch/UI） |
| `.arim` | Aria Music | stream | BGM・環境音・動画 |
| `.ariv` | Aria Voice | voice | ボイス・SE |
| `.ariu` | Aria Update | update | 差分パッチ |

**メリット**:
- ファイル名だけでカテゴリ判別（`voice2.ariv` → voiceカテゴリv2）
- OS/ツールでの関連付け可能
- 拡張子フィルタで高速検索

---

## 最適化されたPakフォーマット

### ヘッダー構造（36バイト固定）

```
[Magic: 4bytes]      "ARIA"                    // 識別子
[Version: 1byte]     0x03                       // フォーマットv3
[Category: 1byte]    0x00-0x05                  // boot/scenario/data/stream/voice/update
[PakVersion: 1byte]  1-255                      // 連番（voice2→2）
[Flags: 1byte]       b0:暗号化 b1:圧縮 b2:チャンク化 b3-7:予約
[EntryCount: 4bytes]                           // ファイルエントリー数
[ManifestOffset: 8bytes]                       // マニフェストまでのオフセット
[ManifestSize: 4bytes]                         // マニフェスト圧縮サイズ
[PayloadOffset: 8bytes]                        // ペイロードまでのオフセット
[Reserved: 4bytes]                             // 将来拡張用
```

### マニフェスト構造（バイナリ）

```
[EntryTable]                                    // ソート済み（バイナリサーチ用）
  [PathHash: 8bytes]   xxHash64                 // パス文字列のハッシュ
  [Offset: 8bytes]                             // ペイロード内オフセット
  [Size: 4bytes]                               // 圧縮サイズ
  [OriginalSize: 4bytes]                       // 元サイズ
  [Flags: 2bytes]      b0:圧縮 b1:暗号化 b2-15:予約

[PathStringPool]                                // 実際のパス文字列（連結）
  "bg/title.png\0voice/mio_001.ogg\0..."
```

**最適化**:
- パス検索はハッシュ比較（O(1)近い）
- エントリーテーブルはメモリ上にマッピング
- 文字列プールは遅延読込

### ペイロード構造

```
[FileData] × EntryCount
  [Compressed/Encrypted Data]
```

---

## カテゴリ別最適化

### .arib（boot）

```
読み込み: 起動時に全文メモリマップ
圧縮:    Zstd L3（サイズ重視）
暗号化:  オプション（keyがあれば）
キャッシュ: 常時保持
```

### .aris（scenario）

```
読み込み: 起動時に全文メモリマップ
圧縮:    Zstd L5（テキスト圧縮効果大）
暗号化:  必須（リリース時）
キャッシュ: 常時保持
```

### .arid（data）

```
読み込み: オンデマンド + LRUキャッシュ
圧縮:    LZ4（速度優先）
暗号化:  オプション
キャッシュ: 最大64エントリー/256MB
最適化:  MipMap事前生成、テクスチャ圧縮
```

### .arim（stream）

```
読み込み: ストリーミング（4MBチャンク）
圧縮:    無圧縮（シークを保証）
暗号化:  チャンク単位（CTRモード）
キャッシュ: 先頭2チャンク + プリフェッチ1チャンク
最適化:  メモリマップ + シーク対応
```

### .ariv（voice）

```
読み込み: オンデマンド + LRUキャッシュ
圧縮:    LZ4（速度優先）
暗号化:  オプション
キャッシュ: 最大128エントリー/128MB
最適化:  プリロード（次の3ファイル）
```

### .ariu（update）

```
読み込み: 起動時に全文メモリマップ（小さいため）
圧縮:    Zstd L5
暗号化:  必須
優先度:  最高（他のpakを上書き）
```

---

## 高速化手法

### 1. メモリマップファイル

```csharp
// Windows: CreateFileMapping + MapViewOfFile
// Linux: mmap
// 大きなpakを直接メモリにマップ（コピー不要）

var mmf = MemoryMappedFile.CreateFromFile("data.arid");
var accessor = mmf.CreateViewAccessor();
```

**メリット**:
- OSがページ管理（不要な部分は自動解放）
- 複数プロセスで共有可能
- ファイル読込オーバーヘッド削減

### 2. ハッシュベース検索

```csharp
// パス文字列をxxHash64でハッシュ化
// エントリーテーブルをソート済み配列として保持
// バイナリサーチでO(log n)、ハッシュテーブルでO(1)

ulong hash = XXHash64.Compute(path);
int index = Array.BinarySearch(entries, hash);
```

### 3. 非同期プリフェッチ

```csharp
// voiceの場合、次の3ファイルを先読み
// dataの場合、次のシーンで使う可能性の高いファイルを先読み

async Task PrefetchAsync(string[] likelyNextPaths)
{
    foreach (var path in likelyNextPaths)
    {
        if (!cache.Contains(path))
        {
            _ = LoadAsync(path); // バックグラウンドで読込
        }
    }
}
```

### 4. 圧縮方式の選択

| 方式 | 速度 | 圧縮率 | 用途 |
|------|------|--------|------|
| 無圧縮 | 最大 | 1.0x | stream（シーク必要） |
| LZ4 | 非常に速い | 2.0-2.5x | data/voice（速度優先） |
| Zstd L3 | 速い | 2.5-3.5x | boot（バランス） |
| Zstd L5 | 普通 | 3.0-4.5x | scenario/update（サイズ重視） |

---

## 実装タスク

### フェーズ1: フォーマットv3実装

- [x] `PakArchiveV3` クラス（ヘッダー36バイト固定）
- [x] バイナリマニフェスト（ハッシュテーブル）
- [x] xxHash64実装（パスハッシュ用）
- [x] メモリマップ対応

### フェーズ2: 圧縮/暗号化

- [x] LZ4ラッパー（K4os.Compression.LZ4等）
- [x] Zstdラッパー（ZstdSharp等）
- [x] チャンク暗号化（CTRモード、.arim専用）

### フェーズ3: aria-pack v3

- [x] `--format v3` オプション
- [x] `--split` でカテゴリ別出力（.arib/.aris等）
- [x] 自動圧縮方式選択
- [x] ハッシュテーブル生成

### フェーズ4: PakAssetProvider v3

- [x] メモリマップ対応
- [x] ハッシュベース検索
- [x] 非同期プリフェッチ
- [x] カテゴリ別キャッシュ戦略

### フェーズ5: テスト

- [x] 読み込み速度ベンチマーク
- [x] メモリ使用量測定
- [x] ストリーミング遅延測定

---

## パフォーマンス目標

| 指標 | v2（現状） | v3（目標） |
|------|-----------|-----------|
| ファイル検索 | O(n)文字列比較 | O(1)ハッシュ検索 |
| テクスチャ読込 | 200ms | 50ms |
| BGMストリーミング開始 | 1秒 | 200ms |
| ボイス読込 | 100ms | 20ms |
| メモリ使用量 | フルコピー | メモリマップ（共有） |
| pakファイルサイズ | 100% | 60-70%（圧縮効果） |
