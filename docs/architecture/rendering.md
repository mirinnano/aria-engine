# レンダリング

このドキュメントでは、AriaEngineのレンダリングシステムについて詳しく説明します。

## レンダリングシステムの概要

AriaEngineのレンダリングシステムは、Raylibを使用してスプライト、テキスト、エフェクトを描画します。Zオーダー管理、アンチエイリアス、トランジションなどの高度な機能をサポートしています。

### 主なコンポーネント

- **SpriteRenderer**: スプライトの描画
- **TransitionManager**: 画面トランジション
- **TweenManager**: アニメーション管理

## スプライトレンダラー

### SpriteRendererの役割

スプライトレンダラーは以下の機能を提供します：

- スプライトのZオーダーソートと描画
- テキストのレンダリングとラッピング
- フォントのロードと管理
- 色の解析と適用
- 地震エフェクトの適用
- トランジションエフェクトの適用

### レンダリングフロー

```
1. スプライトの収集
   ↓
2. Zオーダーでソート
   ↓
3. 画面クリア
   ↓
4. スプライトを順に描画
   ↓
5. トランジションオーバーレイ描画
   ↓
6. 画面表示
```

### メイン描画ループ

```csharp
public void Draw(GameState state)
{
    // 1. 画面クリア
    ClearBackground(Color.BLACK);

    // 2. 地震エフェクトの適用
    ApplyQuakeEffect();

    // 3. スプライトの収集とソート
    var sortedSprites = state.Sprites.Values
        .Where(s => s.Visible)
        .OrderBy(s => s.Z)
        .ToList();

    // 4. スプライトの描画
    foreach (var sprite in sortedSprites)
    {
        DrawSprite(sprite);
    }

    // 5. トランジションの描画
    if (_transitionManager.IsActive)
    {
        _transitionManager.Draw();
    }

    // 6. 地震エフェクトのリセット
    ResetQuakeEffect();
}
```

## スプライトの描画

### スプライトタイプによる描画分岐

```csharp
private void DrawSprite(Sprite sprite)
{
    switch (sprite.Type)
    {
        case SpriteType.Image:
            DrawImageSprite(sprite);
            break;
        case SpriteType.Text:
            DrawTextSprite(sprite);
            break;
        case SpriteType.Rect:
            DrawRectSprite(sprite);
            break;
    }
}
```

### 画像スプライトの描画

```csharp
private void DrawImageSprite(Sprite sprite)
{
    if (string.IsNullOrEmpty(sprite.ImagePath))
        return;

    // テクスチャのロード
    var texture = LoadTexture(sprite.ImagePath);
    if (!texture.IsReady)
        return;

    // スケールと回転を適用
    var width = (int)(texture.Width * sprite.Scale);
    var height = (int)(texture.Height * sprite.Scale);

    // 透明度を適用
    var color = new Color(
        (byte)(sprite.Tint.R * sprite.Alpha / 255),
        (byte)(sprite.Tint.G * sprite.Alpha / 255),
        (byte)(sprite.Tint.B * sprite.Alpha / 255),
        (byte)sprite.Alpha
    );

    // 描画
    var sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
    var destRect = new Rectangle(sprite.X, sprite.Y, width, height);
    var origin = new Vector2(width / 2f, height / 2f);

    Raylib.DrawTexturePro(
        texture,
        sourceRect,
        destRect,
        origin,
        sprite.Rotation,
        color
    );
}
```

### テキストスプライトの描画

```csharp
private void DrawTextSprite(Sprite sprite)
{
    if (string.IsNullOrEmpty(sprite.Text))
        return;

    // フォントの取得
    var font = GetFont();
    if (font == null)
        return;

    // テキストサイズの計算
    var fontSize = sprite.FontSize > 0 ? sprite.FontSize : DefaultFontSize;
    var spacing = fontSize / 10f;

    // テキストの幅と高さを計算
    var textSize = Raylib.MeasureTextEx(font, sprite.Text, fontSize, spacing);

    // 幅制限がある場合はテキストをラップ
    if (sprite.Width > 0 && textSize.X > sprite.Width)
    {
        DrawWrappedText(sprite, font, fontSize, spacing);
    }
    else
    {
        DrawSingleLineText(sprite, font, fontSize, spacing);
    }
}
```

