using Raylib_cs;
using AriaEngine.Core;
using System.Linq;

namespace AriaEngine.Input;

public class InputHandler
{
    public void Update(VirtualMachine vm)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) vm.State.DebugMode = !vm.State.DebugMode;

        if (vm.State.State == VmState.WaitingForClick)
        {
            if (vm.State.SkipMode && (vm.State.SkipUnread || vm.State.CurrentInstructionWasRead))
            {
                if (vm.State.IsWaitingPageClear)
                {
                    vm.State.CurrentTextBuffer = "";
                    vm.State.DisplayedTextLength = 0;
                    vm.State.IsWaitingPageClear = false;
                }
                vm.ResumeFromClick();
                return;
            }

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) || 
                Raylib.IsKeyPressed(KeyboardKey.Enter) || 
                Raylib.IsKeyPressed(KeyboardKey.Space))
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
            // If it's ticking text, click means skip remainder
            if (vm.State.TextSpeedMs > 0 && vm.State.DisplayedTextLength < vm.State.CurrentTextBuffer.Length)
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    vm.State.DisplayedTextLength = vm.State.CurrentTextBuffer.Length;
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
            var buttons = vm.State.Sprites.Values
                .Where(s => s.Visible && s.IsButton)
                .OrderByDescending(s => s.Z);

            bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

            foreach (var btn in buttons)
            {
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

                if (clicked && btn.IsHovered)
                {
                    vm.ResumeFromButton(btn.Id);
                    return; // Return immediately after handling the click
                }
            }
        }
    }
}
