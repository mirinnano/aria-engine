using System;
using System.Collections.Generic;
using AriaEngine.Core;

namespace AriaEngine.Rendering;

public enum EaseType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    Bounce,
    Elastic,
    Back,
    Spring,
    SineWave,
    Cubic,
    Quart,
    Expo
}

public enum TweenProperty
{
    X,
    Y,
    ScaleX,
    ScaleY,
    Opacity
}

public class Tween
{
    public int SpriteId { get; set; }
    public TweenProperty Property { get; set; } = TweenProperty.X;
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

    private static void ApplyValue(Sprite sp, TweenProperty prop, float value)
    {
        switch (prop)
        {
            case TweenProperty.X: sp.X = value; break;
            case TweenProperty.Y: sp.Y = value; break;
            case TweenProperty.ScaleX: sp.ScaleX = value; break;
            case TweenProperty.ScaleY: sp.ScaleY = value; break;
            case TweenProperty.Opacity: sp.Opacity = value; break;
        }
    }

    public void FinishAll(GameState state)
    {
        while (_activeTweens.Count > 0)
        {
            var finishing = _activeTweens.ToArray();
            _activeTweens.Clear();

            foreach (var t in finishing)
            {
                if (!state.Render.Sprites.TryGetValue(t.SpriteId, out var sp)) continue;
                ApplyValue(sp, t.Property, t.To);
                t.OnComplete?.Invoke(state, sp);
            }
        }
    }

    public void Update(GameState state, float deltaTimeMs)
    {
        for (int i = _activeTweens.Count - 1; i >= 0; i--)
        {
            var t = _activeTweens[i];
            
            if (!state.Render.Sprites.TryGetValue(t.SpriteId, out var sp))
            {
                _activeTweens.RemoveAt(i);
                continue;
            }

            t.ElapsedMs += deltaTimeMs;
            if (t.ElapsedMs > t.DurationMs) t.ElapsedMs = t.DurationMs;

            float progress = t.DurationMs > 0 ? t.ElapsedMs / t.DurationMs : 1.0f;
            float eased = ApplyEasing(progress, t.Ease);
            float current = t.From + (t.To - t.From) * eased;

            ApplyValue(sp, t.Property, current);

            if (t.IsComplete)
            {
                ApplyValue(sp, t.Property, t.To);
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
            EaseType.Bounce => EaseOutBounce(p),
            EaseType.Elastic => p is 0f or 1f ? p : MathF.Pow(2f, -10f * p) * MathF.Sin((p * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f,
            EaseType.Back => 1f + 2.70158f * MathF.Pow(p - 1f, 3f) + 1.70158f * MathF.Pow(p - 1f, 2f),
            EaseType.Spring => 1f - MathF.Exp(-6f * p) * MathF.Cos(12f * p),
            EaseType.SineWave => (1f - MathF.Cos(p * MathF.PI)) * 0.5f,
            EaseType.Cubic => 1f - MathF.Pow(1f - p, 3f),
            EaseType.Quart => 1f - MathF.Pow(1f - p, 4f),
            EaseType.Expo => p >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * p),
            _ => p
        };
    }

    private static float EaseOutBounce(float p)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (p < 1f / d1) return n1 * p * p;
        if (p < 2f / d1)
        {
            p -= 1.5f / d1;
            return n1 * p * p + 0.75f;
        }
        if (p < 2.5f / d1)
        {
            p -= 2.25f / d1;
            return n1 * p * p + 0.9375f;
        }

        p -= 2.625f / d1;
        return n1 * p * p + 0.984375f;
    }
}
