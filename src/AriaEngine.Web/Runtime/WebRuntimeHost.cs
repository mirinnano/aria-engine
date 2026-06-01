#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Scripting;
using AriaEngine.Web.Rendering;
using AriaEngine.Web.Storage;

namespace AriaEngine.Web.Runtime;

public sealed record WebRuntimeOptions(string RuntimeDataRoot = "");

public sealed record WebRuntimeFrame(
    VmState ExecutionState,
    int LogicalWidth,
    int LogicalHeight,
    BrowserFontFace Font,
    IReadOnlyList<BrowserDrawCommand> DrawCommands)
{
    public int ProgramCounter { get; init; }
    public string CurrentText { get; init; } = "";
}

public sealed class WebRuntimeHost
{
    private const int MaxStepBatches = 256;
    private const int WebMenuBackdropSpriteId = -9100;
    private const int WebMenuPanelSpriteId = -9101;
    private const int WebMenuRowBaseSpriteId = -9200;
    private const int WebMenuTextBaseSpriteId = -9300;
    private const int WebClickCursorSpriteId = -9400;
    private const double WebMenuStartX = 48d;
    private const double WebMenuStartY = 48d;
    private const double WebMenuRowWidth = 260d;
    private const double WebMenuRowHeight = 44d;
    private const double WebMenuLineHeight = 60d;
    private const double WebMenuDangerGap = 48d;

    private readonly IAssetProvider _provider;
    private readonly ScriptLoader _loader;
    private readonly VirtualMachine _vm;
    private readonly string _runtimeRoot;
    private readonly List<BrowserStorageOperation> _pendingStorageOperations = new();
    private bool _webMenuOpen;

    private WebRuntimeHost(IAssetProvider provider, ScriptLoader loader, VirtualMachine vm, string runtimeRoot)
    {
        _provider = provider;
        _loader = loader;
        _vm = vm;
        _runtimeRoot = runtimeRoot;
    }

    public static WebRuntimeHost Boot(IAssetProvider provider, WebRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        string runtimeRoot = string.IsNullOrWhiteSpace(options.RuntimeDataRoot)
            ? Path.Combine(Path.GetTempPath(), "aria-web-runtime")
            : options.RuntimeDataRoot;
        Directory.CreateDirectory(runtimeRoot);

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var loader = new ScriptLoader(parser, provider, RunMode.Dev);
        var config = new ConfigManager(
            reporter,
            Path.Combine(runtimeRoot, "config.json"),
            Path.Combine(runtimeRoot, "save", "persistent.ariasav"),
            usePortableJsonPersistent: true);
        var saves = new SaveManager(reporter, Path.Combine(runtimeRoot, "saves"), usePortableJsonSaves: true);
        var vm = new VirtualMachine(reporter, new TweenManager(), saves, config, provider, runtimeRoot);

        if (provider.Exists("assets/i18n/locales.json"))
        {
            vm.Localization = LocalizationManager.Load(provider, "assets/i18n/locales.json");
            vm.SyncLocalizationRuntimeState();
        }

        var host = new WebRuntimeHost(provider, loader, vm, runtimeRoot);
        host.LoadInitAndMain();
        return host;
    }

    public WebRuntimeFrame CreateFrame(double cssWidth, double cssHeight)
    {
        RunUntilInteractive();

        var mapper = CanvasScaleMapper.Create(cssWidth, cssHeight);
        var renderer = new BrowserRenderer(mapper);
        BrowserFontFace font = BrowserFontLoader.Resolve(_vm.Localization, _vm.State.EngineSettings.FontPath);
        var drawCommands = renderer.ToDrawCommands(CollectRenderableSprites()).ToList();
        AddClickCursorCommand(drawCommands, mapper);

        return new WebRuntimeFrame(
            _vm.State.Execution.State,
            _vm.State.EngineSettings.WindowWidth,
            _vm.State.EngineSettings.WindowHeight,
            font,
            drawCommands)
        {
            ProgramCounter = _vm.State.Execution.ProgramCounter,
            CurrentText = _vm.State.TextRuntime.CurrentTextBuffer
        };
    }

