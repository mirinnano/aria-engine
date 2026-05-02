using System.Collections.Generic;
using System.Linq;

namespace AriaEngine.Core;

/// <summary>
/// 互換UIスプライトを管理するクラス
/// </summary>
public class CompatUiManager
{
    private readonly GameState _state;
    private int _nextCompatUiSpriteId = SpriteConstants.CompatUiStartId;
    private readonly HashSet<int> _activeSpriteIds = new();

    public CompatUiManager(GameState state)
    {
        _state = state;
    }

    /// <summary>
    /// 互換UIスプライト用のIDを割り当てる
    /// </summary>
    public int AllocateSpriteId()
    {
        while (_state.Render.Sprites.ContainsKey(_nextCompatUiSpriteId))
        {
            _nextCompatUiSpriteId++;
        }
        return _nextCompatUiSpriteId++;
    }

    /// <summary>
    /// 互換UIスプライトを追跡する
    /// </summary>
    public void TrackSprite(int spriteId)
    {
        _activeSpriteIds.Add(spriteId);
    }

    /// <summary>
    /// すべての互換UIスプライトをクリアする
    /// </summary>
    public void ClearAllSprites()
    {
        if (_activeSpriteIds.Count == 0) return;

        foreach (int id in _activeSpriteIds)
        {
            _state.Render.Sprites.Remove(id);
            _state.Interaction.SpriteButtonMap.Remove(id);
        }

        _activeSpriteIds.Clear();
    }

    /// <summary>
    /// スプライトが一時的な互換UIスプライトかどうかを判定する
    /// </summary>
    public static bool IsTransientCompatSprite(Sprite sprite)
    {
        return sprite.Id >= SpriteConstants.CompatUiStartId && sprite.Z >= SpriteConstants.CompatUiMinZIndex;
    }

    /// <summary>
    /// 現在のスプライトから一時的な互換UIスプライトを追跡リストに追加する
    /// </summary>
    public void ScanAndTrackTransientSprites()
    {
        foreach (var sprite in _state.Render.Sprites.Values)
        {
            if (IsTransientCompatSprite(sprite))
            {
                _activeSpriteIds.Add(sprite.Id);
            }
        }

        _nextCompatUiSpriteId = Math.Max(SpriteConstants.CompatUiStartId, _state.Render.Sprites.Count == 0 ? SpriteConstants.CompatUiStartId : _state.Render.Sprites.Keys.Max() + 1);
    }

    /// <summary>
    /// どれかのボタンが表示されているかを確認する
    /// </summary>
    public bool HasAnyVisibleButton()
    {
        return _state.Render.Sprites.Values.Any(s => s.Visible && s.IsButton);
    }

    /// <summary>
    /// 追跡中のスプライト数を取得する
    /// </summary>
    public int TrackedSpriteCount => _activeSpriteIds.Count;
}