### テキストのラッピング

```csharp
private void DrawWrappedText(Sprite sprite, Font font, float fontSize, float spacing)
{
    var words = sprite.Text.Split(' ');
    var currentLine = new StringBuilder();
    var currentY = sprite.Y;
    var lineHeight = fontSize + spacing;

    foreach (var word in words)
    {
        var testLine = currentLine.Length > 0 ?
            $"{currentLine} {word}" : word;

        var testSize = Raylib.MeasureTextEx(font, testLine, fontSize, spacing);

        if (testSize.X > sprite.Width && currentLine.Length > 0)
        {
            // 現在の行を描画
            DrawTextLine(sprite, font, currentLine.ToString(), sprite.X, currentY, fontSize, spacing);
            currentLine.Clear();
            currentY += lineHeight;
            currentLine.Append(word);
        }
        else
        {
            currentLine.Append(testLine);
        }
    }

    // 最後の行を描画
    if (currentLine.Length > 0)
    {
        DrawTextLine(sprite, font, currentLine.ToString(), sprite.X, currentY, fontSize, spacing);
    }
}
```

### 矩形スプライトの描画

```csharp
private void DrawRectSprite(Sprite sprite)
{
    var width = sprite.Width > 0 ? sprite.Width : 100;
    var height = sprite.Height > 0 ? sprite.Height : 100;

    // 透明度を適用
    var color = new Color(
        (byte)(sprite.FillColor.R * sprite.Alpha / 255),
        (byte)(sprite.FillColor.G * sprite.Alpha / 255),
        (byte)(sprite.FillColor.B * sprite.Alpha / 255),
        (byte)sprite.Alpha
    );

    // 角丸矩形の描画
    if (sprite.Roundness > 0)
    {
        Raylib.DrawRectangleRounded(
            new Rectangle(sprite.X, sprite.Y, width, height),
            sprite.Roundness,
            10,
            color
        );
    }
    else
    {
        Raylib.DrawRectangle(sprite.X, sprite.Y, width, height, color);
    }

    // 枠線の描画
    if (sprite.BorderWidth > 0)
    {
        var borderColor = new Color(
            sprite.BorderColor.R,
            sprite.BorderColor.G,
            sprite.BorderColor.B,
            (byte)(sprite.BorderColor.A * sprite.Alpha / 255)
        );

        if (sprite.Roundness > 0)
        {
            Raylib.DrawRectangleRoundedLines(
                new Rectangle(sprite.X, sprite.Y, width, height),
                sprite.Roundness,
                10,
                sprite.BorderWidth,
                borderColor
            );
        }
        else
        {
            Raylib.DrawRectangleLines(
                sprite.X,
                sprite.Y,
                width,
                height,
                borderColor
            );
        }
    }

    // 影の描画
    if (sprite.ShadowOpacity > 0)
    {
        var shadowColor = new Color(
            sprite.ShadowColor.R,
            sprite.ShadowColor.G,
            sprite.ShadowColor.B,
            (byte)(sprite.ShadowColor.A * sprite.ShadowOpacity / 255)
        );

        var shadowRect = new Rectangle(
            sprite.X + sprite.ShadowOffset.X,
            sprite.Y + sprite.ShadowOffset.Y,
            width,
            height
        );

        if (sprite.Roundness > 0)
        {
            Raylib.DrawRectangleRounded(
                shadowRect,
                sprite.Roundness,
                10,
                shadowColor
            );
        }
        else
        {
            Raylib.DrawRectangleRec(shadowRect, shadowColor);
        }
    }
}
```