    public bool HandlePointerPress(double cssX, double cssY, double cssWidth, double cssHeight)
    {
        var mapper = CanvasScaleMapper.Create(cssWidth, cssHeight);
        LogicalPoint logical = mapper.MapCssToLogical(cssX, cssY);

        if (_webMenuOpen)
        {
            return HandleWebMenuPress(logical);
        }

        if (_vm.State.Execution.State == VmState.WaitingForClick)
        {
            _vm.ResumeFromClick();
            RunUntilInteractive();
            return true;
        }

        if (_vm.State.Execution.State != VmState.WaitingForButton)
        {
            return false;
        }

        int? buttonId = FindButtonAt(logical);
        if (buttonId is null)
        {
            return false;
        }

        _vm.ResumeFromButton(buttonId.Value);
        RunUntilInteractive();
        return true;
    }

    public bool HandleContextMenu(double cssX, double cssY, double cssWidth, double cssHeight)
    {
        var mapper = CanvasScaleMapper.Create(cssWidth, cssHeight);
        LogicalPoint logical = mapper.MapCssToLogical(cssX, cssY);
        OpenWebMenu(logical);
        return true;
    }

    public IReadOnlyList<BrowserStorageOperation> DrainStorageOperations()
    {
        var operations = _pendingStorageOperations.ToList();
        _pendingStorageOperations.Clear();
        return operations;
    }

