using AriaEngine.Core;
using AriaEngine.Rendering;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var workspace = Path.Combine(Path.GetTempPath(), "aria-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);
var originalCwd = Environment.CurrentDirectory;
Environment.CurrentDirectory = workspace;

try
{
    var reporter = new ErrorReporter();
    var parser = new Parser(reporter);

    var parsed = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "textbox 0, 0, 640, 120",
        "本文: コロンを含む文章\\",
        "lsp_rect 1, 0, 0, 100, 40",
        "spbtn 1, 50",
        "btnwait %0",
        "if %0 == 50 goto *unlock_all",
        "goto *end",
        "*unlock_all",
        "set_pflag chapter_day1, 1",
        "csp -1",
        "*end"
    }, "smoke.aria");

    Assert(parsed.Instructions.Any(i => i.Op == OpCode.Text && i.Arguments.Count > 0 && i.Arguments[0].Contains(":")), "Parser split plain text containing ':'");

    var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    vm.LoadScript(parsed.Instructions, parsed.Labels, "smoke.aria");
    vm.Step();
    Assert(vm.State.State == VmState.WaitingForClick, "VM did not wait for text click");
    vm.ResumeFromClick();
    vm.Step();
    Assert(vm.State.State == VmState.WaitingForButton, "VM did not reach button wait");
    vm.ResumeFromButton(1);
    vm.Step();

    Assert(vm.State.Flags.TryGetValue("chapter_day1", out var unlocked) && unlocked, "UNLOCK ALL style branch did not set pflag");
    Assert(vm.State.Sprites.Count == 0, "csp -1 did not clear sprites");
    Assert(vm.State.SpriteButtonMap.Count == 0, "csp -1 did not clear button map");

    var fontSizeScript = parser.Parse(new[]
    {
        "font_atlas_size 64"
    }, "font-size.aria");
    var fontSizeVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    fontSizeVm.LoadScript(fontSizeScript.Instructions, fontSizeScript.Labels, "font-size.aria");
    fontSizeVm.Step();
    Assert(fontSizeVm.State.FontAtlasSize == 64, "font_atlas_size should preserve small UI font sizes");
    Assert(SpriteRenderer.SelectFontAtlasSize(18) == 18, "Renderer should choose a native atlas size for small UI text");
    Assert(SpriteRenderer.SelectFontAtlasSize(72) == 72, "Renderer should choose a native atlas size for title text");

    var waitScript = parser.Parse(new[] { "wait 250" }, "wait.aria");
    var waitVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    waitVm.LoadScript(waitScript.Instructions, waitScript.Labels, "wait.aria");
    waitVm.Step();
    Assert(waitVm.State.State == VmState.WaitingForDelay, "wait <ms> should be a timed delay");
    Assert(waitVm.State.DelayTimerMs == 250f, "wait <ms> should store delay milliseconds");

    var waitClickScript = parser.Parse(new[] { "compat_mode on", "textspeed 40", "最後の一文", "@" }, "wait-click.aria");
    var waitClickVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    waitClickVm.LoadScript(waitClickScript.Instructions, waitClickScript.Labels, "wait-click.aria");
    for (int i = 0; i < 12 && waitClickVm.State.State != VmState.WaitingForClick; i++)
    {
        if (waitClickVm.State.State == VmState.Running) waitClickVm.Step();
        else if (waitClickVm.State.State == VmState.WaitingForAnimation) waitClickVm.Update(1000f);
    }
    Assert(waitClickVm.State.State == VmState.WaitingForClick, "@ should enter click wait");
    Assert(waitClickVm.State.DisplayedTextLength == waitClickVm.State.CurrentTextBuffer.Length, "click wait should reveal full text before cursor display");

    var inlineWaitScript = parser.Parse(new[] { "A@B\\C" }, "inline-wait.aria");
    Assert(inlineWaitScript.Instructions.Count(i => i.Op == OpCode.WaitClick) == 1, "inline @ should become a click wait");
    Assert(inlineWaitScript.Instructions.Count(i => i.Op == OpCode.WaitClickClear) == 1, "inline \\ should become a click wait with page clear");
    Assert(inlineWaitScript.Instructions.Where(i => i.Op == OpCode.Text).Select(i => i.Arguments[0]).SequenceEqual(new[] { "A", "B", "C" }), "inline waits should preserve surrounding text order");

    var skipScript = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "一@",
        "二@",
        "三@",
        "set_vflag skip_done, 1"
    }, "skip.aria");
    var skipVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    skipVm.LoadScript(skipScript.Instructions, skipScript.Labels, "skip.aria");
    skipVm.State.SkipMode = true;
    skipVm.State.SkipUnread = true;
    skipVm.State.SkipRateMs = 1; // テスト用に最短間隔
    int skippedWaits = 0;
    for (int i = 0; i < 20 && skippedWaits < 3; i++)
    {
        skippedWaits += skipVm.ProcessSkipFrame(250);
    }
    Assert(skippedWaits >= 3, "Skip should advance through multiple waits");
    // スキップ後の残り命令を実行させる
    for (int i = 0; i < 10 && !skipVm.State.VolatileFlags.ContainsKey("skip_done"); i++)
    {
        skipVm.ProcessSkipFrame(250);
    }
    Assert(skipVm.State.VolatileFlags.TryGetValue("skip_done", out var skipDone) && skipDone, "Fast skip should continue script execution after skipped waits");
    skipVm.StopSkip();
    Assert(!skipVm.State.SkipMode, "Manual advance input should be able to stop skip mode");

    var forceSkipVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    forceSkipVm.LoadScript(skipScript.Instructions, skipScript.Labels, "skip.aria");
    forceSkipVm.State.ForceSkipMode = true;
    forceSkipVm.State.SkipRateMs = 1;
    int forceSkippedWaits = 0;
    for (int i = 0; i < 20 && forceSkippedWaits < 3; i++)
    {
        forceSkippedWaits += forceSkipVm.ProcessSkipFrame(250);
    }
    Assert(forceSkippedWaits >= 3, "Ctrl force skip should skip unread waits without enabling SkipUnread");

    var throttledSkipScript = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "一@",
        "二@",
        "三@",
        "四@",
        "五@",
        "六@",
        "七@",
        "八@",
        "九@",
        "十@",
        "set_vflag throttled_skip_done, 1"
    }, "throttled-skip.aria");
    var throttledSkipVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    throttledSkipVm.LoadScript(throttledSkipScript.Instructions, throttledSkipScript.Labels, "throttled-skip.aria");
    throttledSkipVm.State.SkipMode = true;
    throttledSkipVm.State.SkipUnread = true;
    throttledSkipVm.State.SkipRateMs = 200; // デフォルト5msg/秒
    int throttledWaits = throttledSkipVm.ProcessSkipFrame(250);
    Assert(throttledWaits == 1, "normal skip should advance exactly 1 wait per ~200ms");
    Assert(!throttledSkipVm.State.VolatileFlags.ContainsKey("throttled_skip_done"), "normal skip should not finish long wait chains in one frame by default");

    var uiScript = parser.Parse(new[]
    {
        "ui textbox, padding, 34, 30",
        "ui textbox, x, 72",
        "ui rmenu, w, 420",
        "ui save, columns, 2",
        "ui skip, speed, 5",
        "ui skip, force_speed, 40",
        "ui text, speed, 18",
        "ui text, advance, ratio, 0.5",
        "ui text, shadow, 2, 3, \"#101010\"",
        "ui text, outline, 1, \"#202020\"",
        "ui text, effect, shake, 1.5, 10",
        "ui cursor, mode, \"engine\"",
        "ui cursor, size, 12",
        "ui_rect 300, 250, 150, 340, 50",
        "ui_text 301, \"START\", 420, 164",
        "ui_group 900",
        "ui_group_add 900, 300",
        "ui_group_add 900, 301",
        "ui_button 300, 100",
        "ui_rect 302, 0, 0, 10, 10",
        "ui sprite:302, hover_scale, 1.04",
        "ui_tween 300, \"opacity\", 0.5, 220, \"out_cubic\"",
        "ui_group_clear 900"
    }, "ui.aria");
    var uiVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    uiVm.LoadScript(uiScript.Instructions, uiScript.Labels, "ui.aria");
    for (int i = 0; i < uiScript.Instructions.Count; i++) uiVm.Step();
    Assert(uiVm.State.DefaultTextboxPaddingX == 34 && uiVm.State.DefaultTextboxPaddingY == 30, "ui textbox padding should update textbox defaults");
    Assert(uiVm.State.DefaultTextboxX == 72, "ui textbox x should update textbox default x");
    Assert(uiVm.State.RightMenuWidth == 420, "ui rmenu w should update menu style");
    Assert(uiVm.State.SaveLoadColumns == 2, "ui save columns should update save/load style");
    Assert(uiVm.State.SkipAdvancePerFrame == 5, "ui skip speed should update normal skip throttle");
    Assert(uiVm.State.ForceSkipAdvancePerFrame == 40, "ui skip force_speed should update Ctrl skip throttle");
    Assert(uiVm.State.TextSpeedMs == 18, "ui text speed should update typewriter speed");
    Assert(uiVm.State.TextAdvanceMode == "ratio" && Math.Abs(uiVm.State.TextAdvanceRatio - 0.5f) < 0.001f, "ui text advance ratio should control advance threshold");
    Assert(uiVm.State.DefaultTextShadowX == 2 && uiVm.State.DefaultTextShadowY == 3 && uiVm.State.DefaultTextShadowColor == "#101010", "ui text shadow should update default text-only shadow");
    Assert(uiVm.State.DefaultTextOutlineSize == 1 && uiVm.State.DefaultTextOutlineColor == "#202020", "ui text outline should update default text-only outline");
    Assert(uiVm.State.DefaultTextEffect == "shake" && Math.Abs(uiVm.State.DefaultTextEffectStrength - 1.5f) < 0.001f, "ui text effect should update default text-only effect");
    Assert(uiVm.State.ClickCursorMode == "engine", "ui cursor mode engine should keep engine-rendered cursor");
    Assert(uiVm.State.ClickCursorSize == 12, "ui cursor size should update engine cursor size");
    Assert(Math.Abs(uiVm.State.Sprites[302].HoverScale - 1.04f) < 0.001f, "ui sprite:<id> should not be split as a multi-statement");
    Assert(!uiVm.State.Sprites.ContainsKey(300) && !uiVm.State.Sprites.ContainsKey(301), "ui_group_clear should remove group sprites");
    Assert(!uiVm.State.SpriteButtonMap.ContainsKey(300), "ui_group_clear should remove button bindings");

    var uiStateStyleScript = parser.Parse(new[]
    {
        "ui_rect 410, 0, 0, 120, 32",
        "ui_button 410, 10",
        "ui_state_style 410, \"hover\", fill, \"#333333\""
    }, "ui-state-style.aria");
    var uiStateStyleVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    uiStateStyleVm.LoadScript(uiStateStyleScript.Instructions, uiStateStyleScript.Labels, "ui-state-style.aria");
    for (int i = 0; i < uiStateStyleScript.Instructions.Count; i++) uiStateStyleVm.Step();
    Assert(uiStateStyleVm.State.Sprites[410].HoverFillColor == "#333333", "ui_state_style hover fill should affect hover rendering");

    var uiQualityScript = parser.Parse(new[] { "ui_quality ultra", "ui_motion on, 20" }, "ui-quality.aria");
    var uiQualityVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    uiQualityVm.LoadScript(uiQualityScript.Instructions, uiQualityScript.Labels, "ui-quality.aria");
    uiQualityVm.Step();
    Assert(uiQualityVm.State.UiQualityMode == "ultra", "ui_quality should switch quality mode");
    Assert(uiQualityVm.State.RoundedRectSegments == 96, "ui_quality ultra should maximize rounded rectangle quality");
    Assert(uiQualityVm.State.SubpixelUiRendering, "ui_quality ultra should enable subpixel UI rendering");
    Assert(uiQualityVm.State.UiMotionResponse == 20f, "ui_motion should set response speed");

    var modernReporter = new ErrorReporter();
    var modernParser = new Parser(modernReporter);
    var modernScript = modernParser.Parse(new[]
    {
        "const BTN_START = 300",
        "enum MenuResult { Start = 1, Load, Exit = 9 }",
        "namespace title {",
        "*entry",
        "ui_rect(BTN_START, 10, 20, 120, 32)",
        "ui_button(BTN_START, MenuResult.Start)",
        "mov %1, MenuResult.Load",
        "if (%1 == MenuResult.Load) {",
        "set_vflag modern_block, 1",
        "}",
        "goto *done",
        "*done",
        "}",
    }, "modern.aria");
    Assert(!modernReporter.Errors.Any(e => e.Level == AriaErrorLevel.Error), "Modern syntax should not report parser errors");
    Assert(modernScript.Labels.ContainsKey("title.entry") && modernScript.Labels.ContainsKey("title.done"), "namespace should qualify labels");

    var modernVm = new VirtualMachine(modernReporter, new TweenManager(), new SaveManager(modernReporter), new ConfigManager());
    modernVm.LoadScript(modernScript.Instructions, modernScript.Labels, "modern.aria");
    for (int i = 0; i < 50 && modernVm.State.ProgramCounter < modernScript.Instructions.Count; i++)
    {
        if (modernVm.State.State == VmState.Running) modernVm.Step();
    }
    Assert(modernVm.State.Sprites.ContainsKey(300), "const + function-style ui_rect should create sprite");
    Assert(modernVm.State.SpriteButtonMap.TryGetValue(300, out var modernButton) && modernButton == 1, "enum + function-style ui_button should bind result");
    Assert(modernVm.State.VolatileFlags.TryGetValue("modern_block", out var modernBlock) && modernBlock, "C++ style if braces should execute block");

    var modernLineReporter = new ErrorReporter();
    var modernLineParser = new Parser(modernLineReporter);
    modernLineParser.Parse(new[]
    {
        "const MODERN_VALUE = 1",
        "namespace modern {",
        "*entry",
        "goto *missing",
        "}"
    }, "modern-lines.aria");
    Assert(modernLineReporter.Errors.Any(e => e.LineNumber == 4), "modern preprocessing should preserve original source line numbers");

    var cursorScript = parser.Parse(new[] { "clickcursor engine", "clickcursor \"cursor.bmp\"" }, "cursor.aria");
    var cursorVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    cursorVm.LoadScript(cursorScript.Instructions, cursorScript.Labels, "cursor.aria");
    cursorVm.Step();
    Assert(cursorVm.State.ClickCursorMode == "image" && cursorVm.State.ClickCursorPath == "cursor.bmp", "clickcursor path should opt into image mode");

    var tweenState = new GameState();
    tweenState.Sprites[1] = new Sprite { Id = 1, Type = SpriteType.Rect, X = 0f, Y = 0f };
    var tweenManager = new TweenManager();
    tweenManager.Add(new Tween { SpriteId = 1, Property = "x", From = 0f, To = 1f, DurationMs = 10f });
    tweenManager.Update(tweenState, 5f);
    Assert(tweenState.Sprites[1].X > 0f && tweenState.Sprites[1].X < 1f, "Tween should preserve fractional x positions");

    var quakeVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    quakeVm.State.QuakeAmplitude = 5;
    quakeVm.State.QuakeTimerMs = 10f;
    quakeVm.Update(11f);
    Assert(quakeVm.State.QuakeTimerMs == 0f && quakeVm.State.QuakeAmplitude == 0, "Quake should stop after its timer expires");

    var fadeScript = parser.Parse(new[] { "bgm \"loop.ogg\"", "bgmfade 250" }, "bgmfade.aria");
    var fadeVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    fadeVm.LoadScript(fadeScript.Instructions, fadeScript.Labels, "bgmfade.aria");
    fadeVm.Step();
    fadeVm.Step();
    Assert(fadeVm.State.BgmFadeOutTimerMs == 250f && fadeVm.State.CurrentBgm == "loop.ogg", "bgmfade should schedule a fade instead of stopping immediately");

    var persistentScript = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "set_pflag auto_unlock, 1",
        "set_sflag route_seen, 1",
        "set_counter total_clicks, 7",
        "自動既読保存@"
    }, "persistent.aria");
    var persistentVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    persistentVm.LoadScript(persistentScript.Instructions, persistentScript.Labels, "persistent.aria");
    persistentVm.Step();
    persistentVm.Step();

    var restoredPersistentVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    Assert(restoredPersistentVm.State.Flags.TryGetValue("auto_unlock", out var autoUnlock) && autoUnlock, "pflag should auto-save without user slot save");
    Assert(restoredPersistentVm.State.SaveFlags.TryGetValue("route_seen", out var routeSeen) && routeSeen, "sflag should auto-save to persistent.ariasav without user slot save");
    Assert(restoredPersistentVm.State.Counters.TryGetValue("total_clicks", out var totalClicks) && totalClicks == 7, "counter should auto-save to persistent.ariasav immediately");
    Assert(restoredPersistentVm.State.ReadKeys.Any(key => key.Contains("persistent.aria", StringComparison.OrdinalIgnoreCase)), "read history should auto-save without user slot save");

    var autoSaveScript = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "自動進行保存@",
        "set_vflag after_autosave_wait, 1"
    }, "autosave.aria");
    var autoSaveVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    autoSaveVm.LoadScript(autoSaveScript.Instructions, autoSaveScript.Labels, "autosave.aria");
    autoSaveVm.Step();
    Assert(autoSaveVm.State.State == VmState.WaitingForClick, "Autosave fixture should stop at click wait");
    Assert(autoSaveVm.Saves.HasSaveData(SaveManager.AutoSaveSlot), "Click wait should create hidden autosave data");
    var autoLoadVm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    autoLoadVm.LoadScript(autoSaveScript.Instructions, autoSaveScript.Labels, "autosave.aria");
    Assert(autoLoadVm.LoadAutoSaveGame(), "autoload should restore hidden autosave data");
    Assert(autoLoadVm.State.State == VmState.WaitingForClick && autoLoadVm.State.CurrentTextBuffer.Contains("自動進行保存"), "autoload should restore text wait progress");

    vm.JumpTo("*missing_label");
    Assert(vm.State.State != VmState.Ended || reporter.Errors.Any(e => e.Code == "VM_LABEL_MISSING"), "Missing label was not reported safely");

    var saveReporter = new ErrorReporter();
    var saveParser = new Parser(saveReporter);
    var saveScript = saveParser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "textbox 0, 0, 640, 120",
        "fontsize 28",
        "textbox_style 18, 2, \"#ffffff\", 90, 24, 16, 1, 2, \"#000000\", 80",
        "ロード前@",
        "ロード後@"
    }, "save-load.aria");

    var saveVm = new VirtualMachine(saveReporter, new TweenManager(), new SaveManager(saveReporter), new ConfigManager());
    saveVm.LoadScript(saveScript.Instructions, saveScript.Labels, "save-load.aria");
    saveVm.Step();
    Assert(saveVm.State.State == VmState.WaitingForClick, "Save fixture did not reach first wait");
    saveVm.SaveGame(1);

    var loadVm = new VirtualMachine(saveReporter, new TweenManager(), new SaveManager(saveReporter), new ConfigManager());
    loadVm.LoadScript(saveScript.Instructions, saveScript.Labels, "save-load.aria");
    loadVm.LoadGame(1);
    Assert(loadVm.State.TextTargetSpriteId >= 0, "Load did not restore text target id");
    Assert(loadVm.State.TextboxBackgroundSpriteId >= 0, "Load did not restore textbox background id");
    Assert(loadVm.State.Sprites[loadVm.State.TextboxBackgroundSpriteId].CornerRadius == 18, "Load did not normalize textbox background style");
    Assert(loadVm.State.Sprites[loadVm.State.TextTargetSpriteId].X == 24, "Load did not normalize text target padding");
    loadVm.ResumeFromClick();
    loadVm.Step();

    int textboxRects = loadVm.State.Sprites.Values.Count(s => s.Type == SpriteType.Rect && s.Z == 9000);
    int textTargets = loadVm.State.Sprites.Values.Count(s => s.Type == SpriteType.Text && s.Z == 9001);
    Assert(textboxRects <= 1, "Load created duplicate textbox background sprites");
    Assert(textTargets <= 1, "Load created duplicate textbox text sprites");

    var choiceLoadReporter = new ErrorReporter();
    var choiceLoadParser = new Parser(choiceLoadReporter);
    var choiceLoadScript = choiceLoadParser.Parse(new[]
    {
        "compat_mode on",
        "choice \"A\", \"B\"",
        "text \"after\""
    }, "choice-load.aria");
    var choiceSaveVm = new VirtualMachine(choiceLoadReporter, new TweenManager(), new SaveManager(choiceLoadReporter), new ConfigManager());
    choiceSaveVm.LoadScript(choiceLoadScript.Instructions, choiceLoadScript.Labels, "choice-load.aria");
    choiceSaveVm.Step();
    Assert(choiceSaveVm.State.State == VmState.WaitingForButton, "Choice load fixture did not reach button wait");
    choiceSaveVm.SaveGame(5);

    var choiceLoadedVm = new VirtualMachine(choiceLoadReporter, new TweenManager(), new SaveManager(choiceLoadReporter), new ConfigManager());
    choiceLoadedVm.LoadScript(choiceLoadScript.Instructions, choiceLoadScript.Labels, "choice-load.aria");
    choiceLoadedVm.LoadGame(5);
    int loadedChoiceButton = choiceLoadedVm.State.SpriteButtonMap.Keys.First();
    choiceLoadedVm.ResumeFromButton(loadedChoiceButton);
    Assert(!choiceLoadedVm.State.Sprites.Values.Any(s => s.Z is 9500 or 9501), "Loaded compat choice sprites should be tracked and cleared after button resume");

    // --- func syntax test ---
    var funcReporter = new ErrorReporter();
    var funcParser = new Parser(funcReporter);
    var funcScript = funcParser.Parse(new[]
    {
        "textspeed 0",
        "textbox 0, 0, 640, 120",
        "func greet(name: string) -> void",
        "    text \"Hello, \", $name",
        "endfunc",
        "func calc_sum(a: int, b: int) -> int",
        "    let %result = %a + %b",
        "    return %result",
        "endfunc",
        "greet(\"World\")",
        "calc_sum(10, 20)",
        "@"
    }, "func-test.aria");

    Assert(funcScript.Functions.Count == 2, $"Expected 2 functions, got {funcScript.Functions.Count}");
    Assert(funcScript.Functions.Any(f => f.QualifiedName == "greet"), "greet function not found");
    Assert(funcScript.Functions.Any(f => f.QualifiedName == "calc_sum"), "calc_sum function not found");
    Assert(funcScript.Instructions.Any(i => i.Op == OpCode.Gosub && i.Arguments.Count > 0 && i.Arguments[0] == "*greet"), "greet call not expanded to gosub");
    Assert(funcScript.Instructions.Any(i => i.Op == OpCode.Gosub && i.Arguments.Count > 0 && i.Arguments[0] == "*calc_sum"), "calc_sum call not expanded to gosub");

    // --- switch syntax test ---
    var switchReporter = new ErrorReporter();
    var switchParser = new Parser(switchReporter);
    var switchScript = switchParser.Parse(new[]
    {
        "#define TEST_VAL 2",
        "auto %x = TEST_VAL",
        "switch %x {",
        "    case 1:",
        "        text \"one\"",
        "    case 2:",
        "        text \"two\"",
        "    default:",
        "        text \"other\"",
        "}",
        "@"
    }, "switch-test.aria");
    
    Assert(switchScript.Instructions.Any(i => i.Op == OpCode.Text && i.Arguments.Any(a => a.Contains("two"))), "switch case 2 should expand to text 'two'");
    Assert(!switchReporter.Errors.Any(e => e.Level == AriaErrorLevel.Error), $"Switch syntax should not report parser errors. Errors: {string.Join(", ", switchReporter.Errors.Select(e => e.Message))}");

    // --- array test ---
    var arrayReporter = new ErrorReporter();
    var arrayVm = new VirtualMachine(arrayReporter, new TweenManager(), new SaveManager(arrayReporter), new ConfigManager());
    var arrayParser = new Parser(arrayReporter);
    var arrayScript = arrayParser.Parse(new[]
    {
        "let %arr[0] = 100",
        "let %arr[1] = 200",
        "let %x = %arr[0]",
        "let %y = %arr[1]",
        "for %i = 0 to arr",
        "    let %z = %arr[%i]",
        "next",
        "@"
    }, "array-test.aria");
    arrayVm.LoadScript(arrayScript, "array-test.aria");
    
    // Run until end or waiting state
    for (int i = 0; i < 50 && arrayVm.State.State == VmState.Running; i++)
    {
        arrayVm.Step();
    }
    
    Assert(arrayVm.State.Arrays.ContainsKey("arr"), "Array 'arr' should be created");
    Assert(arrayVm.State.Arrays["arr"].Length >= 2, "Array should have at least 2 elements");
    Assert(arrayVm.State.Arrays["arr"][0] == 100, "arr[0] should be 100");
    Assert(arrayVm.State.Arrays["arr"][1] == 200, "arr[1] should be 200");
    Assert(arrayVm.State.Registers.GetValueOrDefault("x", 0) == 100, "x should be loaded from arr[0]");
    Assert(arrayVm.State.Registers.GetValueOrDefault("y", 0) == 200, "y should be loaded from arr[1]");

    // --- ref test ---
    var refReporter = new ErrorReporter();
    var refVm = new VirtualMachine(refReporter, new TweenManager(), new SaveManager(refReporter), new ConfigManager());
    var refParser = new Parser(refReporter);
    var refScript = refParser.Parse(new[]
    {
        "func swap(ref a: int, ref b: int)",
        "    let %temp = %a",
        "    let %a = %b",
        "    let %b = %temp",
        "endfunc",
        "let %x = 10",
        "let %y = 20",
        "swap(ref %x, ref %y)",
        "@"
    }, "ref-test.aria");
    refVm.LoadScript(refScript, "ref-test.aria");
    
    for (int i = 0; i < 50 && refVm.State.State == VmState.Running; i++)
    {
        refVm.Step();
    }
    
    Console.WriteLine($"REF x={refVm.State.Registers.GetValueOrDefault("x", 0)} y={refVm.State.Registers.GetValueOrDefault("y", 0)}");
    Assert(refVm.State.Registers.GetValueOrDefault("x", 0) == 20, "x should be swapped to 20");
    Assert(refVm.State.Registers.GetValueOrDefault("y", 0) == 10, "y should be swapped to 10");

    Console.WriteLine("ARIA smoke tests passed.");
}
finally
{
    Environment.CurrentDirectory = originalCwd;
    try { Directory.Delete(workspace, recursive: true); } catch { }
}
