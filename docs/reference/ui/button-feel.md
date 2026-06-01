# ButtonFeel — ボタンの押下感

`ButtonFeel` は、AriaEngine の UI ボタンに **視覚的な押下フィードバック** を与えるためのテーマ設定です。
「ボタンを押したのに何も起きている感じがしない」という従来の課題を解決します。

> **T2 UX Quick Wins の一部として導入**: Save サムネイルバグ修正 (T1) と ADV テキスト中央配置 (T3) と並ぶ UX 改善です。

---

## 概要

ボタン (`IsButton = true` のスプライト) が押されている間、以下の視覚的フィードバックが自動で適用されます:

| 効果 | 設定キー | デフォルト値 | 説明 |
|------|----------|-------------|------|
| 押下時の色 | `PressedColor` | `""` (テーマ既定色) | 塗りつぶし色を上書き。空文字なら色変更なし |
| 押下時の Y オフセット | `PressedOffsetY` | `1.5f` | ピクセル単位の沈み込み量（正の値で下方向） |
| 押下時のスケール | `PressedScale` | `0.97f` | 0.97 = 3% 縮小。沈み込みと組み合わせて「押された感」を演出 |
| アニメーション時間 | `AnimationDurationMs` | `80f` | ホバー/押下アニメの継続時間 (ms)。現バージョンでは参照のみ |
| クリック SE パス | `ClickSoundPath` | `assets/se/sys_click.wav` | クリック時の効果音（テーマ差し替え用フィールド） |
| ホバー SE パス | `HoverSoundPath` | `assets/se/sys_hover.wav` | ホバー時の効果音（テーマ差し替え用フィールド） |

---

## 使い方

`ButtonFeel` は **テーマ設定** として組み込まれています。スクリプトから直接操作する必要はなく、`init.aria` でテーマを切り替えるか、`ApplyTheme("...")` を呼ぶだけで自動的に適用されます。

```aria
; init.aria でテーマを soft に設定すると、押下感が大きめに
theme "soft"
```

テーマ別のデフォルト値:

| テーマ | PressedColor | PressedOffsetY | PressedScale | AnimationDurationMs |
|--------|--------------|----------------|--------------|---------------------|
| **Classic** | `#181818` | `1.5` | `0.97` | `80` |
| **Soft**    | `#0d1014` | `1.8` | `0.96` | `90` |
| **Glass**   | `#081114` | `1.5` | `0.97` | `80` |
| **Mono**    | `#000000` | `1.0` | `0.98` | `60` |
| (default)   | `""`     | `1.5` | `0.97` | `80` |

---

## 動作の仕組み

1. **入力** — `InputHandler` がマウス押下を検出すると、押されているボタンの `IsPressed` フラグが `true` になります。
2. **描画** — `SpriteRenderer` の `UpdateUiPresentation()` が `IsPressed && IsButton` を検知すると、`RenderScaleX/Y` を `PressedScale` で上書きします。
3. **適用** — `DrawRectSprite()` が色と Y オフセットを適用してフレームを描画します。
4. **リリース** — マウスボタンを離すと `IsPressed` が `false` に戻り、すべての視覚効果が即座に解除されます。

> ドラッグオフ（クリック後にマウスをボタンの外に移動）しても、`_pressedButtonId` は維持されるため、押下視覚効果はボタンに留まり続けます。これは「クリックして指を離す」までの自然な挙動を再現します。

---

## 後方互換性

- **新規スプライトの `IsPressed` は既定で `false`**: 既存のスクリプトは変更なしで動作します。
- **`IsPressed` は renderer-owned runtime 状態**: `[JsonIgnore]` 属性付きのため、セーブ/ロードで永続化されません。
- **押下感のテーマ設定が空文字の場合**: 既存テーマとの後方互換性のため、色変更はスキップされます（オフセットとスケールは常に適用）。

---

## 関連 API

| 種類 | 名前 | 説明 |
|------|------|------|
| プロパティ | `GameState.ButtonFeel` | 現在の `ButtonFeel` インスタンス |
| フラグ | `Sprite.IsPressed` | renderer-owned な押下中フラグ |
| クラス | `ButtonFeel` | 7 つのテーマプロパティを保持 |
| メソッド | `UiThemeManager.ApplyTheme("classic"\|"soft"\|"glass"\|"mono")` | テーマ適用（押下感も同時に設定） |
| メソッド | `UiThemeManager.ResetToDefaults()` | デフォルト押下感に戻す |

---

## テストカバレッジ

- `GameState.ButtonFeel` が初期化されることの確認
- `ButtonFeel` のデフォルト値が期待通り（`PressedOffsetY=1.5`, `PressedScale=0.97`）
- 全 4 テーマが `ButtonFeel.HoverColor` を設定することの確認
- `ResetToDefaults` が `ButtonFeel` を初期値にリセットすることの確認
- `Sprite.IsPressed` のデフォルト値 `false` と切り替え動作
