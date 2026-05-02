using System;
using Raylib_cs;
using AriaEngine.Core;

namespace AriaEngine.Rendering;

public class TransitionManager
{
    private float _timer = 0;

    public void Update(VirtualMachine vm, float deltaTime)
    {
        if (!vm.State.Render.IsFading) return;
        
        _timer += deltaTime * 1000f; // ms
        if (_timer >= vm.State.Render.FadeDurationMs)
        {
            _timer = 0;
            if (vm.State.Execution.State == VmState.FadingIn) vm.State.Render.FadeProgress = 1.0f;
            if (vm.State.Execution.State == VmState.FadingOut) vm.State.Render.FadeProgress = 0.0f;
            vm.FinishFade();
            return;
        }

        float progress = _timer / vm.State.Render.FadeDurationMs;
        if (vm.State.Execution.State == VmState.FadingIn)
        {
            vm.State.Render.FadeProgress = progress;
        }
        else if (vm.State.Execution.State == VmState.FadingOut)
        {
            vm.State.Render.FadeProgress = 1.0f - progress;
        }
    }

    public void Draw(GameState state)
    {
        if (state.Render.FadeProgress >= 1.0f) return;

        float screenW = Raylib.GetScreenWidth();
        float screenH = Raylib.GetScreenHeight();
        float progress = Math.Clamp(state.Render.FadeProgress, 0f, 1f);
        int alpha = (int)((1.0f - progress) * 255);
        Color overlayColor = new(0, 0, 0, alpha);

        switch (state.Render.TransitionStyle)
        {
            case TransitionType.Fade:
            default:
                Raylib.DrawRectangle(0, 0, (int)screenW, (int)screenH, overlayColor);
                break;

            case TransitionType.SlideLeft:
            {
                int curtainX = (int)(screenW * (1.0f - progress));
                Raylib.DrawRectangle(curtainX, 0, (int)screenW - curtainX, (int)screenH, overlayColor);
                break;
            }

            case TransitionType.SlideRight:
            {
                int curtainW = (int)(screenW * progress);
                Raylib.DrawRectangle(0, 0, curtainW, (int)screenH, overlayColor);
                break;
            }

            case TransitionType.SlideUp:
            {
                int curtainY = (int)(screenH * (1.0f - progress));
                Raylib.DrawRectangle(0, curtainY, (int)screenW, (int)screenH - curtainY, overlayColor);
                break;
            }

            case TransitionType.SlideDown:
            {
                int curtainH = (int)(screenH * progress);
                Raylib.DrawRectangle(0, 0, (int)screenW, curtainH, overlayColor);
                break;
            }

            case TransitionType.WipeCircle:
            {
                float centerX = screenW / 2f;
                float centerY = screenH / 2f;
                float maxRadius = MathF.Sqrt(screenW * screenW + screenH * screenH) / 2f;
                float radius = progress * maxRadius;
                // Draw expanding black circle from center
                // Raylib's DrawCircle with large radius + high segment count gives filled circle
                Raylib.DrawCircle((int)centerX, (int)centerY, radius, overlayColor);
                break;
            }
        }
    }
}
