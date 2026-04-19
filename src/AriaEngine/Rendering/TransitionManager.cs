using Raylib_cs;
using AriaEngine.Core;

namespace AriaEngine.Rendering;

public class TransitionManager
{
    private float _timer = 0;

    public void Update(VirtualMachine vm, float deltaTime)
    {
        if (vm.State.QuakeTimerMs > 0)
        {
            vm.State.QuakeTimerMs -= deltaTime * 1000f;
            if (vm.State.QuakeTimerMs < 0) vm.State.QuakeTimerMs = 0;
        }

        if (!vm.State.IsFading) return;
        
        _timer += deltaTime * 1000f; // ms
        if (_timer >= vm.State.FadeDurationMs)
        {
            _timer = 0;
            if (vm.State.State == VmState.FadingIn) vm.State.FadeProgress = 1.0f;
            if (vm.State.State == VmState.FadingOut) vm.State.FadeProgress = 0.0f;
            vm.FinishFade();
            return;
        }

        float progress = _timer / vm.State.FadeDurationMs;
        if (vm.State.State == VmState.FadingIn)
        {
            vm.State.FadeProgress = progress;
        }
        else if (vm.State.State == VmState.FadingOut)
        {
            vm.State.FadeProgress = 1.0f - progress;
        }
    }

    public void Draw(GameState state)
    {
        if (state.FadeProgress < 1.0f)
        {
            int alpha = (int)((1.0f - state.FadeProgress) * 255);
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, alpha));
        }
    }
}
