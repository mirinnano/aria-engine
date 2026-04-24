using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Rendering;

namespace AriaEngine.UI;

public class MenuSystem
{
    private enum MenuState
    {
        Closed,
        Main,
        Save,
        Load,
        Backlog,
        Settings
    }

    private const int SaveSlotCount = 10;
    private readonly VirtualMachine _vm;
    private MenuState _currentState = MenuState.Closed;
    private double _openedAt;
    private int _backlogScroll;

    private static readonly Color Black = new(0, 0, 0, 238);
    private static readonly Color White = new(245, 245, 245, 255);
    private static readonly Color Gray = new(150, 150, 150, 255);
    private static readonly Color Line = new(245, 245, 245, 90);
    private static readonly Color Soft = new(245, 245, 245, 28);

    public bool IsOpen => _currentState != MenuState.Closed;

    public MenuSystem(VirtualMachine vm)
    {
        _vm = vm;
    }

    public void OpenMainMenu() => Open(MenuState.Main);
    public void OpenSaveLoadMenu(bool isSave) => Open(isSave ? MenuState.Save : MenuState.Load);
    public void OpenBacklog() { _backlogScroll = 0; Open(MenuState.Backlog); }
    public void CloseMenu() => _currentState = MenuState.Closed;

    private void Open(MenuState state)
    {
        _currentState = state;
        _openedAt = Raylib.GetTime();
    }