    public bool ApplyLoadedStorage(BrowserStorageOperation operation, string? payload)
    {
        if (operation.Kind != BrowserStorageOperationKind.Read ||
            operation.Area != BrowserStorageArea.IndexedDb ||
            !operation.StoreName.Equals("saves", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        int? slot = ParseSaveSlotKey(operation.Key);
        if (slot is null) return false;

        Directory.CreateDirectory(GetSaveDirectory());
        File.WriteAllText(GetSavePath(slot.Value), payload);
        _vm.LoadGame(slot.Value);
        _webMenuOpen = false;
        RunUntilInteractive();
        return true;
    }

    private void LoadInitAndMain()
    {
        ParseResult init = _loader.LoadScript("init.aria");
        _vm.LoadScript(init, "init.aria");
        RunUntilStopped();

        _vm.SetIncludeResolver(path => _provider.Exists(path) ? _loader.LoadScript(path) : null);
        string mainScript = _vm.State.EngineSettings.MainScript;
        ParseResult main = _loader.LoadScript(mainScript);
        _vm.LoadScript(main, mainScript);
        RunUntilInteractive();
    }

    private void RunUntilInteractive()
    {
        RunUntilStopped();
        if (IsInteractive(_vm.State.Execution.State) && _vm.Tweens.IsAnimating)
        {
            _vm.Tweens.FinishAll(_vm.State);
            _vm.Update(0f);
        }
    }

    private void RunUntilStopped()
    {
        for (int i = 0; i < MaxStepBatches && CanAutoAdvance(_vm.State.Execution.State); i++)
        {
            if (_vm.State.Execution.State == VmState.Running)
            {
                _vm.Step();
            }
            else
            {
                if (_vm.State.Execution.State is VmState.FadingIn or VmState.FadingOut)
                {
                    _vm.FinishFade();
                }
                else
                {
                    _vm.Tweens.Update(_vm.State, 1000f);
                    _vm.Update(1000f);
                }
            }
        }

        if (CanAutoAdvance(_vm.State.Execution.State))
        {
            throw new InvalidOperationException("Web runtime did not reach an interactive state.");
        }
    }

    private static bool CanAutoAdvance(VmState state) =>
        state is VmState.Running
            or VmState.WaitingForDelay
            or VmState.WaitingForAnimation
            or VmState.WaitingForTimer
            or VmState.FadingIn
            or VmState.FadingOut;

    private static bool IsInteractive(VmState state) =>
        state is VmState.WaitingForButton or VmState.WaitingForClick;

    private int? FindButtonAt(LogicalPoint logical)
    {
        return _vm.State.Interaction.SpriteButtonMap.Keys
            .Select(id => _vm.State.Render.Sprites.TryGetValue(id, out Sprite? sprite) ? sprite : null)
            .Where(sprite => sprite is { Visible: true })
            .OrderByDescending(sprite => sprite!.Z)
            .Select(sprite => sprite!)
            .FirstOrDefault(sprite => Contains(sprite, logical))?.Id;
    }

    private static bool Contains(Sprite sprite, LogicalPoint point)
    {
        double x = sprite.ClickAreaW > 0 ? sprite.ClickAreaX : sprite.X;
        double y = sprite.ClickAreaH > 0 ? sprite.ClickAreaY : sprite.Y;
        double width = sprite.ClickAreaW > 0 ? sprite.ClickAreaW : sprite.Width * sprite.ScaleX;
        double height = sprite.ClickAreaH > 0 ? sprite.ClickAreaH : sprite.Height * sprite.ScaleY;

        return width > 0 &&
               height > 0 &&
               point.X >= x &&
               point.X <= x + width &&
               point.Y >= y &&
               point.Y <= y + height;
    }

    private IReadOnlyList<Sprite> CollectRenderableSprites()
    {
        var sprites = _vm.State.Render.Sprites.Values.ToList();
        if (ShouldRenderCompatTextWindow())
        {
            AddCompatTextWindowSprites(sprites);
        }

        if (_webMenuOpen)
        {
            AddWebMenuSprites(sprites);
        }

        return sprites;
    }

    private void AddCompatTextWindowSprites(List<Sprite> sprites)
    {
        TextWindowState textWindow = _vm.State.TextWindow;
        string text = _vm.State.TextRuntime.CurrentTextBuffer;
        sprites.Add(new Sprite
        {
            Id = -9000,
            Type = SpriteType.Rect,
            X = textWindow.DefaultTextboxX,
            Y = textWindow.DefaultTextboxY,
            Width = textWindow.DefaultTextboxW,
            Height = textWindow.DefaultTextboxH,
            FillColor = textWindow.DefaultTextboxBgColor,
            FillAlpha = textWindow.DefaultTextboxBgAlpha,
            CornerRadius = textWindow.DefaultTextboxCornerRadius,
            BorderColor = textWindow.DefaultTextboxBorderColor,
            BorderWidth = textWindow.DefaultTextboxBorderWidth,
            BorderOpacity = textWindow.DefaultTextboxBorderOpacity,
            ShadowColor = textWindow.DefaultTextboxShadowColor,
            ShadowOffsetX = textWindow.DefaultTextboxShadowOffsetX,
            ShadowOffsetY = textWindow.DefaultTextboxShadowOffsetY,
            ShadowAlpha = textWindow.DefaultTextboxShadowAlpha,
            Z = 9000
        });
        sprites.Add(new Sprite
        {
            Id = -8999,
            Type = SpriteType.Text,
            Text = text,
            X = textWindow.DefaultTextboxX + textWindow.DefaultTextboxPaddingX,
            Y = textWindow.DefaultTextboxY + textWindow.DefaultTextboxPaddingY,
            Width = Math.Max(0, textWindow.DefaultTextboxW - textWindow.DefaultTextboxPaddingX * 2),
            Height = Math.Max(0, textWindow.DefaultTextboxH - textWindow.DefaultTextboxPaddingY * 2),
            FontSize = textWindow.DefaultFontSize,
            Color = textWindow.DefaultTextColor,
            Z = 9001
        });
    }

    private void AddWebMenuSprites(List<Sprite> sprites)
    {
        IReadOnlyList<RightMenuEntry> entries = GetWebMenuEntries();

        sprites.Add(new Sprite
        {
            Id = WebMenuBackdropSpriteId,
            Type = SpriteType.Rect,
            X = 0,
            Y = 0,
            Width = _vm.State.EngineSettings.WindowWidth,
            Height = _vm.State.EngineSettings.WindowHeight,
            FillColor = "#06070a",
            FillAlpha = 246,
            Z = 9800
        });

        double currentY = WebMenuStartY;
        for (int i = 0; i < entries.Count; i++)
        {
            RightMenuEntry entry = entries[i];
            if (IsDangerMenuAction(entry.Action) && i > 0 && !IsDangerMenuAction(entries[i - 1].Action))
            {
                currentY += WebMenuDangerGap;
            }

            bool focused = i == 0;
            string icon = string.IsNullOrEmpty(entry.Icon) ? ResolveMenuIcon(entry.Action) : entry.Icon;
            if (!string.IsNullOrEmpty(icon))
            {
                sprites.Add(new Sprite
                {
                    Id = WebMenuRowBaseSpriteId - i,
                    Type = SpriteType.Text,
                    Text = icon,
                    X = (float)WebMenuStartX,
                    Y = (float)(currentY + 6),
                    Width = 28,
                    Height = ToSpriteInt(WebMenuRowHeight),
                    FontSize = 16,
                    Color = focused ? "#ffffff" : "#78787d",
                    Z = 9802 + i * 3
                });
            }

            sprites.Add(new Sprite
            {
                Id = WebMenuTextBaseSpriteId - i,
                Type = SpriteType.Text,
                Text = entry.Label.ToUpperInvariant(),
                X = (float)(WebMenuStartX + (string.IsNullOrEmpty(icon) ? 0 : 20)),
                Y = (float)(currentY + 4),
                Width = ToSpriteInt(WebMenuRowWidth - 20),
                Height = ToSpriteInt(WebMenuRowHeight),
                FontSize = 26,
                Color = focused ? "#ffffff" : "#a0a0a5",
                TextAlign = TextAlignment.Left,
                TextVAlign = TextVerticalAlignment.Top,
                Z = 9803 + i * 3
            });

            if (focused)
            {
                sprites.Add(new Sprite
                {
                    Id = WebMenuTextBaseSpriteId - 100 - i,
                    Type = SpriteType.Rect,
                    X = (float)(WebMenuStartX + (string.IsNullOrEmpty(icon) ? 0 : 20)),
                    Y = (float)(currentY + WebMenuRowHeight - 4),
                    Width = 76,
                    Height = 1,
                    FillColor = "#ffffff",
                    FillAlpha = 178,
                    Z = 9804 + i * 3
                });
            }

            currentY += WebMenuLineHeight;
        }
    }

    private bool HandleWebMenuPress(LogicalPoint logical)
    {
        int? index = FindWebMenuEntryIndex(logical);
        if (index is null)
        {
            _webMenuOpen = false;
            return true;
        }

        IReadOnlyList<RightMenuEntry> entries = GetWebMenuEntries();
        string action = ResolveMenuAction(entries[index.Value].Action);
        _webMenuOpen = false;

        if (action.Equals("save", StringComparison.OrdinalIgnoreCase))
        {
            QueueWebSave(slot: 0);
            return true;
        }

        if (action.Equals("load", StringComparison.OrdinalIgnoreCase))
        {
            _pendingStorageOperations.Add(IndexedDbSaveStore.ReadSave(0));
            return true;
        }

        return true;
    }

    private void OpenWebMenu(LogicalPoint logical)
    {
        _webMenuOpen = true;
    }

    private int? FindWebMenuEntryIndex(LogicalPoint logical)
    {
        IReadOnlyList<RightMenuEntry> entries = GetWebMenuEntries();
        double currentY = WebMenuStartY;
        for (int i = 0; i < entries.Count; i++)
        {
            if (IsDangerMenuAction(entries[i].Action) && i > 0 && !IsDangerMenuAction(entries[i - 1].Action))
            {
                currentY += WebMenuDangerGap;
            }

            if (logical.X >= WebMenuStartX &&
                logical.X <= WebMenuStartX + WebMenuRowWidth &&
                logical.Y >= currentY &&
                logical.Y <= currentY + WebMenuRowHeight)
            {
                return i;
            }

            currentY += WebMenuLineHeight;
        }

        return null;
    }

    private IReadOnlyList<RightMenuEntry> GetWebMenuEntries()
    {
        IEnumerable<RightMenuEntry> source = _vm.State.MenuRuntime.RightMenuEntries.Count > 0
            ? _vm.State.MenuRuntime.RightMenuEntries
            : new[] { new RightMenuEntry { Label = "SAVE", Action = "save" }, new RightMenuEntry { Label = "LOAD", Action = "load" } };

        var entries = source
            .Select(entry => new RightMenuEntry
            {
                Label = LocalizeMenuLabel(entry),
                Action = entry.Action,
                Icon = ResolveMenuIcon(entry.Action)
            })
            .ToList();

        if (!entries.Any(entry =>
                entry.Action.Equals("settings", StringComparison.OrdinalIgnoreCase) ||
                entry.Action.Equals("config", StringComparison.OrdinalIgnoreCase)))
        {
            var settings = new RightMenuEntry { Label = "SETTINGS", Action = "settings" };
            entries.Add(new RightMenuEntry
            {
                Label = LocalizeMenuLabel(settings),
                Action = settings.Action,
                Icon = ResolveMenuIcon(settings.Action)
            });
        }

        return entries;
    }

    private string LocalizeMenuLabel(RightMenuEntry entry)
    {
        string? key = entry.Action.TrimStart('*').ToLowerInvariant() switch
        {
            "save" => "menu.save",
            "load" => "menu.load",
            "backlog" or "lookback" => "menu.backlog",
            "skip" => "menu.skip",
            "settings" or "setting" or "config" => "menu.settings",
            "gallery" => "menu.gallery",
            "reset" => "menu.reset",
            "end" or "quit" or "close" => "menu.end",
            _ => null
        };
        if (key is null) return entry.Label;

        string localized = _vm.Localization.Get(key);
        return localized == key ? entry.Label : localized;
    }

    private static string ResolveMenuIcon(string action) =>
        action.TrimStart('*').ToLowerInvariant() switch
        {
            "save" => "\u25A0",
            "load" => "\u25A1",
            "backlog" or "lookback" => "\u25CF",
            "skip" => "\u25B6",
            "settings" or "setting" or "config" => "\u25C6",
            "gallery" => "\u25C7",
            "reset" => "\u25B2",
            "end" or "quit" or "close" => "\u25BC",
            _ => ""
        };

    private static bool IsDangerMenuAction(string action) =>
        action.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("end", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        action.Equals("close", StringComparison.OrdinalIgnoreCase);

    private static int ToSpriteInt(double value) => Math.Max(0, (int)Math.Round(value));

    private void AddClickCursorCommand(List<BrowserDrawCommand> drawCommands, CanvasScaleMapper mapper)
    {
        if (!_vm.State.UiRuntime.ShowClickCursor || _vm.State.Execution.State != VmState.WaitingForClick)
        {
            return;
        }

        TextWindowState textWindow = _vm.State.TextWindow;
        Sprite? textSprite = textWindow.TextTargetSpriteId >= 0 &&
                             _vm.State.Render.Sprites.TryGetValue(textWindow.TextTargetSpriteId, out Sprite? found) &&
                             found.Type == SpriteType.Text
            ? found
            : null;

        double x = (textSprite?.X ?? textWindow.DefaultTextboxX + textWindow.DefaultTextboxPaddingX) + 2;
        double y = (textSprite?.Y ?? textWindow.DefaultTextboxY + textWindow.DefaultTextboxPaddingY) +
                   (textSprite?.FontSize ?? textWindow.DefaultFontSize) * 1.25 + 2;
        double size = _vm.State.UiRuntime.ClickCursorSize > 0
            ? _vm.State.UiRuntime.ClickCursorSize
            : Math.Clamp(textWindow.DefaultFontSize * 0.38, 8, 24);
        CssPoint css = mapper.MapLogicalToCss(x, y);

        drawCommands.Add(new BrowserDrawCommand
        {
            Kind = BrowserDrawKind.Triangle,
            SpriteId = WebClickCursorSpriteId,
            CssX = css.X,
            CssY = css.Y,
            CssWidth = size * mapper.Scale,
            CssHeight = size * 1.1 * mapper.Scale,
            LogicalX = x,
            LogicalY = y,
            LogicalWidth = size,
            LogicalHeight = size * 1.1,
            FillColor = "#cdcdcf",
            FillAlpha = 255,
            BorderColor = "#e6e6f0",
            BorderWidth = 1,
            BorderOpacity = 200,
            Z = 9900
        });
    }

    private string ResolveMenuAction(string action)
    {
        return _vm.State.MenuRuntime.MenuActionOverrides.TryGetValue(action, out string? overrideAction)
            ? overrideAction
            : action;
    }

    private void QueueWebSave(int slot)
    {
        _vm.SaveGame(slot);
        string path = GetSavePath(slot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Web save payload was not created.", path);
        }

        string payload = File.ReadAllText(path);
        _pendingStorageOperations.Add(IndexedDbSaveStore.WriteSave(slot, payload));
    }

    private string GetSaveDirectory() => Path.Combine(_runtimeRoot, "saves");

    private string GetSavePath(int slot) => Path.Combine(GetSaveDirectory(), $"slot_{slot:00}.ariasav");

    private static int? ParseSaveSlotKey(string key)
    {
        const string prefix = "save:";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(key[prefix.Length..], out int slot) ? slot : null;
    }

    private bool ShouldRenderCompatTextWindow()
    {
        TextWindowState textWindow = _vm.State.TextWindow;
        return textWindow.CompatAutoUi &&
               textWindow.TextboxVisible &&
               !textWindow.UseManualTextLayout &&
               !string.IsNullOrEmpty(_vm.State.TextRuntime.CurrentTextBuffer) &&
               (textWindow.TextTargetSpriteId < 0 ||
                !_vm.State.Render.Sprites.ContainsKey(textWindow.TextTargetSpriteId));
    }
}
