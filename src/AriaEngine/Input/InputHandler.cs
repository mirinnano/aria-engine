using Raylib_cs;
using AriaEngine.Core;

namespace AriaEngine.Input;

public class InputHandler
{
    public void Update(VirtualMachine vm)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) vm.State.DebugMode = !vm.State.DebugMode;
        vm.State.ForceSkipMode = IsForceSkipHeld();

        // スキップ中に読み進めキーが押されたら即停止
        if ((vm.State.SkipMode || vm.State.ForceSkipMode) && IsAdvancePressed())
        {
            vm.StopSkip();
        }

        foreach (var hotkey in vm.State.UiHotkeys)
        {
            if (TryParseKey(hotkey.Key, out var key) && Raylib.IsKeyPressed(key))
            {
                vm.JumpTo(hotkey.Value);
                return;
            }
        }

        if (vm.State.State == VmState.WaitingForClick)
        {
            if (vm.State.SkipMode && !vm.State.ForceSkipMode && IsAdvancePressed())
            {
                vm.StopSkip();
                return;
            }

            if (IsAdvancePressed())
            {
                if (vm.State.IsWaitingPageClear)
                {
                    vm.State.CurrentTextBuffer = "";
                    vm.State.DisplayedTextLength = 0;
                    vm.State.IsWaitingPageClear = false;
                }
                vm.ResumeFromClick();
            }
        }
        else if (vm.State.State == VmState.WaitingForAnimation)
        {
            if (vm.State.TextSpeedMs > 0 && vm.State.DisplayedTextLength < vm.State.CurrentTextBuffer.Length)
            {
                if (IsAdvancePressed())
                {
                    if (vm.State.SkipMode && !vm.State.ForceSkipMode) vm.StopSkip();
                    if (CanAdvanceTextAnimation(vm.State))
                    {
                        vm.State.DisplayedTextLength = vm.State.CurrentTextBuffer.Length;
                        vm.State.State = VmState.Running;
                    }
                    else
                    {
                        vm.State.DisplayedTextLength = vm.State.CurrentTextBuffer.Length;
                    }
                }
            }
        }
        else if (vm.State.State == VmState.WaitingForButton)
        {
            if (vm.State.ButtonTimeoutMs > 0)
            {
                vm.State.ButtonTimer += Raylib.GetFrameTime() * 1000f;
                if (vm.State.ButtonTimer >= vm.State.ButtonTimeoutMs)
                {
                    vm.SignalTimeout();
                    return;
                }
            }

            var mousePoint = Raylib.GetMousePosition();
            bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
            Sprite? clickedButton = null;
            int clickedZ = int.MinValue;

            foreach (var kvp in vm.State.Sprites)
            {
                var btn = kvp.Value;
                if (!btn.Visible || !btn.IsButton)
                {
                    btn.IsHovered = false;
                    continue;
                }

                float scaleX = btn.ScaleX;
                float scaleY = btn.ScaleY;
                float bx = btn.X + btn.ClickAreaX;
                float by = btn.Y + btn.ClickAreaY;

                // Use click area dimensions if set, otherwise use sprite dimensions
                // For text sprites with zero dimensions, use a default minimum size
                int bw = btn.ClickAreaW > 0 ? btn.ClickAreaW : Math.Max(btn.Width, 50);
                int bh = btn.ClickAreaH > 0 ? btn.ClickAreaH : Math.Max(btn.Height, 30);

                Rectangle rect = new Rectangle(bx, by, bw * scaleX, bh * scaleY);
                btn.IsHovered = Raylib.CheckCollisionPointRec(mousePoint, rect);
                if (btn.IsHovered)
                {
                    int resultValue = vm.State.SpriteButtonMap.TryGetValue(btn.Id, out int mappedValue) ? mappedValue : btn.Id;
                    if (TryDispatchHoverEvent(vm, btn.Id, resultValue)) return;
                }
                else
                {
                    vm.State.UiHoverActive.Remove(btn.Id);
                }

                if (clicked && btn.IsHovered)
                {
                    if (btn.Z >= clickedZ)
                    {
                        clickedButton = btn;
                        clickedZ = btn.Z;
                    }
                }
            }

            if (clickedButton != null)
            {
                // スライダークリック処理
                if (clickedButton.SliderMin < clickedButton.SliderMax)
                {
                    float bx = clickedButton.X + clickedButton.ClickAreaX;
                    float bw = clickedButton.ClickAreaW > 0 ? clickedButton.ClickAreaW : Math.Max(clickedButton.Width, 50);
                    float ratio = Math.Clamp((mousePoint.X - bx) / (bw * clickedButton.ScaleX), 0f, 1f);
                    int newValue = clickedButton.SliderMin + (int)Math.Round(ratio * (clickedButton.SliderMax - clickedButton.SliderMin));
                    clickedButton.SliderValue = newValue;
                    vm.SetReg($"%{clickedButton.Id}", newValue);
                    UpdateSliderVisuals(vm.State, clickedButton);
                }
                // チェックボックスクリック処理
                else if (!string.IsNullOrEmpty(clickedButton.CheckboxLabel))
                {
                    clickedButton.CheckboxValue = !clickedButton.CheckboxValue;
                    vm.SetReg($"%{clickedButton.Id}", clickedButton.CheckboxValue ? 1 : 0);
                    UpdateCheckboxVisuals(vm.State, clickedButton);
                }
                vm.ResumeFromButton(clickedButton.Id);
                return;
            }

            // スライダードラッグ対応（マウス押下中も連続更新）
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                foreach (var kvp in vm.State.Sprites)
                {
                    var sp = kvp.Value;
                    if (!sp.Visible || sp.SliderMin >= sp.SliderMax) continue;
                    float bx = sp.X + sp.ClickAreaX;
                    float bw = sp.ClickAreaW > 0 ? sp.ClickAreaW : Math.Max(sp.Width, 50);
                    Rectangle rect = new Rectangle(bx, sp.Y + sp.ClickAreaY, bw * sp.ScaleX, Math.Max(sp.Height, 30) * sp.ScaleY);
                    if (Raylib.CheckCollisionPointRec(mousePoint, rect))
                    {
                        float ratio = Math.Clamp((mousePoint.X - bx) / (bw * sp.ScaleX), 0f, 1f);
                        int newValue = sp.SliderMin + (int)Math.Round(ratio * (sp.SliderMax - sp.SliderMin));
                        if (newValue != sp.SliderValue)
                        {
                            sp.SliderValue = newValue;
                            vm.SetReg($"%{sp.Id}", newValue);
                            UpdateSliderVisuals(vm.State, sp);
                        }
                    }
                }
            }
        }
    }

    private static bool IsAdvancePressed()
    {
        bool keyboardOrMouse =
            Raylib.IsMouseButtonPressed(MouseButton.Left) ||
            Raylib.GetMouseWheelMove() < 0f ||
            Raylib.IsKeyPressed(KeyboardKey.Enter) ||
            Raylib.IsKeyPressed(KeyboardKey.KpEnter) ||
            Raylib.IsKeyPressed(KeyboardKey.Space) ||
            Raylib.IsKeyPressed(KeyboardKey.Z);

        if (keyboardOrMouse) return true;

        for (int pad = 0; pad < 4; pad++)
        {
            if (Raylib.IsGamepadAvailable(pad) &&
                Raylib.IsGamepadButtonPressed(pad, GamepadButton.RightFaceDown))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsForceSkipHeld()
    {
        return Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
    }

    private static bool CanAdvanceTextAnimation(GameState state)
    {
        int total = Math.Max(1, state.CurrentTextBuffer.Length);
        float ratio = Math.Clamp(state.DisplayedTextLength / (float)total, 0f, 1f);
        return state.TextAdvanceMode switch
        {
            "any" or "immediate" => true,
            "ratio" => ratio >= Math.Clamp(state.TextAdvanceRatio, 0f, 1f),
            _ => state.DisplayedTextLength >= state.CurrentTextBuffer.Length
        };
    }

    private static void UpdateSliderVisuals(GameState state, Sprite slider)
    {
        int fillId = slider.Id + 1;
        int thumbId = slider.Id + 2;
        int valueId = slider.Id + 3;
        float ratio = slider.SliderMax > slider.SliderMin ? (float)(slider.SliderValue - slider.SliderMin) / (slider.SliderMax - slider.SliderMin) : 0f;

        if (state.Sprites.TryGetValue(fillId, out var fill)) fill.Width = (int)(slider.Width * ratio);
        if (state.Sprites.TryGetValue(thumbId, out var thumb)) thumb.X = slider.X + (int)(slider.Width * ratio) - 8;
        if (state.Sprites.TryGetValue(valueId, out var valText)) valText.Text = slider.SliderValue.ToString();
    }

    private static void UpdateCheckboxVisuals(GameState state, Sprite checkbox)
    {
        int checkId = checkbox.Id + 1;
        checkbox.FillColor = checkbox.CheckboxValue ? "#f5f5f5" : "#000000";
        checkbox.FillAlpha = checkbox.CheckboxValue ? 255 : 0;
        if (state.Sprites.TryGetValue(checkId, out var checkMark)) checkMark.Text = checkbox.CheckboxValue ? "v" : "";
    }

    private static bool TryDispatchHoverEvent(VirtualMachine vm, int spriteId, int resultValue)
    {
        if (vm.State.UiEvents.TryGetValue($"{spriteId}:hover", out string? label) ||
            vm.State.UiEvents.TryGetValue($"{resultValue}:hover", out label))
        {
            if (!vm.State.UiHoverActive.Add(spriteId)) return false;
            vm.JumpTo(label);
            return true;
        }

        return false;
    }

    private static bool TryParseKey(string name, out KeyboardKey key)
    {
        key = name.Trim().ToLowerInvariant() switch
        {
            "escape" or "esc" => KeyboardKey.Escape,
            "enter" or "return" => KeyboardKey.Enter,
            "space" => KeyboardKey.Space,
            "up" => KeyboardKey.Up,
            "down" => KeyboardKey.Down,
            "left" => KeyboardKey.Left,
            "right" => KeyboardKey.Right,
            _ => (KeyboardKey)0
        };

        if (key != (KeyboardKey)0) return true;
        string enumName = "KeyboardKey." + name.Trim().ToUpperInvariant();
        return Enum.TryParse(enumName, ignoreCase: true, out key);
    }
}
