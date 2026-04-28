using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Rendering;
using System.Linq;

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
        Settings,
        Gallery,
        Confirm
    }

    private const int SaveSlotCount = 10;
    private readonly VirtualMachine _vm;
    private MenuState _currentState = MenuState.Closed;
    private MenuState _returnState = MenuState.Main;
    private double _openedAt;
    private int _backlogScroll;
    private int _galleryScroll;
    private string _pendingConfirmAction = "";
    private int? _pendingLoadSlot;
    private string? _galleryFullImage;

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
    public void OpenGallery() { _galleryScroll = 0; _galleryFullImage = null; Open(MenuState.Gallery); }
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
            if (_currentState == MenuState.Confirm) Open(_returnState);
            else if (_currentState == MenuState.Main) CloseMenu();
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

        if (_currentState == MenuState.Gallery)
        {
            if (_galleryFullImage != null)
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    _galleryFullImage = null;
                }
                return;
            }
            int wheel = (int)Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                int cols = Math.Max(1, (Raylib.GetScreenWidth() - 120) / 220);
                int rowsVisible = Math.Max(1, (Raylib.GetScreenHeight() - 200) / 180);
                int perPage = cols * rowsVisible;
                int max = Math.Max(0, _vm.State.GalleryEntries.Count - perPage);
                _galleryScroll = Math.Clamp(_galleryScroll - wheel, 0, max);
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
            case MenuState.Gallery:
                UpdateGalleryClick();
                break;
            case MenuState.Confirm:
                UpdateConfirmClick();
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
            case MenuState.Gallery:
                DrawGallery(renderer);
                break;
            case MenuState.Confirm:
                DrawConfirm(renderer);
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
            else
            {
                RequestConfirmation("load_slot", _currentState, i);
                return;
            }
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
                case 4:
                    _vm.State.BgmVolume = SliderValueFromMouse(mouse.X, rows[i], 0, 100);
                    break;
                case 5:
                    _vm.State.SeVolume = SliderValueFromMouse(mouse.X, rows[i], 0, 100);
                    break;
                case 6:
                    _vm.State.AutoModeWaitTimeMs = SliderValueFromMouse(mouse.X, rows[i], 500, 5000);
                    break;
                case 7:
                    _vm.ToggleFullscreen();
                    break;
            }
            _vm.SavePersistentState();
            return;
        }
    }

    private int SliderValueFromMouse(float mouseX, Rectangle row, int min, int max)
    {
        float trackX = row.X + 180;
        float trackW = row.Width - 320;
        float ratio = Math.Clamp((mouseX - trackX) / trackW, 0f, 1f);
        int step = max <= 100 ? 1 : 100;
        int value = min + (int)Math.Round((max - min) * ratio / step) * step;
        return Math.Clamp(value, min, max);
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
        string key = action.TrimStart('*').ToLowerInvariant();

        // スクリプトによる上書きを優先（gosub相当で呼び出し、returnで戻れる）
        if (_vm.State.MenuActionOverrides.TryGetValue(key, out string? overrideLabel) && !string.IsNullOrWhiteSpace(overrideLabel))
        {
            CloseMenu();
            _vm.CallSub(overrideLabel);
            return;
        }

        switch (key)
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
            case "gallery":
                OpenGallery();
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
                RequestConfirmation("reset", _currentState);
                break;
            case "end":
            case "quit":
            case "close":
                RequestConfirmation("end", _currentState);
                break;
            default:
                CloseMenu();
                if (!string.IsNullOrWhiteSpace(action)) _vm.JumpTo("*" + action.TrimStart('*'));
                break;
        }
    }

    private void RequestConfirmation(string action, MenuState returnState, int? loadSlot = null)
    {
        _pendingConfirmAction = action;
        _pendingLoadSlot = loadSlot;
        _returnState = returnState;
        Open(MenuState.Confirm);
    }

    private void ExecuteConfirmedAction()
    {
        string action = _pendingConfirmAction;
        int? loadSlot = _pendingLoadSlot;
        _pendingConfirmAction = "";
        _pendingLoadSlot = null;

        switch (action)
        {
            case "load_slot" when loadSlot.HasValue:
                _vm.LoadGame(loadSlot.Value);
                CloseMenu();
                break;
            case "reset":
                CloseMenu();
                _vm.ResetGame();
                break;
            case "end":
                _vm.QuitGame();
                break;
            default:
                Open(_returnState);
                break;
        }
    }

    private void UpdateConfirmClick()
    {
        var mouse = Raylib.GetMousePosition();
        var (yes, no) = GetConfirmRows();
        if (Raylib.CheckCollisionPointRec(mouse, yes))
        {
            ExecuteConfirmedAction();
            return;
        }

        if (Raylib.CheckCollisionPointRec(mouse, no))
        {
            _pendingConfirmAction = "";
            _pendingLoadSlot = null;
            Open(_returnState);
        }
    }

    private bool CanOpenRightMenu()
    {
        return _vm.State.State is VmState.WaitingForClick or VmState.WaitingForAnimation or VmState.WaitingForButton or VmState.WaitingForDelay;
    }

    private void DrawMainMenu(SpriteRenderer renderer)
    {
        var entries = GetVisibleMainEntries();
        int w = Math.Clamp(_vm.State.RightMenuWidth, 220, Raylib.GetScreenWidth() - 72);
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
        var panel = CenterPanel(Math.Min(_vm.State.SaveLoadWidth, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
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
        var panel = CenterPanel(Math.Min(_vm.State.BacklogWidth, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
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
                DrawText(renderer, (_vm.State.TextHistoryStartNumber + i).ToString("000"), (int)panel.X + 28, y, 14, Gray);
                DrawText(renderer, line, (int)panel.X + 84, y - 2, 18, White);
                y += 34;
            }
        }

        DrawFooter(renderer, panel, "MOUSE WHEEL  SCROLL / ESC  BACK");
    }

    private void DrawSettings(SpriteRenderer renderer)
    {
        var panel = CenterPanel(Math.Min(_vm.State.SettingsWidth, Raylib.GetScreenWidth() - 72), 432);
        DrawPanel(renderer, panel, "SETTINGS");

        var rows = GetSettingsRows();
        var mouse = Raylib.GetMousePosition();
        DrawTextRow(renderer, rows[0], "TEXT SPEED", $"{_vm.State.TextSpeedMs} MS", Raylib.CheckCollisionPointRec(mouse, rows[0]));
        DrawTextRow(renderer, rows[1], "SKIP UNREAD", _vm.State.SkipUnread ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[1]));
        DrawTextRow(renderer, rows[2], "BACKLOG", _vm.State.BacklogEnabled ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[2]));
        DrawTextRow(renderer, rows[3], "CLICK CURSOR", _vm.State.ShowClickCursor ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[3]));
        DrawSliderRow(renderer, rows[4], "BGM VOLUME", _vm.State.BgmVolume, 0, 100, Raylib.CheckCollisionPointRec(mouse, rows[4]));
        DrawSliderRow(renderer, rows[5], "SE VOLUME", _vm.State.SeVolume, 0, 100, Raylib.CheckCollisionPointRec(mouse, rows[5]));
        DrawSliderRow(renderer, rows[6], "AUTO WAIT", _vm.State.AutoModeWaitTimeMs, 500, 5000, Raylib.CheckCollisionPointRec(mouse, rows[6]));
        DrawTextRow(renderer, rows[7], "FULLSCREEN", _vm.Config.Config.IsFullscreen ? "ON" : "OFF", Raylib.CheckCollisionPointRec(mouse, rows[7]));
        DrawFooter(renderer, panel, "CLICK TO CHANGE / ESC  BACK");
    }

    private void UpdateGalleryClick()
    {
        if (_galleryFullImage != null) return;
        var mouse = Raylib.GetMousePosition();
        var entries = _vm.State.GalleryEntries.Values.ToList();
        var rects = GetGalleryItemRects();
        for (int i = 0; i < rects.Count; i++)
        {
            int idx = _galleryScroll + i;
            if (idx >= entries.Count) break;
            if (!Raylib.CheckCollisionPointRec(mouse, rects[i])) continue;
            var entry = entries[idx];
            if (_vm.State.UnlockedCgs.Contains(entry.FlagName))
            {
                _galleryFullImage = entry.ImagePath;
            }
            return;
        }
    }

    private void DrawGallery(SpriteRenderer renderer)
    {
        if (_galleryFullImage != null)
        {
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Color.Black);
            var tex = renderer.GetOrLoadTexture(_galleryFullImage);
            if (tex.Width > 0)
            {
                float scale = Math.Min((float)Raylib.GetScreenWidth() / tex.Width, (float)Raylib.GetScreenHeight() / tex.Height);
                int w = (int)(tex.Width * scale);
                int h = (int)(tex.Height * scale);
                int x = (Raylib.GetScreenWidth() - w) / 2;
                int y = (Raylib.GetScreenHeight() - h) / 2;
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), new Rectangle(x, y, w, h), new System.Numerics.Vector2(0, 0), 0, Color.White);
            }
            DrawCenteredText(renderer, "CLICK / ESC  BACK", 0, Raylib.GetScreenHeight() - 40, Raylib.GetScreenWidth(), 14, Gray);
            return;
        }

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 132));

        var entries = _vm.State.GalleryEntries.Values.ToList();
        var rects = GetGalleryItemRects();
        var mouse = Raylib.GetMousePosition();

        for (int i = 0; i < rects.Count; i++)
        {
            int idx = _galleryScroll + i;
            if (idx >= entries.Count) break;
            var entry = entries[idx];
            bool unlocked = _vm.State.UnlockedCgs.Contains(entry.FlagName);
            bool hover = Raylib.CheckCollisionPointRec(mouse, rects[i]);
            DrawGalleryItem(renderer, rects[i], entry, unlocked, hover);
        }

        if (entries.Count == 0)
        {
            DrawCenteredText(renderer, "NO ENTRIES", 0, Raylib.GetScreenHeight() / 2 - 10, Raylib.GetScreenWidth(), 20, Gray);
        }

        DrawCenteredText(renderer, "MOUSE WHEEL  SCROLL / CLICK  VIEW / ESC  BACK", 0, Raylib.GetScreenHeight() - 40, Raylib.GetScreenWidth(), 14, Gray);
    }

    private List<Rectangle> GetGalleryItemRects()
    {
        var result = new List<Rectangle>();
        int cols = Math.Max(1, (Raylib.GetScreenWidth() - 120) / 220);
        int rows = Math.Max(1, (Raylib.GetScreenHeight() - 200) / 180);
        int startX = (Raylib.GetScreenWidth() - (cols * 220 - 20)) / 2;
        int startY = 80;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result.Add(new Rectangle(startX + c * 220, startY + r * 180, 200, 160));
            }
        }
        return result;
    }

    private void DrawGalleryItem(SpriteRenderer renderer, Rectangle rect, GalleryEntry entry, bool unlocked, bool hover)
    {
        Raylib.DrawRectangleRounded(rect, 0.04f, 12, hover ? new Color(60, 60, 60, 220) : new Color(40, 40, 40, 200));
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.04f, 12, 1, hover ? White : Line);

        if (unlocked)
        {
            var tex = renderer.GetOrLoadTexture(entry.ImagePath);
            if (tex.Width > 0)
            {
                float scale = Math.Min((float)(rect.Width - 20) / tex.Width, (float)(rect.Height - 50) / tex.Height);
                int w = (int)(tex.Width * scale);
                int h = (int)(tex.Height * scale);
                int x = (int)(rect.X + (rect.Width - w) / 2);
                int y = (int)(rect.Y + 10);
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), new Rectangle(x, y, w, h), new System.Numerics.Vector2(0, 0), 0, Color.White);
            }
            DrawCenteredText(renderer, entry.Title, (int)rect.X, (int)(rect.Y + rect.Height - 32), (int)rect.Width, 14, White);
        }
        else
        {
            DrawCenteredText(renderer, "???", (int)rect.X, (int)(rect.Y + rect.Height / 2 - 10), (int)rect.Width, 20, Gray);
        }
    }

    private void DrawConfirm(SpriteRenderer renderer)
    {
        var panel = CenterPanel(Math.Min(420, Raylib.GetScreenWidth() - 72), 190);
        DrawPanel(renderer, panel, "CONFIRM");

        string message = _pendingConfirmAction switch
        {
            "load_slot" => $"LOAD SLOT {(_pendingLoadSlot.GetValueOrDefault() + 1):00}?",
            "reset" => "RESET CURRENT GAME?",
            "end" => "EXIT GAME?",
            _ => "CONTINUE?"
        };
        DrawCenteredText(renderer, message, (int)panel.X, (int)panel.Y + 70, (int)panel.Width, 18, ColorFromHex(_vm.State.MenuTextColor, 255));

        var (yes, no) = GetConfirmRows();
        var mouse = Raylib.GetMousePosition();
        DrawTextRow(renderer, yes, "YES", "CONFIRM", Raylib.CheckCollisionPointRec(mouse, yes));
        DrawTextRow(renderer, no, "NO", "BACK", Raylib.CheckCollisionPointRec(mouse, no));
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
        int w = Math.Clamp(_vm.State.RightMenuWidth, 220, Raylib.GetScreenWidth() - 72);
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
        var panel = CenterPanel(Math.Min(_vm.State.SettingsWidth, Raylib.GetScreenWidth() - 72), 432);
        var rows = new List<Rectangle>();
        for (int i = 0; i < 8; i++)
        {
            rows.Add(new Rectangle(panel.X + 28, panel.Y + 62 + i * 42, panel.Width - 56, 34));
        }
        return rows;
    }

    private Rectangle GetSaveSlotRect(int index)
    {
        var panel = CenterPanel(Math.Min(_vm.State.SaveLoadWidth, Raylib.GetScreenWidth() - 72), Math.Min(560, Raylib.GetScreenHeight() - 64));
        int columns = Math.Clamp(_vm.State.SaveLoadColumns, 1, 4);
        int col = index % columns;
        int row = index / columns;
        float slotW = (panel.Width - 48 - (columns - 1) * 24) / columns;
        float slotH = 92;
        return new Rectangle(panel.X + 24 + col * (slotW + 24), panel.Y + 60 + row * (slotH + 8), slotW, slotH);
    }

    private (Rectangle Yes, Rectangle No) GetConfirmRows()
    {
        var panel = CenterPanel(Math.Min(420, Raylib.GetScreenWidth() - 72), 190);
        var yes = new Rectangle(panel.X + 28, panel.Y + 106, (panel.Width - 68) / 2f, 36);
        var no = new Rectangle(yes.X + yes.Width + 12, yes.Y, yes.Width, yes.Height);
        return (yes, no);
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
            DrawCenteredText(renderer, label, (int)rect.X, (int)rect.Y + 5, (int)rect.Width, 14, hover ? Color.Black : White);
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
        var fill = ColorFromHex(_vm.State.MenuFillColor, _vm.State.MenuFillAlpha);
        var line = ColorFromHex(_vm.State.MenuLineColor, 90);
        float roundness = Math.Clamp(_vm.State.MenuCornerRadius / Math.Max(rect.Height, 1f), 0f, 1f);
        Raylib.DrawRectangleRounded(rect, roundness, 16, fill);
        Raylib.DrawRectangleRoundedLinesEx(rect, roundness, 16, 1, line);
        DrawText(renderer, title, (int)rect.X + 24, (int)rect.Y + 22, 20, ColorFromHex(_vm.State.MenuTextColor, 255));
        Raylib.DrawLine((int)rect.X + 24, (int)rect.Y + 48, (int)(rect.X + rect.Width - 24), (int)rect.Y + 48, line);
    }

    private void DrawTextRow(SpriteRenderer renderer, Rectangle rect, string left, string right, bool hover)
    {
        DrawRect(rect, hover);
        DrawText(renderer, left, (int)rect.X + 14, (int)rect.Y + 8, 17, hover ? Color.Black : ColorFromHex(_vm.State.MenuTextColor, 255));
        int rw = Raylib.MeasureText(right, 13);
        DrawText(renderer, right, (int)(rect.X + rect.Width - rw - 14), (int)rect.Y + 11, 13, hover ? Color.Black : Gray);
    }

    private void DrawSliderRow(SpriteRenderer renderer, Rectangle rect, string label, int value, int min, int max, bool hover)
    {
        DrawRect(rect, hover);
        DrawText(renderer, label, (int)rect.X + 14, (int)rect.Y + 8, 17, hover ? Color.Black : ColorFromHex(_vm.State.MenuTextColor, 255));

        float trackX = rect.X + 180;
        float trackW = rect.Width - 320;
        float trackY = rect.Y + 14;
        float trackH = 6;
        float ratio = (float)(value - min) / (max - min);
        float thumbX = trackX + ratio * trackW;

        // Track background
        Raylib.DrawRectangleRounded(new Rectangle(trackX, trackY, trackW, trackH), 0.5f, 8, new Color(80, 80, 80, 255));
        // Track fill
        Raylib.DrawRectangleRounded(new Rectangle(trackX, trackY, ratio * trackW, trackH), 0.5f, 8, hover ? Color.DarkGray : Line);
        // Thumb
        Raylib.DrawCircle((int)thumbX, (int)(trackY + trackH / 2), 7, hover ? White : Line);

        string valueText = value.ToString();
        int vw = Raylib.MeasureText(valueText, 13);
        DrawText(renderer, valueText, (int)(rect.X + rect.Width - vw - 14), (int)rect.Y + 11, 13, hover ? Color.Black : Gray);
    }

    private static void DrawRect(Rectangle rect, bool hover)
    {
        Raylib.DrawRectangleRounded(rect, 0.06f, 12, hover ? White : Soft);
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.06f, 12, 1, hover ? White : Line);
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

    private static Color ColorFromHex(string hex, int alpha)
    {
        string value = hex.Trim().TrimStart('#');
        if (value.Length == 6 &&
            int.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out int r) &&
            int.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
            int.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
        {
            return new Color(r, g, b, Math.Clamp(alpha, 0, 255));
        }

        return new Color(0, 0, 0, Math.Clamp(alpha, 0, 255));
    }
}


