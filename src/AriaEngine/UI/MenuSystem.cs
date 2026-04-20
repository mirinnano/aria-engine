using Raylib_cs;
using AriaEngine.Core;
using System.Collections.Generic;
using System.Numerics;

namespace AriaEngine.UI;

/// <summary>
/// メニューシステム
/// 右クリックメニュー、設定画面、セーブ/ロードUIを管理します。
/// </summary>
public class MenuSystem
{
    private enum MenuState
    {
        Closed,
        Main,
        SaveLoad,
        Settings,
        Options
    }

    private MenuState _currentState = MenuState.Closed;
    private readonly VirtualMachine _vm;
    private int _selectedSlot = 0;
    private const int SaveSlotCount = 10;

    // メニュー項目
    private readonly List<MenuItem> _mainMenuItems = new();
    private readonly List<MenuItem> _settingsItems = new();

    public bool IsOpen => _currentState != MenuState.Closed;

    public MenuSystem(VirtualMachine vm)
    {
        _vm = vm;

        InitializeMainMenu();
        InitializeSettings();
    }

    private void InitializeMainMenu()
    {
        _mainMenuItems.Clear();
        _mainMenuItems.Add(new MenuItem("Return", () => CloseMenu()));
        _mainMenuItems.Add(new MenuItem("Save", () => OpenSaveLoadMenu(true)));
        _mainMenuItems.Add(new MenuItem("Load", () => OpenSaveLoadMenu(false)));
        _mainMenuItems.Add(new MenuItem("Settings", () => OpenSettingsMenu()));
        _mainMenuItems.Add(new MenuItem("Title", () => _vm.CallSub("title")));
        _mainMenuItems.Add(new MenuItem("Quit", () => _vm.QuitGame()));
    }

    private void InitializeSettings()
    {
        _settingsItems.Clear();
        _settingsItems.Add(new MenuItem($"Text Speed: {_vm.State.TextSpeedMs}ms", ToggleTextSpeed));
        _settingsItems.Add(new MenuItem($"Auto Mode: {(_vm.State.AutoMode ? "On" : "Off")}", ToggleAutoMode));
        _settingsItems.Add(new MenuItem($"Skip Unread: {(_vm.State.SkipUnread ? "On" : "Off")}", ToggleSkipUnread));
        _settingsItems.Add(new MenuItem("Return", () => OpenMainMenu()));
    }

