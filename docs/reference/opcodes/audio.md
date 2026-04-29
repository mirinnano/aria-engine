# オーディオコマンド

BGM、効果音（SE）、ボイス再生を制御するコマンドです。Raylibの音声機能を使用し、非同期で音声ファイルを読み込み・再生します。

---

## 目次

- [BGM制御](#bgm制御)
- [SE・ボイス制御](#seボイス制御)
- [音量制御](#音量制御)
- [フェード制御](#フェード制御)

---

## BGM制御

### play_bgm

**カテゴリ**: オーディオ

**構文**:
```
play_bgm "ファイルパス"
bgm "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 再生するBGMファイルのパス

**説明**:
BGMを再生します。指定したファイルをBGMとして読み込み、ループ再生を開始します。新しいBGMを指定すると、現在再生中のBGMは停止して入れ替わります。フェードアウト中に呼び出された場合、フェードはリセットされます。

BGMは内部キャッシュ（最大8ファイル）に保持され、同じファイルを再度再生する際は読み込みを省略します。対応形式はRaylibがサポートする音声形式（MP3、OGG、WAV等）です。

**例**:
```aria
play_bgm "assets/bgm/title_theme.mp3"
bgm "assets/bgm/main_theme.ogg"
```

**関連コマンド**: [stop_bgm](#stop_bgm), [bgmvol](#bgmvol), [bgmfade](#bgmfade)

---

### stop_bgm

**カテゴリ**: オーディオ

**構文**:
```
stop_bgm
```

**引数**: なし

**説明**:
現在再生中のBGMを停止します。フェードアウト中であっても即座に停止し、フェード状態をリセットします。BGMファイル自体はキャッシュに残り、次回再生時に再利用されます。

**例**:
```aria
stop_bgm
text "BGMを停止しました"
```

**関連コマンド**: [play_bgm](#play_bgm), [bgmfade](#bgmfade)

---

### play_mp3

**カテゴリ**: オーディオ

**構文**:
```
play_mp3 "ファイルパス"
mp3loop "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 再生するMP3ファイルのパス

**説明**:
MP3ファイルをBGMとして再生します。`play_bgm`と同じく`CurrentBgm`に設定されるため、BGMスロットを共有します。`play_bgm`との違いは名前とエイリアスのみで、内部的な動作は同一です。NScripter互換のため`mp3loop`エイリアスも提供されています。

**例**:
```aria
play_mp3 "assets/bgm/ending.mp3"
mp3loop "assets/bgm/loop.mp3"
```

**関連コマンド**: [play_bgm](#play_bgm), [mp3vol](#mp3vol), [mp3fadeout](#mp3fadeout)

---

## SE・ボイス制御

### play_se

**カテゴリ**: オーディオ

**構文**:
```
play_se "ファイルパス"
play_se チャンネル, "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 再生する効果音ファイルのパス
- `チャンネル`（オプション）: 互換性用の引数。値は無視されます

**説明**:
効果音（SE）を1回だけ再生します。指定したファイルをSEとして読み込み、再生キュー（`PendingSe`）に追加します。実際の再生は次のフレームの`AudioManager.Update`で行われます。

SEは内部キャッシュ（最大16ファイル）に保持されます。対応形式はRaylibがサポートする音声形式です。

2引数形式（`play_se 0, "path"`）はNScripter互換のため存在しますが、第1引数のチャンネル値は無視されます。

**例**:
```aria
play_se "assets/se/click.wav"
play_se 1, "assets/se/cursor.ogg"
```

**関連コマンド**: [sevol](#sevol), [dwave](#dwave)

---

### dwave

**カテゴリ**: オーディオ

**構文**:
```
dwave "ファイルパス"
dwave チャンネル, "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 再生するボイスファイルのパス
- `チャンネル`（オプション）: 互換性用の引数。値は無視されます

**説明**:
ボイス（キャラクター音声）を再生します。`play_se`と同じくSEキューに追加されますが、追加で`LastVoicePath`にファイルパスを記録します。バックログからのボイス再生機能で使用されます。

2引数形式（`dwave 0, "path"`）はNScripter互換のため存在しますが、第1引数のチャンネル値は無視されます。

**例**:
```aria
dwave "assets/voice/mio_001.wav"
ミオ「おはようございます」
```

**関連コマンド**: [dwaveloop](#dwaveloop), [dwavestop](#dwavestop), [play_se](#play_se)

---

### dwaveloop

**カテゴリ**: オーディオ

**構文**:
```
dwaveloop "ファイルパス"
dwaveloop チャンネル, "ファイルパス"
```

**引数**:
- `"ファイルパス"`: 再生するボイスファイルのパス
- `チャンネル`（オプション）: 互換性用の引数。値は無視されます

**説明**:
ボイスファイルをSEキューに追加します。`play_se`や`dwave`と同じ動作をします。NScripterの`dwaveloop`コマンドの互換エイリアスとして提供されていますが、AriaEngineではSEはワンショット再生のみで、ループ制御は行われません。

**例**:
```aria
dwaveloop "assets/voice/ambient.wav"
```

**関連コマンド**: [dwave](#dwave), [dwavestop](#dwavestop)

---

### dwavestop

**カテゴリ**: オーディオ

**構文**:
```
dwavestop
```

**引数**: なし

**説明**:
SEキュー（`PendingSe`）をクリアします。キューに溜まっている未再生のSE・ボイスを全て破棄します。既に再生が開始されたSEには影響しません。

**例**:
```aria
dwavestop
```

**関連コマンド**: [dwave](#dwave), [dwaveloop](#dwaveloop)

---

## 音量制御

### bgmvol

**カテゴリ**: オーディオ

**構文**:
```
bgmvol 音量
```

**引数**:
- `音量`: BGMの音量（0〜100）

**説明**:
BGMの再生音量を設定します。0で無音、100で最大音量です。デフォルト値は100です。小数点以下の指定はできません。

**例**:
```aria
bgmvol 50   ; 50%の音量
bgmvol 0    ; ミュート
bgmvol 100  ; 最大音量
```

**関連コマンド**: [sevol](#sevol), [mp3vol](#mp3vol), [play_bgm](#play_bgm)

---

### sevol

**カテゴリ**: オーディオ

**構文**:
```
sevol 音量
```

**引数**:
- `音量`: SEの音量（0〜100）

**説明**:
効果音（SE）・ボイスの再生音量を設定します。0で無音、100で最大音量です。デフォルト値は100です。`play_se`や`dwave`で再生される音声に適用されます。

**例**:
```aria
sevol 80    ; 80%の音量
sevol 0     ; SEをミュート
```

**関連コマンド**: [bgmvol](#bgmvol), [play_se](#play_se), [dwave](#dwave)

---

### mp3vol

**カテゴリ**: オーディオ

**構文**:
```
mp3vol 音量
```

**引数**:
- `音量`: MP3（BGM）の音量（0〜100）

**説明**:
`play_mp3`で再生するBGMの音量を設定します。内部的には`bgmvol`と同じ`BgmVolume`を操作するため、`bgmvol`と`mp3vol`は同じ音量を共有します。NScripter互換のため`mp3vol`エイリアスが存在します。

**例**:
```aria
mp3vol 70
play_mp3 "assets/bgm/boss_battle.mp3"
```

**関連コマンド**: [bgmvol](#bgmvol), [play_mp3](#play_mp3)

---

## フェード制御

### bgmfade

**カテゴリ**: オーディオ

**構文**:
```
bgmfade 時間
```

**引数**:
- `時間`: フェードアウト時間（ミリ秒）。省略時は500

**説明**:
現在再生中のBGMを指定した時間でフェードアウトし、停止します。フェードは毎フレーム音量を下げていき、終了時にBGMを停止します。既にBGMが停止している場合、または時間が0の場合、即座に停止します。

フェード中に新しいBGMが指定されると、フェードはキャンセルされ新しいBGMが通常音量で再生されます。

**例**:
```aria
bgmfade 1000  ; 1秒かけてフェードアウト
bgmfade 3000  ; 3秒かけてフェードアウト
```

**関連コマンド**: [stop_bgm](#stop_bgm), [play_bgm](#play_bgm), [mp3fadeout](#mp3fadeout)

---

### mp3fadeout

**カテゴリ**: オーディオ

**構文**:
```
mp3fadeout 時間
```

**引数**:
- `時間`: フェードアウト時間（ミリ秒）。省略時は500

**説明**:
`bgmfade`と同じ動作をします。MP3形式のBGM用にNScripter互換で提供されています。内部的には同じフェード機構を使用し、`BgmFadeOutDurationMs`と`BgmFadeOutTimerMs`を操作します。

**例**:
```aria
mp3fadeout 2000  ; 2秒かけてフェードアウト
```

**関連コマンド**: [bgmfade](#bgmfade), [play_mp3](#play_mp3), [stop_bgm](#stop_bgm)

---

## 実装上の注意

### キャッシュ機構

- **BGMキャッシュ**: 最大8ファイル。LRU方式で古いファイルをアンロードします。
- **SEキャッシュ**: 最大16ファイル。LRU方式で古いファイルをアンロードします。
- 読み込みに失敗したファイルは`failedBgm`/`failedSe`に記録され、同じセッション中は再試行しません。

### 非同期再生

`play_se`、`dwave`、`dwaveloop`はコマンド実行時に即座に音声を再生するのではなく、`PendingSe`キューに追加します。実際の再生は次のフレームの`AudioManager.Update`で行われます。このため、同じフレーム内で複数のSEを追加できますが、大量のSEを同時に再生すると混音が発生します。

### エラーハンドリング

音声ファイルの読み込み・再生に失敗した場合、エンジンは警告ログを出力し、無音で処理を継続します。スクリプトの実行は停止しません。
