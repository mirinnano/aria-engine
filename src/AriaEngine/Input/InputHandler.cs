using Raylib_cs;
using AriaEngine.Core;

namespace AriaEngine.Input;

public class InputHandler
{
    public void Update(VirtualMachine vm)
    {
        if (!vm.State.ProductionMode && Raylib.IsKeyPressed(KeyboardKey.F3)) vm.State.DebugMode = !vm.State.DebugMode;
        if (!vm.State.ProductionMode && Raylib.IsKeyPressed(KeyboardKey.F5))
        {
            vm.SaveGame(0);
            return;
        }
        if (!vm.State.ProductionMode && Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            vm.Menu.CloseMenu();
            vm.LoadGame(0);
            return;
        }
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
                // テキストがまだ表示中の場合は、テキストを即座に完了させるだけ
                if (vm.State.DisplayedTextLength < vm.State.CurrentTextBuffer.Length)
                {
                    vm.State.DisplayedTextLength = vm.State.CurrentTextBuffer.Length;
                }
                else
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
            bool keyboardActivate = IsFocusedButtonActivatePressed();
            if (IsFocusNextPressed()) MoveButtonFocus(vm.State, 1);
            if (IsFocusPreviousPressed()) MoveButtonFocus(vm.State, -1);

            Sprite? clickedButton = null;
            int clickedZ = int.MinValue;
            bool mouseHoverFound = false;

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
                bool mouseHovered = Raylib.CheckCollisionPointRec(mousePoint, rect);
                if (mouseHovered)
                {
                    vm.State.FocusedButtonId = btn.Id;
                    mouseHoverFound = true;
                }

                btn.IsHovered = mouseHovered || btn.Id == vm.State.FocusedButtonId;
                if (mouseHovered)
                {
                    int resultValue = vm.State.SpriteButtonMap.TryGetValue(btn.Id, out int mappedValue) ? mappedValue : btn.Id;
                    if (TryDispatchHoverEvent(vm, btn.Id, resultValue)) return;
                }
                else
                {
                    vm.State.UiHoverActive.Remove(btn.Id);
                }

                if (clicked && mouseHovered)
                {
                    if (btn.Z >= clickedZ)
                    {
                        clickedButton = btn;
                        clickedZ = btn.Z;
                    }
                }
            }

            if (!mouseHoverFound && !IsFocusedButtonValid(vm.State))
            {
                MoveButtonFocus(vm.State, 1);
            }

            if (keyboardActivate &&
                vm.State.FocusedButtonId >= 0 &&
                vm.State.Sprites.TryGetValue(vm.State.FocusedButtonId, out var focusedButton) &&
                focusedButton.Visible &&
                focusedButton.IsButton)
            {
                clickedButton = focusedButton;
            }

            if (clickedButton != null)
            {
                int resultValue = vm.State.SpriteButtonMap.TryGetValue(clickedButton.Id, out int mappedValue) ? mappedValue : clickedButton.Id;
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

    public static int MoveButtonFocus(GameState state, int direction)
    {
        var buttons = GetFocusableButtons(state);
        if (buttons.Count == 0)
        {
            state.FocusedButtonId = -1;
            foreach (var sprite in state.Sprites.Values) sprite.IsHovered = false;
            return -1;
        }

        int current = buttons.FindIndex(sprite => sprite.Id == state.FocusedButtonId);
        int step = direction < 0 ? -1 : 1;
        int next = current < 0
            ? (step > 0 ? 0 : buttons.Count - 1)
            : (current + step + buttons.Count) % buttons.Count;

        state.FocusedButtonId = buttons[next].Id;
        foreach (var sprite in state.Sprites.Values)
        {
            if (sprite.IsButton) sprite.IsHovered = sprite.Id == state.FocusedButtonId;
        }

        return state.FocusedButtonId;
    }

    private static List<Sprite> GetFocusableButtons(GameState state)
    {
        return state.Sprites.Values
            .Where(sprite => sprite.Visible && sprite.IsButton)
            .OrderBy(sprite => sprite.Y)
            .ThenBy(sprite => sprite.X)
            .ThenBy(sprite => sprite.Z)
            .ThenBy(sprite => sprite.Id)
            .ToList();
    }

    private static bool IsFocusedButtonValid(GameState state)
    {
        return state.FocusedButtonId >= 0 &&
               state.Sprites.TryGetValue(state.FocusedButtonId, out var sprite) &&
               sprite.Visible &&
               sprite.IsButton;
    }

    private static bool IsFocusNextPressed()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Down) ||
               Raylib.IsKeyPressed(KeyboardKey.Right) ||
               (Raylib.IsKeyPressed(KeyboardKey.Tab) && !Raylib.IsKeyDown(KeyboardKey.LeftShift) && !Raylib.IsKeyDown(KeyboardKey.RightShift));
    }

    private static bool IsFocusPreviousPressed()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Up) ||
               Raylib.IsKeyPressed(KeyboardKey.Left) ||
               (Raylib.IsKeyPressed(KeyboardKey.Tab) && (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift)));
    }

    private static bool IsFocusedButtonActivatePressed()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Enter) ||
               Raylib.IsKeyPressed(KeyboardKey.KpEnter) ||
               Raylib.IsKeyPressed(KeyboardKey.Space);
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