    public void Update()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            if (IsOpen)
            {
                CloseMenu();
            }
            else if (_vm.State.State == VmState.WaitingForClick ||
                     _vm.State.State == VmState.WaitingForAnimation)
            {
                OpenMainMenu();
            }
            return;
        }

        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_currentState == MenuState.SaveLoad)
            {
                OpenMainMenu();
            }
            else
            {
                CloseMenu();
            }
            return;
        }

        switch (_currentState)
        {
            case MenuState.Main:
                UpdateMainMenu();
                break;
            case MenuState.SaveLoad:
                UpdateSaveLoadMenu();
                break;
            case MenuState.Settings:
                UpdateSettingsMenu();
                break;
        }
    }

    private void UpdateMainMenu()
    {
        // キーボード操作は一旦省略（基本マウス操作とする）
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = Raylib.GetMousePosition();
            for (int i = 0; i < 5; i++)
            {
                int btnId = 10001 + i * 2;
                if (_vm.State.Sprites.TryGetValue(btnId, out var btn) && btn.IsHovered)
                {
                    ExecuteMenuAction(i);
                    break;
                }
            }
        }
    }

    private void ExecuteMenuAction(int index)
    {
        switch (index)
        {
            case 0: // Save
                _vm.SaveGame(0);
                CloseMenu();
                break;
            case 1: // Load
                _vm.LoadGame(0);
                CloseMenu();
                break;
            case 2: // Settings
                break;
            case 3: // Title
                CloseMenu();
                _vm.JumpTo("*title_start");
                break;
            case 4: // Close
                CloseMenu();
                break;
        }
    }

    private void UpdateSaveLoadMenu()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Left))
        {
            _selectedSlot = (_selectedSlot - 1 + SaveSlotCount) % SaveSlotCount;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Right))
        {
            _selectedSlot = (_selectedSlot + 1) % SaveSlotCount;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            if (_vm.State.SaveMode)
            {
                _vm.SaveGame(_selectedSlot);
            }
            else
            {
                _vm.LoadGame(_selectedSlot);
            }
        }
    }

    private void UpdateSettingsMenu()
    {
        InitializeSettings(); // 設定値を更新
    }

    public void Draw()
    {
        if (!IsOpen) return;

        switch (_currentState)
        {
            case MenuState.Main:
                DrawMainMenu();
                break;
            case MenuState.SaveLoad:
                DrawSaveLoadMenu();
                break;
            case MenuState.Settings:
                DrawSettingsMenu();
                break;
        }
    }

    private void DrawMainMenu()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        // 背景を描画（半透明黒）
        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 180));

        // メニューボックス
        int boxWidth = 300;
        int boxHeight = _mainMenuItems.Count * 50 + 40;
        int boxX = (screenWidth - boxWidth) / 2;
        int boxY = (screenHeight - boxHeight) / 2;

        Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, new Color(40, 40, 60, 230));
        Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, new Color(200, 200, 255, 255));

        // メニュータイトル
        var title = "Menu";
        int titleWidth = Raylib.MeasureText(title, 24);
        Raylib.DrawText(title, (screenWidth - titleWidth) / 2, boxY - 30, 24, Color.White);

        // メニュー項目を描画
        int startY = boxY + 30;
        for (int i = 0; i < _mainMenuItems.Count; i++)
        {
            var item = _mainMenuItems[i];
            int itemY = startY + i * 50;
            var itemColor = Color.White;

            // マウスホバー効果
            var mousePos = Raylib.GetMousePosition();
            if (mousePos.X >= boxX && mousePos.X <= boxX + boxWidth &&
                mousePos.Y >= itemY && mousePos.Y <= itemY + 40)
            {
                itemColor = new Color(255, 200, 100, 255);
                Raylib.DrawRectangle(boxX + 10, itemY, boxWidth - 20, 40, new Color(255, 255, 255, 30));
            }

            Raylib.DrawText(item.Label, boxX + 20, itemY + 10, 20, itemColor);
        }
    }

    private void DrawSaveLoadMenu()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        // 背景を描画
        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 180));

        // セーブ/ロードボックス
        int boxWidth = 600;
        int boxHeight = 400;
        int boxX = (screenWidth - boxWidth) / 2;
        int boxY = (screenHeight - boxHeight) / 2;

        Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, new Color(40, 40, 60, 230));
        Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, new Color(200, 200, 255, 255));

        // タイトル
        var title = _vm.State.SaveMode ? "Save Game" : "Load Game";
        int titleWidth = Raylib.MeasureText(title, 24);
        Raylib.DrawText(title, (screenWidth - titleWidth) / 2, boxY - 30, 24, Color.White);

        // セーブスロットを描画
        int slotWidth = (boxWidth - 80) / SaveSlotCount;
        for (int i = 0; i < SaveSlotCount; i++)
        {
            int slotX = boxX + 40 + i * slotWidth;
            int slotY = boxY + 50;

            // スロット背景
            var slotColor = (i == _selectedSlot) ?
                new Color(255, 200, 100, 230) :
                new Color(80, 80, 100, 180);

            Raylib.DrawRectangle(slotX, slotY, slotWidth - 10, 300, slotColor);

            // スロット番号
            Raylib.DrawText($"Slot {i + 1}", slotX + 10, slotY + 10, 18, Color.White);

            // セーブ情報（簡易版）
            var saveInfo = _vm.State.SaveMode ? "Empty" : $"Save {i + 1}";
            Raylib.DrawText(saveInfo, slotX + 10, slotY + 40, 14, new Color(200, 200, 200, 255));

            // 選択インジケーター
            if (i == _selectedSlot)
            {
                Raylib.DrawRectangleLines(slotX, slotY, slotWidth - 10, 300, Color.White);
            }
        }

        // 操作説明
        Raylib.DrawText("← → : Select Slot   ENTER : Confirm   ESC : Back",
            boxX + 40, boxY + 370, 16, new Color(180, 180, 180, 255));
    }

    private void DrawSettingsMenu()
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        // 背景を描画
        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 180));

        // 設定ボックス
        int boxWidth = 400;
        int boxHeight = _settingsItems.Count * 60 + 60;
        int boxX = (screenWidth - boxWidth) / 2;
        int boxY = (screenHeight - boxHeight) / 2;

        Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, new Color(40, 40, 60, 230));
        Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, new Color(200, 200, 255, 255));

        // タイトル
        var title = "Settings";
        int titleWidth = Raylib.MeasureText(title, 24);
        Raylib.DrawText(title, (screenWidth - titleWidth) / 2, boxY - 30, 24, Color.White);

        // 設定項目を描画
        int startY = boxY + 30;
        for (int i = 0; i < _settingsItems.Count; i++)
        {
            var item = _settingsItems[i];
            int itemY = startY + i * 60;
            var itemColor = Color.White;

            // マウスホバー効果
            var mousePos = Raylib.GetMousePosition();
            if (mousePos.X >= boxX && mousePos.X <= boxX + boxWidth &&
                mousePos.Y >= itemY && mousePos.Y <= itemY + 50)
            {
                itemColor = new Color(255, 200, 100, 255);
                Raylib.DrawRectangle(boxX + 10, itemY, boxWidth - 20, 50, new Color(255, 255, 255, 30));
            }

            Raylib.DrawText(item.Label, boxX + 20, itemY + 15, 20, itemColor);
        }
    }

    private void OpenMainMenu()
    {
        _currentState = MenuState.Main;
    }

    private void OpenSaveLoadMenu(bool isSave)
    {
        _vm.State.SaveMode = isSave;
        _currentState = MenuState.SaveLoad;
        _selectedSlot = 0;
    }

    private void OpenSettingsMenu()
    {
        _currentState = MenuState.Settings;
    }

    private void CloseMenu()
    {
        _currentState = MenuState.Closed;
    }

    // 設定トグルメソッド
    private void ToggleTextSpeed()
    {
        int[] speeds = { 0, 10, 20, 30, 50, 80, 120 };
        int currentIndex = Array.IndexOf(speeds, _vm.State.TextSpeedMs);
        currentIndex = (currentIndex + 1) % speeds.Length;
        _vm.State.TextSpeedMs = speeds[currentIndex];
    }

    private void ToggleAutoMode()
    {
        _vm.State.AutoMode = !_vm.State.AutoMode;
    }

    private void ToggleSkipUnread()
    {
        _vm.State.SkipUnread = !_vm.State.SkipUnread;
    }
}

/// <summary>
/// メニュー項目
/// </summary>
public class MenuItem
{
    public string Label { get; set; } = string.Empty;
    public Action Action { get; set; } = () => { };

    public MenuItem(string label, Action action)
    {
        Label = label;
        Action = action;
    }
}
