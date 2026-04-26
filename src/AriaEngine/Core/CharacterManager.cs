using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AriaEngine.Rendering;

namespace AriaEngine.Core;

public class CharacterInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, string> Expressions { get; set; } = new();
    public Dictionary<string, string> Poses { get; set; } = new();
    public int DefaultX { get; set; } = 0;
    public int DefaultY { get; set; } = 0;
    public float DefaultScale { get; set; } = 1.0f;
    public int DefaultZ { get; set; } = 100;
}

public class CharacterData
{
    public Dictionary<string, CharacterInfo> Characters { get; set; } = new();
}

public class CharacterManager
{
    private Dictionary<string, CharacterInfo> _characters = new();
    private Dictionary<string, int> _activeCharacters = new();
    private readonly Dictionary<string, int> _characterSpriteIds = new(StringComparer.OrdinalIgnoreCase);
    private int _nextCharacterSpriteId = 5000;
    private string _dataPath = "characters.json";
    private GameState _state;
    private TweenManager _tweens;
    private readonly ErrorReporter _reporter;

    public CharacterManager(GameState state, TweenManager tweens, ErrorReporter reporter)
    {
        _state = state;
        _tweens = tweens;
        _reporter = reporter;
    }

    public void LoadCharacterData(string configFile = "")
    {
        string path = string.IsNullOrEmpty(configFile) ? _dataPath : configFile;

        if (!File.Exists(path))
        {
            return; // デフォルトキャラクターを作成
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<CharacterData>(json);
            if (data != null && data.Characters != null)
            {
                _characters = data.Characters;
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"キャラクターデータの読み込みに失敗しました: {ex.Message}", -1, path, AriaErrorLevel.Warning));
        }
    }

    public void ShowCharacter(string characterId, string expression = "normal", string pose = "default")
    {
        var character = GetCharacterInfo(characterId);
        if (character == null)
        {
            _reporter.Report(new AriaError($"キャラクター '{characterId}' が見つかりません。", -1, "", AriaErrorLevel.Warning));
            return;
        }

        // 画像パスを決定
        string imagePath = "";
        if (!string.IsNullOrEmpty(pose) && character.Poses.TryGetValue(pose, out string? posePath))
        {
            imagePath = posePath;
        }
        else if (character.Expressions.TryGetValue(expression, out string? exprPath))
        {
            imagePath = exprPath;
        }
        else if (character.Expressions.TryGetValue("normal", out string? normalPath))
        {
            imagePath = normalPath;
        }

        int spriteId = GetOrAllocateSpriteId(characterId);

        // スプライトを作成
        _state.Sprites[spriteId] = new Sprite
        {
            Id = spriteId,
            Type = SpriteType.Image,
            ImagePath = imagePath,
            X = character.DefaultX,
            Y = character.DefaultY,
            Z = character.DefaultZ,
            ScaleX = character.DefaultScale,
            ScaleY = character.DefaultScale,
            Visible = true
        };

        _activeCharacters[characterId] = spriteId;
    }

    public void HideCharacter(string characterId, int fadeDuration = 300)
    {
        if (!_activeCharacters.TryGetValue(characterId, out int spriteId))
        {
            return;
        }

        if (_state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            if (fadeDuration > 0)
            {
                // フェードアウトアニメーション
                _tweens.Add(new Tween
                {
                    SpriteId = spriteId,
                    Property = "opacity",
                    From = sprite.Opacity,
                    To = 0f,
                    DurationMs = fadeDuration,
                    Ease = EaseType.EaseOut,
                    OnComplete = (state, _) => state.Sprites.Remove(spriteId)
                });
            }
            else
            {
                sprite.Visible = false;
                _state.Sprites.Remove(spriteId);
            }
        }

        _activeCharacters.Remove(characterId);
    }

    public void MoveCharacter(string characterId, int x, int y, int duration = 500)
    {
        if (!_activeCharacters.TryGetValue(characterId, out int spriteId))
        {
            return;
        }

        if (_state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            _tweens.Add(new Tween
            {
                SpriteId = spriteId,
                Property = "x",
                From = sprite.X,
                To = x,
                DurationMs = duration,
                Ease = EaseType.EaseOut
            });

            _tweens.Add(new Tween
            {
                SpriteId = spriteId,
                Property = "y",
                From = sprite.Y,
                To = y,
                DurationMs = duration,
                Ease = EaseType.EaseOut
            });
        }
    }

    public void ChangeExpression(string characterId, string expression)
    {
        if (!_activeCharacters.TryGetValue(characterId, out int spriteId))
        {
            return;
        }

        var character = GetCharacterInfo(characterId);
        if (character == null)
        {
            return;
        }

        if (character.Expressions.TryGetValue(expression, out string? imagePath) && _state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            sprite.ImagePath = imagePath;
        }
    }

    public void ChangePose(string characterId, string pose)
    {
        if (!_activeCharacters.TryGetValue(characterId, out int spriteId))
        {
            return;
        }

        var character = GetCharacterInfo(characterId);
        if (character == null)
        {
            return;
        }

        if (character.Poses.TryGetValue(pose, out string? imagePath) && _state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            sprite.ImagePath = imagePath;
        }
    }

    public bool IsCharacterVisible(string characterId)
    {
        return _activeCharacters.ContainsKey(characterId);
    }

    public CharacterInfo? GetCharacterInfo(string characterId)
    {
        return _characters.TryGetValue(characterId, out var character) ? character : null;
    }

    public void SetCharacterZ(string characterId, int z)
    {
        if (_activeCharacters.TryGetValue(characterId, out int spriteId) && _state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            sprite.Z = z;
        }
    }

    public void SetCharacterScale(string characterId, float scale)
    {
        if (_activeCharacters.TryGetValue(characterId, out int spriteId) && _state.Sprites.TryGetValue(spriteId, out var sprite))
        {
            sprite.ScaleX = scale;
            sprite.ScaleY = scale;
        }
    }

    private int GetOrAllocateSpriteId(string characterId)
    {
        if (_characterSpriteIds.TryGetValue(characterId, out int spriteId))
        {
            return spriteId;
        }

        while (_state.Sprites.ContainsKey(_nextCharacterSpriteId) || _characterSpriteIds.ContainsValue(_nextCharacterSpriteId))
        {
            _nextCharacterSpriteId++;
        }

        spriteId = _nextCharacterSpriteId++;
        _characterSpriteIds[characterId] = spriteId;
        return spriteId;
    }
}