## フォント管理

### フォントのロード

```csharp
private Font LoadFont(string fontPath, int fontSize)
{
    // キャッシュをチェック
    string cacheKey = $"{fontPath}_{fontSize}";
    if (_fontCache.TryGetValue(cacheKey, out var cachedFont))
    {
        return cachedFont;
    }

    // フォントをロード
    var font = Raylib.LoadFontEx(fontPath, fontSize, null, 4000);

    if (!font.IsReady)
    {
        throw new Exception($"Failed to load font: {fontPath}");
    }

    // フィルターを適用
    Raylib.SetTextureFilter(font.Texture, _fontFilter);

    // キャッシュに保存
    _fontCache[cacheKey] = font;

    return font;
}
```

### フォントフィルターの適用

```csharp
public void SetFontFilter(TextureFilter filter)
{
    _fontFilter = filter;

    // 既存のフォントにフィルターを適用
    foreach (var font in _fontCache.Values)
    {
        Raylib.SetTextureFilter(font.Texture, filter);
    }
}
```

## 色の解析

### 16進数色コードの解析

```csharp
private Color ParseColor(string colorCode)
{
    if (string.IsNullOrEmpty(colorCode))
        return Color.WHITE;

    // #を削除
    colorCode = colorCode.TrimStart('#');

    // 6桁の16進数を解析
    if (colorCode.Length == 6)
    {
        int r = Convert.ToInt32(colorCode.Substring(0, 2), 16);
        int g = Convert.ToInt32(colorCode.Substring(2, 2), 16);
        int b = Convert.ToInt32(colorCode.Substring(4, 2), 16);

        return new Color(r, g, b, 255);
    }
    // 8桁の16進数（透明度含む）
    else if (colorCode.Length == 8)
    {
        int r = Convert.ToInt32(colorCode.Substring(0, 2), 16);
        int g = Convert.ToInt32(colorCode.Substring(2, 2), 16);
        int b = Convert.ToInt32(colorCode.Substring(4, 2), 16);
        int a = Convert.ToInt32(colorCode.Substring(6, 2), 16);

        return new Color(r, g, b, a);
    }

    return Color.WHITE;
}
```

## Zオーダー管理

### Zオーダーのソート

```csharp
private List<Sprite> GetSortedSprites(GameState state)
{
    return state.Sprites.Values
        .Where(s => s.Visible && s.Alpha > 0)
        .OrderBy(s => s.Z)
        .ThenBy(s => s.Id) // 同じZの場合はIDでソート
        .ToList();
}
```

### Zオーダーの例

```aria
; 背景（最奥）
lsp 10, "bg.png", 0, 0
sp_z 10, 0

; キャラクター（中間）
lsp 20, "hero.png", 800, 100
sp_z 20, 50

; UI（最前面）
lsp 30, "ui.png", 0, 0
sp_z 30, 100
```

## 地震エフェクト

### 地震エフェクトの適用

```csharp
private Vector2 _quakeOffset = Vector2.Zero;
private float _quakeIntensity = 0f;
private float _quakeDuration = 0f;

public void StartQuake(float intensity, float duration)
{
    _quakeIntensity = intensity;
    _quakeDuration = duration;
}

private void ApplyQuakeEffect()
{
    if (_quakeDuration > 0)
    {
        var randomX = (float)(_random.NextDouble() - 0.5) * 2 * _quakeIntensity;
        var randomY = (float)(_random.NextDouble() - 0.5) * 2 * _quakeIntensity;

        _quakeOffset = new Vector2(randomX, randomY);
        Raylib.TranslateTransform(_quakeOffset.X, _quakeOffset.Y);

        _quakeDuration -= GetFrameTime();
    }
    else
    {
        _quakeOffset = Vector2.Zero;
    }
}

private void ResetQuakeEffect()
{
    if (_quakeOffset != Vector2.Zero)
    {
        Raylib.TranslateTransform(-_quakeOffset.X, -_quakeOffset.Y);
    }
}
```

