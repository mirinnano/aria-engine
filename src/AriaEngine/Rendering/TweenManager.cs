using System;
using System.Collections.Generic;
using AriaEngine.Core;

namespace AriaEngine.Rendering;

public enum EaseType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

public class Tween
{
    public int SpriteId { get; set; }
    public string Property { get; set; } = "x"; // "x", "y", "scaleX", "scaleY", "opacity", "color_r", "color_g", "color_b"
    public float From { get; set; }
    public float To { get; set; }
    public float DurationMs { get; set; }
    public float ElapsedMs { get; set; }
    public EaseType Ease { get; set; } = EaseType.Linear;
    public Action<GameState, Sprite>? OnComplete { get; set; }
    public bool IsComplete => ElapsedMs >= DurationMs;
}

public class TweenManager
{
    private List<Tween> _activeTweens = new();

    public EaseType CurrentEaseType { get; set; } = EaseType.Linear;

    public void Add(Tween t)
    {
        _activeTweens.Add(t);
    }

    public bool IsAnimating => _activeTweens.Count > 0;

    public void Update(GameState state, float deltaTimeMs)
    {
        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var t = _activeTweens[i];
            
            if (!state.Sprites.TryGetValue(t.SpriteId, out var sp))
            {
                _activeTweens.RemoveAt(i);
                continue;
            }

            t.ElapsedMs += deltaTimeMs;
            if (t.ElapsedMs > t.DurationMs) t.ElapsedMs = t.DurationMs;

            float progress = t.DurationMs > 0 ? t.ElapsedMs / t.DurationMs : 1.0f;
            float eased = ApplyEasing(progress, t.Ease);
            float current = t.From + (t.To - t.From) * eased;

            switch (t.Property)
            {
                case "x": sp.X = current; break;
                case "y": sp.Y = current; break;
                case "scaleX": sp.ScaleX = current; break;
                case "scaleY": sp.ScaleY = current; break;
                case "opacity": sp.Opacity = current; break;
                case "color_r": 
                    // To animate colors smoothly, we'd need to parse hex into RGB, animate, then back to hex string.
                    // For simplicity, we implement it manually if needed or delegate string properties differently.
                    break;
            }

            if (t.IsComplete)
            {
                // Ensure final value is set exactly
                switch (t.Property)
                {
                    case "x": sp.X = t.To; break;
                    case "y": sp.Y = t.To; break;
                    case "scaleX": sp.ScaleX = t.To; break;
                    case "scaleY": sp.ScaleY = t.To; break;
                    case "opacity": sp.Opacity = t.To; break;
                }
                t.OnComplete?.Invoke(state, sp);
                _activeTweens.RemoveAt(i);
            }
        }
    }

    private float ApplyEasing(float p, EaseType ease)
    {
        return ease switch
        {
            EaseType.Linear => p,
            EaseType.EaseIn => p * p,
            EaseType.EaseOut => p * (2f - p),
            EaseType.EaseInOut => p < 0.5f ? 2f * p * p : -1f + (4f - 2f * p) * p,
            _ => p
        };
    }
}