    public void Update()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            if (IsOpen) CloseMenu();
            else if (CanOpenRightMenu()) OpenMainMenu();
            return;
        }

        UpdateSystemButtons();
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_currentState == MenuState.Main) CloseMenu();
            else OpenMainMenu();
            return;
        }

        if (_currentState == MenuState.Backlog)
        {
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                int max = Math.Max(0, _vm.State.TextHistory.Count - 10);
                _backlogScroll = Math.Clamp(_backlogScroll - wheel, 0, max);
            }
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        switch (_currentState)
        {
            case MenuState.Main:
                UpdateMainMenuClick();
                break;
            case MenuState.Save:
            case MenuState.Load:
                UpdateSaveLoadClick();
                break;
            case MenuState.Settings:
                UpdateSettingsClick();
                break;
        }
    }

    public void Draw(SpriteRenderer renderer)
    {
        DrawSystemButtons(renderer);
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 132));

        switch (_currentState)
        {
            case MenuState.Main:
                DrawMainMenu(renderer);
                break;
            case MenuState.Save:
            case MenuState.Load:
                DrawSaveLoadMenu(renderer, _currentState == MenuState.Save);
                break;
            case MenuState.Backlog:
                DrawBacklog(renderer);
                break;
            case MenuState.Settings:
                DrawSettings(renderer);
                break;
        }
    }

    private void UpdateMainMenuClick()
    {
        var mouse = Raylib.GetMousePosition();
        var rows = GetMainMenuRows();
        var entries = GetVisibleMainEntries();
        for (int i = 0; i < rows.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, rows[i]))
            {
                ExecuteAction(entries[i].Action);
                return;
            }
        }
    }

    private void UpdateSaveLoadClick()
    {
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < SaveSlotCount; i++)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, GetSaveSlotRect(i))) continue;
            if (_currentState == MenuState.Save) _vm.SaveGame(i);
            else _vm.LoadGame(i);
            CloseMenu();
            return;
        }
    }

    private void UpdateSettingsClick()
    {
        var mouse = Raylib.GetMousePosition();
        var rows = GetSettingsRows();
        for (int i = 0; i < rows.Count; i++)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, rows[i])) continue;
            switch (i)
            {
                case 0:
                    CycleTextSpeed();
                    break;
                case 1:
                    _vm.State.SkipUnread = !_vm.State.SkipUnread;
                    break;
                case 2:
                    _vm.State.BacklogEnabled = !_vm.State.BacklogEnabled;
                    break;
                case 3:
                    _vm.State.ShowClickCursor = !_vm.State.ShowClickCursor;
                    break;
            }
            _vm.SavePersistentState();
            return;
        }
    }

    private void UpdateSystemButtons()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var mouse = Raylib.GetMousePosition();
        foreach (var (action, rect) in GetSystemButtonRects())
        {
            if (Raylib.CheckCollisionPointRec(mouse, rect))
            {
                ExecuteAction(action);
                return;
            }
        }
    }

    private void ExecuteAction(string action)
    {
        switch (action.TrimStart('*').ToLowerInvariant())
        {
            case "save":
                OpenSaveLoadMenu(true);
                break;
            case "load":
                OpenSaveLoadMenu(false);
                break;
            case "lookback":
            case "backlog":
                OpenBacklog();
                break;
            case "config":
            case "settings":
            case "setting":
                Open(MenuState.Settings);
                break;
            case "skip":
                _vm.ToggleSkip();
                _vm.SavePersistentState();
                CloseMenu();
                break;
            case "reset":
                CloseMenu();
                _vm.ResetGame();
                break;
            case "end":
            case "quit":
            case "close":
                _vm.QuitGame();
                break;
            default:
                CloseMenu();
                if (!string.IsNullOrWhiteSpace(action)) _vm.JumpTo("*" + action.TrimStart('*'));
                break;
        }
    }

    private bool CanOpenRightMenu()
    {
        if (string.IsNullOrWhiteSpace(_vm.State.CurrentTextBuffer)) return false;
        return _vm.State.State is VmState.WaitingForClick or VmState.WaitingForAnimation or VmState.WaitingForButton;
    }

    private void DrawMainMenu(SpriteRenderer renderer)
    {
        var entries = GetVisibleMainEntries();
        int w = 360;
        int h = 78 + entries.Count * 42;
        var panel = CenterPanel(w, h);
        DrawPanel(renderer, panel, "MENU");

        var mouse = Raylib.GetMousePosition();
        var rows = GetMainMenuRows();
        for (int i = 0; i < rows.Count; i++)
        {
            DrawTextRow(renderer, rows[i], entries[i].Label, entries[i].Action.ToUpperInvariant(), Raylib.CheckCollisionPointRec(mouse, rows[i]));
        }
        DrawFooter(renderer, panel, "RIGHT CLICK / ESC  CLOSE");
    }

    private void DrawSaveLoadMenu(SpriteRenderer renderer, bool isSave)
    {
        var panel = CenterPanel(Math.Min(760, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
        DrawPanel(renderer, panel, isSave ? "SAVE" : "LOAD");

        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < SaveSlotCount; i++)
        {
            DrawSaveSlot(renderer, i, GetSaveSlotRect(i), Raylib.CheckCollisionPointRec(mouse, GetSaveSlotRect(i)), isSave);
        }
        DrawFooter(renderer, panel, "CLICK SLOT / ESC  BACK");
    }

    private void DrawBacklog(SpriteRenderer renderer)
    {
        var panel = CenterPanel(Math.Min(860, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
        DrawPanel(renderer, panel, "BACKLOG");

        int visible = Math.Max(1, ((int)panel.Height - 116) / 34);
        int maxStart = Math.Max(0, _vm.State.TextHistory.Count - visible);
        int start = Math.Clamp(maxStart - _backlogScroll, 0, maxStart);
        int y = (int)panel.Y + 70;

        if (_vm.State.TextHistory.Count == 0)
        {
            DrawCenteredText(renderer, "NO LOG", (int)panel.X, (int)panel.Y + (int)panel.Height / 2 - 10, (int)panel.Width, 20, Gray);
        }
        else
        {
            for (int i = start; i < Math.Min(_vm.State.TextHistory.Count, start + visible); i++)
            {
                string line = _vm.State.TextHistory[i].Replace("\r", " ").Replace("\n", " / ");
                int maxChars = Math.Max(20, ((int)panel.Width - 96) / 12);
                if (line.Length > maxChars) line = line[..maxChars] + "...";
                DrawText(renderer, (i + 1).ToString("000"), (int)panel.X + 28, y, 14, Gray);
                DrawText(renderer, line, (int)panel.X + 84, y - 2, 18, White);
                y += 34;
            }
        }

        DrawFooter(renderer, panel, "MOUSE WHEEL  SCROLL / ESC  BACK");
    }

    private void DrawSettings(SpriteRenderer renderer)
    {
        var panel = CenterPanel(520, 306);
        DrawPanel(renderer, panel, "SETTINGS");

        var rows = GetSettingsRows();
        var mouse = Raylib.GetMousePosition();
        DrawTextRow(renderer, rows[0], "TEXT SPEED", $"{_vm.State.TextSpeedMs} MS", Raylib.CheckCollisionPointRec(mouse, rows[0]));
        DrawTextRow(renderer, rows[1], "SKIP UNREAD", _vm.State.SkipUnread ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[1]));
        DrawTextRow(renderer, rows[2], "BACKLOG", _vm.State.BacklogEnabled ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[2]));
        DrawTextRow(renderer, rows[3], "CLICK CURSOR", _vm.State.ShowClickCursor ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[3]));
        DrawFooter(renderer, panel, "CLICK TO CHANGE / ESC  BACK");
    }

    private void DrawSaveSlot(SpriteRenderer renderer, int index, Rectangle rect, bool hover, bool isSave)
    {
        bool hasSave = _vm.Saves.HasSaveData(index);
        var saveData = _vm.Saves.GetSaveData(index);
        DrawRect(rect, hover);

        DrawText(renderer, $"SLOT {(index + 1):00}", (int)rect.X + 18, (int)rect.Y + 14, 18, White);
        string status = hasSave ? "SAVED" : isSave ? "EMPTY" : "NO DATA";
        int sw = Raylib.MeasureText(status, 14);
        DrawText(renderer, status, (int)(rect.X + rect.Width - sw - 18), (int)rect.Y + 18, 14, hasSave ? White : Gray);

        string preview = hasSave && saveData != null && !string.IsNullOrWhiteSpace(saveData.PreviewText)
            ? saveData.PreviewText
            : "----";
        if (preview.Length > 36) preview = preview[..36] + "...";
        DrawText(renderer, preview, (int)rect.X + 18, (int)rect.Y + 44, 16, Gray);

        if (hasSave && saveData != null)
        {
            DrawText(renderer, saveData.SaveTime.ToString("yyyy/MM/dd HH:mm"), (int)rect.X + 18, (int)rect.Y + 70, 14, Gray);
        }
    }

    private List<RightMenuEntry> GetVisibleMainEntries()
    {
        var entries = _vm.State.RightMenuEntries
            .Select(e => new RightMenuEntry { Label = e.Label, Action = e.Action })
            .ToList();
        if (!entries.Any(e => e.Action.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
                              e.Action.Equals("config", StringComparison.OrdinalIgnoreCase)))
        {
            entries.Add(new RightMenuEntry { Label = "SETTINGS", Action = "settings" });
        }
        return entries;
    }

    private List<Rectangle> GetMainMenuRows()
    {
        var entries = GetVisibleMainEntries();
        int w = 360;
        int h = 78 + entries.Count * 42;
        var panel = CenterPanel(w, h);
        var rows = new List<Rectangle>();
        for (int i = 0; i < entries.Count; i++)
        {
            rows.Add(new Rectangle(panel.X + 24, panel.Y + 54 + i * 42, panel.Width - 48, 34));
        }
        return rows;
    }

    private List<Rectangle> GetSettingsRows()
    {
        var panel = CenterPanel(520, 306);
        var rows = new List<Rectangle>();
        for (int i = 0; i < 4; i++)
        {
            rows.Add(new Rectangle(panel.X + 28, panel.Y + 62 + i * 42, panel.Width - 56, 34));
        }
        return rows;
    }

    private Rectangle GetSaveSlotRect(int index)
    {
        var panel = CenterPanel(Math.Min(760, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
        int col = index % 2;
        int row = index / 2;
        float slotW = (panel.Width - 72) / 2f;
        float slotH = 92;
        return new Rectangle(panel.X + 24 + col * (slotW + 24), panel.Y + 60 + row * (slotH + 8), slotW, slotH);
    }

    private List<(string Action, Rectangle Rect)> GetSystemButtonRects()
    {
        var result = new List<(string, Rectangle)>();
        int x = Raylib.GetScreenWidth() - 38;
        int y = 10;
        Add("end", _vm.State.ShowSystemCloseButton);
        Add("reset", _vm.State.ShowSystemResetButton);
        Add("skip", _vm.State.ShowSystemSkipButton);
        Add("save", _vm.State.ShowSystemSaveButton);
        Add("load", _vm.State.ShowSystemLoadButton);
        return result;

        void Add(string action, bool visible)
        {
            if (!visible) return;
            result.Add((action, new Rectangle(x, y, 28, 24)));
            x -= 34;
        }
    }

    private void DrawSystemButtons(SpriteRenderer renderer)
    {
        foreach (var (action, rect) in GetSystemButtonRects())
        {
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rect);
            DrawRect(rect, hover);
            string label = action switch
            {
                "end" => "X",
                "reset" => "R",
                "skip" => _vm.State.SkipMode ? "S*" : "S",
                "save" => "V",
                "load" => "L",
                _ => "?"
            };
            DrawCenteredText(renderer, label, (int)rect.X, (int)rect.Y + 5, (int)rect.Width, 14, hover ? Black : White);
        }
    }

    private void CycleTextSpeed()
    {
        int[] speeds = { 0, 15, 30, 50, 80 };
        int index = Array.IndexOf(speeds, _vm.State.TextSpeedMs);
        _vm.State.TextSpeedMs = speeds[(index + 1 + speeds.Length) % speeds.Length];
    }

    private Rectangle CenterPanel(int width, int height)
    {
        float t = Math.Clamp((float)((Raylib.GetTime() - _openedAt) / 0.16), 0f, 1f);
        t = 1f - MathF.Pow(1f - t, 3f);
        int yOffset = (int)((1f - t) * 10f);
        return new Rectangle((Raylib.GetScreenWidth() - width) / 2, (Raylib.GetScreenHeight() - height) / 2 + yOffset, width, height);
    }

    private void DrawPanel(SpriteRenderer renderer, Rectangle rect, string title)
    {
        Raylib.DrawRectangleRec(rect, Black);
        Raylib.DrawRectangleLinesEx(rect, 1, Line);
        DrawText(renderer, title, (int)rect.X + 24, (int)rect.Y + 22, 20, White);
        Raylib.DrawLine((int)rect.X + 24, (int)rect.Y + 48, (int)(rect.X + rect.Width - 24), (int)rect.Y + 48, Line);
    }

    private void DrawTextRow(SpriteRenderer renderer, Rectangle rect, string left, string right, bool hover)
    {
        DrawRect(rect, hover);
        DrawText(renderer, left, (int)rect.X + 14, (int)rect.Y + 8, 17, hover ? Black : White);
        int rw = Raylib.MeasureText(right, 13);
        DrawText(renderer, right, (int)(rect.X + rect.Width - rw - 14), (int)rect.Y + 11, 13, hover ? Black : Gray);
    }

    private static void DrawRect(Rectangle rect, bool hover)
    {
        Raylib.DrawRectangleRec(rect, hover ? White : Soft);
        Raylib.DrawRectangleLinesEx(rect, 1, hover ? White : Line);
    }

    private void DrawFooter(SpriteRenderer renderer, Rectangle panel, string text)
    {
        DrawText(renderer, text, (int)panel.X + 24, (int)(panel.Y + panel.Height - 28), 12, Gray);
    }

    private void DrawCenteredText(SpriteRenderer renderer, string text, int x, int y, int width, int size, Color color)
    {
        int tw = Raylib.MeasureText(text, size);
        DrawText(renderer, text, x + (width - tw) / 2, y, size, color);
    }

    private static void DrawText(SpriteRenderer renderer, string text, int x, int y, int size, Color color)
    {
        renderer.DrawUiText(text, x, y, size, color);
    }
}