## トランジション

### トランジションの種類

1. **フェード**: フェードイン/フェードアウト
2. **スライド**: 上下左右へのスライド
3. **拡大縮小**: 拡大/縮小
4. **ブラインド**: ブラインドエフェクト

### トランジションの実装

```csharp
public class TransitionManager
{
    private enum TransitionType
    {
        None,
        FadeIn,
        FadeOut,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown
    }

    private TransitionType _currentTransition = TransitionType.None;
    private float _progress = 0f;
    private float _duration = 0f;
    private Color _transitionColor = Color.BLACK;

    public void StartFadeIn(float duration, Color color)
    {
        _currentTransition = TransitionType.FadeIn;
        _duration = duration;
        _progress = 0f;
        _transitionColor = color;
    }

    public void StartFadeOut(float duration, Color color)
    {
        _currentTransition = TransitionType.FadeOut;
        _duration = duration;
        _progress = 0f;
        _transitionColor = color;
    }

    public void Update(float deltaTime)
    {
        if (_currentTransition == TransitionType.None)
            return;

        _progress += deltaTime;

        if (_progress >= _duration)
        {
            _currentTransition = TransitionType.None;
            _progress = 0f;
        }
    }

    public void Draw()
    {
        if (_currentTransition == TransitionType.None)
            return;

        float alpha = _progress / _duration;

        switch (_currentTransition)
        {
            case TransitionType.FadeIn:
                DrawFadeTransition(1 - alpha);
                break;
            case TransitionType.FadeOut:
                DrawFadeTransition(alpha);
                break;
        }
    }

    private void DrawFadeTransition(float alpha)
    {
        var color = new Color(
            _transitionColor.R,
            _transitionColor.G,
            _transitionColor.B,
            (byte)(alpha * 255)
        );

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), color);
    }

    public bool IsActive => _currentTransition != TransitionType.None;
}
```

## パフォーマンスの最適化

### テクスチャキャッシュ

```csharp
private readonly Dictionary<string, Texture2D> _textureCache =
    new Dictionary<string, Texture2D>();

private Texture2D LoadTexture(string path)
{
    if (_textureCache.TryGetValue(path, out var cachedTexture))
    {
        return cachedTexture;
    }

    var texture = Raylib.LoadTexture(path);
    _textureCache[path] = texture;

    return texture;
}
```

### バッチ描画

可能な限りバッチ描画を使用して、描画コールを減らします。

```csharp
private void DrawBatchedSprites(List<Sprite> sprites)
{
    Raylib.BeginDrawing();

    foreach (var sprite in sprites)
    {
        DrawSprite(sprite);
    }

    Raylib.EndDrawing();
}
```

### 可視カリング

画面外のスプライトを描画から除外します。

```csharp
private bool IsVisible(Sprite sprite)
{
    var screenWidth = Raylib.GetScreenWidth();
    var screenHeight = Raylib.GetScreenHeight();

    return sprite.X >= -sprite.Width &&
           sprite.Y >= -sprite.Height &&
           sprite.X <= screenWidth &&
           sprite.Y <= screenHeight;
}
```

## まとめ

AriaEngineのレンダリングシステムは以下の特徴を持っています：

1. **効率的な描画**: Zオーダーソートとバッチ描画
2. **高品質なテキスト**: アンチエイリアスとラッピング
3. **柔軟なエフェクト**: 地震、トランジション、アニメーション
4. **パフォーマンス最適化**: キャッシュと可視カリング
5. **拡張性**: 新しいスプライトタイプとエフェクトを追加可能

これでAriaEngineのアーキテクチャドキュメントが完成しました！

次は、実際にエンジンを使用してゲームを作成してみましょう：

- [最初のプロジェクト作成](../tutorials/getting-started.md) - 環境セットアップから最初のスクリプトまで
