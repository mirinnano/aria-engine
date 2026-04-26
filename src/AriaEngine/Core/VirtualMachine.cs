using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core.Commands;
using AriaEngine.Rendering;
using AriaEngine.UI;
using AriaEngine.Utility;
using Raylib_cs;

namespace AriaEngine.Core;

public class VirtualMachine
{
    private List<Instruction> _instructions = new();
    private Dictionary<string, int> _labels = new();
    public GameState State { get; set; }
    private readonly ErrorReporter _reporter;
    private string _currentScriptFile = "";
    internal ErrorReporter Reporter => _reporter;
    public string CurrentScriptFile => _currentScriptFile;

    public TweenManager Tweens { get; private set; }
    public SaveManager Saves { get; private set; }
    public ConfigManager Config { get; private set; }
    public MenuSystem Menu { get; private set; }
    public SpritePool SpritePool { get; private set; }

    private Dictionary<string, string> _characterPaths = new();
    private readonly HashSet<int> _activeCompatUiSpriteIds = new();
    private int _nextCompatUiSpriteId = 50000;
    private bool _persistentDirty;
    private float _persistentSaveTimerMs;
    private readonly List<ICommandHandler> _commandHandlers;
    private readonly Dictionary<OpCode, ICommandHandler> _handlerMap;

    public VirtualMachine(ErrorReporter reporter, TweenManager tweens, SaveManager saves, ConfigManager config)
    {
        _reporter = reporter;
        Tweens = tweens;
        Saves = saves;
        Config = config;
        State = new GameState();
        Menu = new MenuSystem(this);
        SpritePool = new SpritePool(128); // スプライトプールを初期化
        _commandHandlers = new List<ICommandHandler>
        {
            new CoreCommandHandler(this),
            new ScriptCommandHandler(this),
            new InputCommandHandler(this),
            new RenderCommandHandler(this),
            new SpriteDecoratorCommandHandler(this),
            new TweenCommandHandler(this),
            new TextCommandHandler(this),
            new UiCommandHandler(this),
            new SaveCommandHandler(this),
            new FlowCommandHandler(this),
            new AudioCommandHandler(this),
            new SystemCommandHandler(this),
            new FlagCommandHandler(this),
            new CompatibilityCommandHandler(this)
        };
        _handlerMap = _commandHandlers
            .SelectMany(handler => handler.HandledCodes.Select(code => new { code, handler }))
            .ToDictionary(item => item.code, item => item.handler);

        // Load initial config into GameState
        State.TextSpeedMs = Config.Config.GlobalTextSpeedMs;
        State.BgmVolume = Config.Config.BgmVolume;
        State.SeVolume = Config.Config.SeVolume;
        var persistent = Config.LoadPersistentGameData();
        State.SkipUnread = persistent.SkipUnread;
        State.Registers = new Dictionary<string, int>(persistent.Registers, StringComparer.OrdinalIgnoreCase);
        State.Flags = new Dictionary<string, bool>(persistent.Flags, StringComparer.OrdinalIgnoreCase);
        State.SaveFlags = new Dictionary<string, bool>(persistent.SaveFlags, StringComparer.OrdinalIgnoreCase);
        State.Counters = new Dictionary<string, int>(persistent.Counters, StringComparer.OrdinalIgnoreCase);
        State.ReadKeys = new HashSet<string>(persistent.ReadKeys, StringComparer.OrdinalIgnoreCase);

        // Initialize new managers
        ChapterManager = new ChapterManager(reporter);
        ChapterManager.LoadChapters();

        GameFlow = new GameFlowManager();
        GameFlow.Initialize();

        CharacterManager = new CharacterManager(State, tweens, reporter);
        CharacterManager.LoadCharacterData();
    }

    public ChapterManager ChapterManager { get; private set; }
    public GameFlowManager GameFlow { get; private set; }
    public CharacterManager CharacterManager { get; private set; }

    public void LoadScript(List<Instruction> instructions, Dictionary<string, int> labels, string file)
    {
        _instructions = instructions;
        _labels = labels;
        _currentScriptFile = file;
        State.ProgramCounter = 0;
        State.State = VmState.Running;
    }

    public void ResumeFromClick()
    {
        if (State.State == VmState.WaitingForClick)
        {
            State.State = VmState.Running;
        }
    }

    public void ResumeFromButton(int buttonId)
    {
        if (State.State == VmState.WaitingForButton)
        {
            int resultValue = State.SpriteButtonMap.TryGetValue(buttonId, out int mappedValue) ? mappedValue : buttonId;

            // Set explicit target register + compatibility registers
            string targetReg = State.ButtonResultRegister.TrimStart('%').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(targetReg)) targetReg = "0";
            SetReg(targetReg, resultValue);
            SetReg("0", resultValue);
            SetReg("r0", resultValue);

            ClearCompatUiSprites();

            State.State = VmState.Running;
            if (State.UiEvents.TryGetValue($"{buttonId}:click", out string? label) ||
                State.UiEvents.TryGetValue($"{resultValue}:click", out label))
            {
                JumpTo(label);
            }
            State.ButtonTimeoutMs = 0;
            State.ButtonTimer = 0f;
            State.ButtonResultRegister = "0";
        }
    }

    public void SignalTimeout()
    {
        if (State.State == VmState.WaitingForButton)
        {
            string targetReg = State.ButtonResultRegister.TrimStart('%').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(targetReg)) targetReg = "0";
            SetReg(targetReg, -1);
            SetReg("0", -1);
            SetReg("r0", -1);
            State.State = VmState.Running;
            State.ButtonTimeoutMs = 0;
            State.ButtonTimer = 0f;
            State.ButtonResultRegister = "0";
        }
    }

    public void FinishFade()
    {
        if (State.State == VmState.FadingIn || State.State == VmState.FadingOut)
        {
            State.IsFading = false;
            State.State = VmState.Running;
        }
    }

    public void Step()
    {
        while (State.State == VmState.Running && State.ProgramCounter < _instructions.Count)
        {
            var inst = _instructions[State.ProgramCounter];
            State.ProgramCounter++;
            State.CurrentInstructionWasRead = IsInstructionRead(inst);
            MarkInstructionRead(inst);

            try
            {
                if (EvaluateCondition(inst.Condition))
                {
                    ExecuteInstruction(inst);
                }
            }
            catch (Exception ex)
            {
                _reporter.Report(new AriaError($"実行時エラー: {ex.Message}", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Error));
            }
        }

        if (State.State == VmState.Running && State.ProgramCounter >= _instructions.Count)
        {
            State.State = VmState.Ended;
        }
    }

    public void Update(float deltaTimeMs)
    {
        if (State.QuakeTimerMs > 0f)
        {
            State.QuakeTimerMs = Math.Max(0f, State.QuakeTimerMs - deltaTimeMs);
            if (State.QuakeTimerMs <= 0f) State.QuakeAmplitude = 0;
        }

        // Typewriter effect tracking
        if (State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
        {
            State.TextTimerMs += deltaTimeMs;
            while (State.TextTimerMs >= State.TextSpeedMs && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
            {
                State.TextTimerMs -= State.TextSpeedMs;
                State.DisplayedTextLength++;
            }

            if (State.Sprites.TryGetValue(State.TextTargetSpriteId, out var txtSprite))
            {
                int length = Math.Clamp(State.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
                txtSprite.Text = State.CurrentTextBuffer.Substring(0, length);
            }

            if (State.DisplayedTextLength >= State.CurrentTextBuffer.Length && State.State == VmState.WaitingForAnimation)
            {
                State.State = VmState.Running; // Resume script
            }
        }

        if (State.State == VmState.WaitingForDelay)
        {
            State.DelayTimerMs -= deltaTimeMs;
            if (State.DelayTimerMs <= 0) State.State = VmState.Running;
        }

        if (State.State == VmState.WaitingForClick && State.AutoMode)
        {
            State.AutoModeTimerMs += deltaTimeMs;
            if (State.AutoModeTimerMs >= State.AutoModeWaitTimeMs)
            {
                State.AutoModeTimerMs = 0;
                ResumeFromClick();
            }
        }

        // Tween完了チェック: awaitで止まっているとき、全Tweenが終了したらRunningに戻す
        if (State.State == VmState.WaitingForAnimation && !Tweens.IsAnimating)
        {
            // タイプライター中でもなければ復帰
            bool typewriterActive = State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length;
            if (!typewriterActive)
            {
                State.State = VmState.Running;
            }
        }
        
        State.ScriptTimerMs += deltaTimeMs;
        if (_persistentDirty)
        {
            _persistentSaveTimerMs += deltaTimeMs;
            if (_persistentSaveTimerMs >= 1000f)
            {
                SavePersistentState();
            }
        }
    }

    internal bool EvaluateCondition(IReadOnlyList<string>? condTokens)
    {
        if (condTokens == null || condTokens.Count == 0) return true;

        bool allTrue = true;
        for (int i = 0; i < condTokens.Count; i++)
        {
            if (condTokens[i] == "&&") continue;

            string lhsStr = condTokens[i];
            string op = (i + 1 < condTokens.Count) ? condTokens[i + 1] : "==";
            // If it's a direct boolean variable (in NScripter typically flags are == 1)
            if (op == "&&")
            {
                if (GetVal(lhsStr) == 0) allTrue = false;
                continue;
            }

            string rhsStr = (i + 2 < condTokens.Count) ? condTokens[i + 2] : "0";

            if (op == "==" || op == "!=" || op == ">" || op == "<" || op == ">=" || op == "<=")
            {
                int lhs = GetVal(lhsStr);
                int rhs = GetVal(rhsStr);
                bool result = false;
                switch (op)
                {
                    case "==": result = lhs == rhs; break;
                    case "!=": result = lhs != rhs; break;
                    case ">":  result = lhs > rhs; break;
                    case "<":  result = lhs < rhs; break;
                    case ">=": result = lhs >= rhs; break;
                    case "<=": result = lhs <= rhs; break;
                }
                if (!result) allTrue = false;
                i += 2;
            }
            else
            {
                // Fallback direct evaluation
                if (GetVal(lhsStr) == 0) allTrue = false;
            }
        }
        return allTrue;
    }

    private void ExecuteInstruction(Instruction inst)
    {
        if (_handlerMap.TryGetValue(inst.Op, out var handler))
        {
            handler.Execute(inst);
            return;
        }

        _reporter.Report(new AriaError(
            $"未実装の命令 '{inst.Op}' はスキップされました。",
            inst.SourceLine,
            _currentScriptFile,
            AriaErrorLevel.Warning,
            "VM_OPCODE_UNHANDLED",
            hint: "CommandHandler の HandledCodes と Execute 実装を確認してください。"));
    }
    private void MarkInstructionRead(Instruction inst)
    {
        if (!State.KidokuMode) return;
        if (State.ReadKeys.Add($"{_currentScriptFile}:{inst.SourceLine}"))
        {
            MarkPersistentDirty();
        }
    }

    private bool IsInstructionRead(Instruction inst)
    {
        return State.ReadKeys.Contains($"{_currentScriptFile}:{inst.SourceLine}");
    }

    internal void MarkPersistentDirty()
    {
        _persistentDirty = true;
        SavePersistentState();
    }

    public void SavePersistentState()
    {
        Config.Config.GlobalTextSpeedMs = State.TextSpeedMs;
        Config.Config.BgmVolume = State.BgmVolume;
        Config.Config.SeVolume = State.SeVolume;
        Config.Config.SkipUnread = State.SkipUnread;
        Config.SavePersistentGameData(new PersistentGameData
        {
            SkipUnread = State.SkipUnread,
            Registers = State.Registers
                .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Flags = new Dictionary<string, bool>(State.Flags, StringComparer.OrdinalIgnoreCase),
            SaveFlags = new Dictionary<string, bool>(State.SaveFlags, StringComparer.OrdinalIgnoreCase),
            Counters = new Dictionary<string, int>(State.Counters, StringComparer.OrdinalIgnoreCase),
            ReadKeys = State.ReadKeys.ToList()
        });
        Config.Save();
        _persistentDirty = false;
        _persistentSaveTimerMs = 0f;
    }

    internal void AddBacklogEntry()
    {
        if (!State.BacklogEnabled) return;
        string text = State.CurrentTextBuffer.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (State.TextHistory.Count == 0 || State.TextHistory[^1] != text)
        {
            State.TextHistory.Add(text);
            const int maxTextHistory = 300;
            if (State.TextHistory.Count > maxTextHistory)
            {
                int removeCount = State.TextHistory.Count - maxTextHistory;
                State.TextHistory.RemoveRange(0, removeCount);
                State.TextHistoryStartNumber += removeCount;
            }
        }
    }

    internal bool ValidateArgs(Instruction inst, int min)
    {
        if (inst.Arguments.Count >= min) return true;

        var info = CommandRegistry.GetInfo(inst.Op);
        string commandName = info?.CanonicalName ?? inst.Op.ToString();
        int required = info?.MinArgs > min ? info.MinArgs : min;
        _reporter.Report(new AriaError(
            $"'{commandName}' には最低 {required} 個の引数が必要です（渡された引数: {inst.Arguments.Count}）",
            inst.SourceLine,
            _currentScriptFile,
            AriaErrorLevel.Error,
            "VM_ARG_MISSING",
            details: $"op={inst.Op}; args=[{string.Join(", ", inst.Arguments)}]",
            hint: "命令の引数数を確認してください。VMはこの命令をスキップして続行します。"));
        return false;
    }

    internal int AllocateCompatUiSpriteId()
    {
        while (State.Sprites.ContainsKey(_nextCompatUiSpriteId))
        {
            _nextCompatUiSpriteId++;
        }
        return _nextCompatUiSpriteId++;
    }

    internal void TrackCompatUiSprite(int spriteId)
    {
        _activeCompatUiSpriteIds.Add(spriteId);
    }

    internal void ClearCompatUiSprites()
    {
        if (_activeCompatUiSpriteIds.Count == 0) return;

        foreach (int id in _activeCompatUiSpriteIds)
        {
            State.Sprites.Remove(id);
            State.SpriteButtonMap.Remove(id);
        }

        _activeCompatUiSpriteIds.Clear();
    }

    internal bool HasAnyVisibleButton()
    {
        return State.Sprites.Values.Any(s => s.Visible && s.IsButton);
    }

    internal void ApplyUiTheme(string themeNameRaw)
    {
        string themeName = themeNameRaw.Trim().ToLowerInvariant();

        if (themeName == "classic")
        {
            State.DefaultTextboxCornerRadius = 6;
            State.DefaultTextboxBorderWidth = 2;
            State.DefaultTextboxBorderColor = "#d1d5db";
            State.DefaultTextboxBorderOpacity = 120;
            State.DefaultTextboxShadowOffsetX = 0;
            State.DefaultTextboxShadowOffsetY = 2;
            State.DefaultTextboxShadowColor = "#000000";
            State.DefaultTextboxShadowAlpha = 120;
            State.DefaultTextboxPaddingX = 22;
            State.DefaultTextboxPaddingY = 18;

            State.ChoiceWidth = 620;
            State.ChoiceHeight = 60;
            State.ChoiceSpacing = 16;
            State.ChoiceFontSize = 30;
            State.ChoiceBgColor = "#202020";
            State.ChoiceBgAlpha = 240;
            State.ChoiceTextColor = "#ffffff";
            State.ChoiceCornerRadius = 6;
            State.ChoiceBorderColor = "#d1d5db";
            State.ChoiceBorderWidth = 2;
            State.ChoiceBorderOpacity = 120;
            State.ChoiceHoverColor = "#303030";
            State.ChoicePaddingX = 18;
            return;
        }

        if (themeName == "soft")
        {
            State.DefaultTextboxCornerRadius = 22;
            State.DefaultTextboxBorderWidth = 1;
            State.DefaultTextboxBorderColor = "#ffffff";
            State.DefaultTextboxBorderOpacity = 70;
            State.DefaultTextboxShadowOffsetX = 0;
            State.DefaultTextboxShadowOffsetY = 5;
            State.DefaultTextboxShadowColor = "#000000";
            State.DefaultTextboxShadowAlpha = 90;
            State.DefaultTextboxPaddingX = 30;
            State.DefaultTextboxPaddingY = 24;

            State.ChoiceWidth = 640;
            State.ChoiceHeight = 62;
            State.ChoiceSpacing = 18;
            State.ChoiceFontSize = 28;
            State.ChoiceBgColor = "#101010";
            State.ChoiceBgAlpha = 220;
            State.ChoiceTextColor = "#ffffff";
            State.ChoiceCornerRadius = 20;
            State.ChoiceBorderColor = "#ffffff";
            State.ChoiceBorderWidth = 1;
            State.ChoiceBorderOpacity = 70;
            State.ChoiceHoverColor = "#242424";
            State.ChoicePaddingX = 24;
            return;
        }

        if (themeName == "glass")
        {
            State.DefaultTextboxCornerRadius = 26;
            State.DefaultTextboxBorderWidth = 1;
            State.DefaultTextboxBorderColor = "#ffffff";
            State.DefaultTextboxBorderOpacity = 110;
            State.DefaultTextboxShadowOffsetX = 0;
            State.DefaultTextboxShadowOffsetY = 8;
            State.DefaultTextboxShadowColor = "#000000";
            State.DefaultTextboxShadowAlpha = 120;
            State.DefaultTextboxPaddingX = 34;
            State.DefaultTextboxPaddingY = 26;
            State.DefaultTextboxBgColor = "#050505";
            State.DefaultTextboxBgAlpha = 168;

            State.MenuFillColor = "#050505";
            State.MenuFillAlpha = 218;
            State.MenuLineColor = "#ffffff";
            State.MenuTextColor = "#ffffff";
            State.MenuCornerRadius = 24;
            State.ChoiceHoverColor = "#2b2b2b";
            return;
        }

        if (themeName == "mono")
        {
            State.MenuFillColor = "#000000";
            State.MenuFillAlpha = 238;
            State.MenuLineColor = "#ffffff";
            State.MenuTextColor = "#ffffff";
            State.MenuCornerRadius = 16;
        }

        // default: clean
        State.DefaultTextboxCornerRadius = UIThemeDefaults.TextboxCornerRadius;
        State.DefaultTextboxBorderWidth = UIThemeDefaults.TextboxBorderWidth;
        State.DefaultTextboxBorderColor = UIThemeDefaults.TextboxBorderColor;
        State.DefaultTextboxBorderOpacity = UIThemeDefaults.TextboxBorderOpacity;
        State.DefaultTextboxShadowOffsetX = UIThemeDefaults.TextboxShadowOffsetX;
        State.DefaultTextboxShadowOffsetY = UIThemeDefaults.TextboxShadowOffsetY;
        State.DefaultTextboxShadowColor = UIThemeDefaults.TextboxShadowColor;
        State.DefaultTextboxShadowAlpha = UIThemeDefaults.TextboxShadowAlpha;
        State.DefaultTextboxPaddingX = UIThemeDefaults.TextboxPaddingX;
        State.DefaultTextboxPaddingY = UIThemeDefaults.TextboxPaddingY;

        State.ChoiceWidth = UIThemeDefaults.ChoiceWidth;
        State.ChoiceHeight = UIThemeDefaults.ChoiceHeight;
        State.ChoiceSpacing = UIThemeDefaults.ChoiceSpacing;
        State.ChoiceFontSize = UIThemeDefaults.ChoiceFontSize;
        State.ChoiceBgColor = UIThemeDefaults.ChoiceBgColor;
        State.ChoiceBgAlpha = UIThemeDefaults.ChoiceBgAlpha;
        State.ChoiceTextColor = UIThemeDefaults.ChoiceTextColor;
        State.ChoiceCornerRadius = UIThemeDefaults.ChoiceCornerRadius;
        State.ChoiceBorderColor = UIThemeDefaults.ChoiceBorderColor;
        State.ChoiceBorderWidth = UIThemeDefaults.ChoiceBorderWidth;
        State.ChoiceBorderOpacity = UIThemeDefaults.ChoiceBorderOpacity;
        State.ChoiceHoverColor = UIThemeDefaults.ChoiceHoverColor;
        State.ChoicePaddingX = UIThemeDefaults.ChoicePaddingX;
    }

    internal void SetReg(string reg, int val)
    {
        string name = RegisterStoragePolicy.Normalize(reg);
        State.Registers[name] = val;
        if (RegisterStoragePolicy.IsPersistent(name))
        {
            MarkPersistentDirty();
        }
    }

    internal static string GetResultRegister(Instruction inst)
    {
        return inst.Arguments.Count > 1 ? inst.Arguments[1] : inst.Arguments[0];
    }
    internal int GetReg(string reg) => State.Registers.TryGetValue(reg.TrimStart('%').ToLowerInvariant(), out int v) ? v : 0;
    internal int GetVal(string valStr)
    {
        // If it starts with %, treat as register name
        if (valStr.StartsWith("%"))
        {
            string regName = valStr.Substring(1).ToLowerInvariant();
            return GetReg(regName);
        }
        // Otherwise, try to parse as integer
        if (int.TryParse(valStr, out int val)) return val;
        // If not integer, try as register name
        return GetReg(valStr);
    }

    internal float GetFloat(string valStr, Instruction? inst = null, float fallback = 0f)
    {
        if (valStr.StartsWith("%"))
        {
            return GetVal(valStr);
        }

        if (float.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            return value;
        }

        int regValue = GetReg(valStr);
        if (regValue != 0 || State.Registers.ContainsKey(RegisterStoragePolicy.Normalize(valStr)))
        {
            return regValue;
        }

        _reporter.Report(new AriaError(
            $"数値として解釈できない値 '{valStr}' を {fallback} として扱いました。",
            inst?.SourceLine ?? 0,
            _currentScriptFile,
            AriaErrorLevel.Warning,
            "VM_FLOAT_PARSE",
            hint: "scale/座標/時間などの数値引数に、数値または数値レジスタを指定してください。"));
        return fallback;
    }
    
    internal void SetStr(string reg, string val) => State.StringRegisters[reg.TrimStart('$').ToLowerInvariant()] = val;
    internal string GetString(string valStr)
    {
        if (valStr.StartsWith("$"))
        {
            string key = valStr.TrimStart('$').ToLowerInvariant();
            return State.StringRegisters.TryGetValue(key, out string? v) ? (v ?? "") : "";
        }
        return valStr;
    }

    internal bool IsOn(string token)
    {
        string value = GetString(token).ToLowerInvariant();
        if (value is "on" or "true" or "yes") return true;
        if (value is "off" or "false" or "no") return false;
        return GetVal(token) != 0;
    }

    internal void SetSystemButton(string name, bool visible)
    {
        switch (name.ToLowerInvariant())
        {
            case "close":
            case "end":
                State.ShowSystemCloseButton = visible;
                break;
            case "reset":
                State.ShowSystemResetButton = visible;
                break;
            case "skip":
                State.ShowSystemSkipButton = visible;
                break;
            case "save":
                State.ShowSystemSaveButton = visible;
                break;
            case "load":
                State.ShowSystemLoadButton = visible;
                break;
        }
    }

    public void JumpTo(string labelNameArg)
    {
        string label = labelNameArg.TrimStart('*');
        if (_labels.TryGetValue(label, out int pc)) State.ProgramCounter = pc;
        else
        {
            _reporter.Report(new AriaError(
                $"未定義のラベル '*{label}' へのジャンプをスキップしました。",
                0,
                _currentScriptFile,
                AriaErrorLevel.Error,
                "VM_LABEL_MISSING",
                hint: "goto/gosub/分岐先のラベル名が定義済みか確認してください。"));
        }
    }

    internal void ReturnFromSubroutine()
    {
        if (State.CallStack.Count > 0)
        {
            State.ProgramCounter = State.CallStack.Pop();
        }
        else if (State.ProgramCounter >= _instructions.Count)
        {
            State.State = VmState.Ended;
        }
    }

    /// <summary>
    /// サブルーチンを呼び出します。
    /// </summary>
    public void CallSub(string labelName)
    {
        string label = labelName.TrimStart('*');
        if (_labels.TryGetValue(label, out int pc))
        {
            State.CallStack.Push(State.ProgramCounter + 1);
            State.ProgramCounter = pc;
        }
        else
        {
            _reporter.Report(new AriaError(
                $"未定義のサブルーチン '*{label}' の呼び出しをスキップしました。",
                0,
                _currentScriptFile,
                AriaErrorLevel.Error,
                "VM_SUB_MISSING",
                hint: "呼び出し先ラベル、または defsub 宣言が存在するか確認してください。"));
        }
    }

    /// <summary>
    /// ゲームを終了します。
    /// </summary>
    public void QuitGame()
    {
        State.State = VmState.Ended;
        State.RequestClose = true;
    }

    public void ResetGame()
    {
        State.Sprites.Clear();
        State.SpriteButtonMap.Clear();
        State.CurrentTextBuffer = "";
        State.DisplayedTextLength = 0;
        State.State = VmState.Running;

        if (_labels.ContainsKey("start")) JumpTo("*start");
        else if (_labels.ContainsKey("title_start")) JumpTo("*title_start");
        else State.ProgramCounter = 0;
    }

    public void ToggleSkip()
    {
        State.SkipMode = !State.SkipMode;
    }

    public void StopSkip()
    {
        State.SkipMode = false;
        State.ForceSkipMode = false;
    }

    public int ProcessSkipFrame(int maxWaits = -1)
    {
        if (maxWaits < 0) maxWaits = State.ForceSkipMode ? State.ForceSkipAdvancePerFrame : State.SkipAdvancePerFrame;
        if ((!State.SkipMode && !State.ForceSkipMode) || maxWaits <= 0) return 0;

        int skippedWaits = 0;
        int guard = maxWaits * 8;
        while ((State.SkipMode || State.ForceSkipMode) && State.State != VmState.Ended && guard-- > 0)
        {
            if (State.State == VmState.WaitingForClick)
            {
                if (!CanSkipCurrentWait()) break;
                CompleteCurrentText();
                if (State.IsWaitingPageClear)
                {
                    State.CurrentTextBuffer = "";
                    State.DisplayedTextLength = 0;
                    State.IsWaitingPageClear = false;
                }
                ResumeFromClick();
                skippedWaits++;
                if (skippedWaits >= maxWaits) break;
                continue;
            }

            if (State.State == VmState.WaitingForAnimation)
            {
                if (State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
                {
                    CompleteCurrentText();
                    State.State = VmState.Running;
                    continue;
                }
                break;
            }

            if (State.State == VmState.WaitingForDelay)
            {
                State.DelayTimerMs = 0f;
                State.State = VmState.Running;
                continue;
            }

            if (State.State == VmState.Running)
            {
                int beforePc = State.ProgramCounter;
                Step();
                if (State.State == VmState.Running && State.ProgramCounter == beforePc) break;
                continue;
            }

            break;
        }

        return skippedWaits;
    }

    private bool CanSkipCurrentWait()
    {
        return State.ForceSkipMode || State.SkipUnread || State.CurrentInstructionWasRead;
    }

    private void CompleteCurrentText()
    {
        State.DisplayedTextLength = State.CurrentTextBuffer.Length;
        if (State.TextTargetSpriteId >= 0 &&
            State.Sprites.TryGetValue(State.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = State.CurrentTextBuffer;
        }
    }

    /// <summary>
    /// ゲームをセーブします。
    /// </summary>
    public void SaveGame(int slot)
    {
        NormalizeRuntimeTextSprites();
        Saves.Save(slot, State, _currentScriptFile);
    }

    public void AutoSaveGame()
    {
        SavePersistentState();
        SaveGame(SaveManager.AutoSaveSlot);
    }

    public bool LoadAutoSaveGame()
    {
        if (!Saves.HasSaveData(SaveManager.AutoSaveSlot)) return false;
        LoadGame(SaveManager.AutoSaveSlot);
        return true;
    }

    /// <summary>
    /// ゲームをロードします。
    /// </summary>
    public void LoadGame(int slot)
    {
        var (data, success) = Saves.Load(slot);
        if (success && data != null)
        {
            ApplyLoadedState(data);

            // "load_restore"ラベルが存在すればそちらに飛ぶことで初期化をスクリプトから行う
            if (_labels.ContainsKey("load_restore")) JumpTo("*load_restore");
            else
            {
                // Fallback: If no custom setup is provided, attempt to update the renderer if needed
            }
        }
        else
        {
            _reporter.Report(new AriaError($"Failed to load game from slot {slot}", -1, _currentScriptFile, AriaErrorLevel.Warning, "LOAD_FAILED"));
        }
    }

    private void ApplyLoadedState(SaveData data)
    {
        var loaded = data.State;

        State.ProgramCounter = loaded.ProgramCounter;
        State.State = loaded.State;
        State.CurrentScene = loaded.CurrentScene;

        var mergedRegisters = State.Registers
            .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in loaded.Registers)
        {
            if (RegisterStoragePolicy.IsSaveStored(pair.Key))
            {
                mergedRegisters[RegisterStoragePolicy.Normalize(pair.Key)] = pair.Value;
            }
        }
        State.Registers = mergedRegisters;

        State.StringRegisters = new Dictionary<string, string>(loaded.StringRegisters, StringComparer.OrdinalIgnoreCase);
        State.SaveFlags = new Dictionary<string, bool>(loaded.SaveFlags, StringComparer.OrdinalIgnoreCase);
        State.VolatileFlags = new Dictionary<string, bool>(loaded.VolatileFlags, StringComparer.OrdinalIgnoreCase);
        State.Counters = new Dictionary<string, int>(loaded.Counters, StringComparer.OrdinalIgnoreCase);

        State.Sprites = new Dictionary<int, Sprite>(loaded.Sprites);
        State.SpriteButtonMap = new Dictionary<int, int>(loaded.SpriteButtonMap);
        State.CallStack = new Stack<int>(loaded.CallStack.Reverse());
        State.ParamStack = new Stack<string>(loaded.ParamStack.Reverse());
        State.LoopStack = loaded.LoopStack != null ? new Stack<LoopState>(loaded.LoopStack.Reverse()) : new Stack<LoopState>();

        State.CurrentBgm = loaded.CurrentBgm;
        State.BgmVolume = loaded.BgmVolume;
        State.SeVolume = loaded.SeVolume;

        State.TextboxVisible = loaded.TextboxVisible;
        State.CurrentTextBuffer = loaded.CurrentTextBuffer;
        State.DisplayedTextLength = Math.Clamp(loaded.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
        State.TextAdvanceMode = loaded.TextAdvanceMode;
        State.TextAdvanceRatio = loaded.TextAdvanceRatio;
        State.TextTimerMs = 0f;
        State.IsWaitingPageClear = loaded.IsWaitingPageClear;
        State.TextHistory = new List<string>(loaded.TextHistory);
        State.TextHistoryStartNumber = Math.Max(1, loaded.TextHistoryStartNumber);
        State.TextTargetSpriteId = loaded.TextTargetSpriteId;
        State.TextboxBackgroundSpriteId = loaded.TextboxBackgroundSpriteId;
        State.UseManualTextLayout = loaded.UseManualTextLayout;
        State.CompatAutoUi = loaded.CompatAutoUi;

        State.DefaultTextboxX = loaded.DefaultTextboxX;
        State.DefaultTextboxY = loaded.DefaultTextboxY;
        State.DefaultTextboxW = loaded.DefaultTextboxW;
        State.DefaultTextboxH = loaded.DefaultTextboxH;
        State.DefaultFontSize = loaded.DefaultFontSize;
        State.DefaultTextColor = loaded.DefaultTextColor;
        State.DefaultTextboxBgColor = loaded.DefaultTextboxBgColor;
        State.DefaultTextboxBgAlpha = loaded.DefaultTextboxBgAlpha;
        State.DefaultTextboxPaddingX = loaded.DefaultTextboxPaddingX;
        State.DefaultTextboxPaddingY = loaded.DefaultTextboxPaddingY;
        State.DefaultTextboxCornerRadius = loaded.DefaultTextboxCornerRadius;
        State.DefaultTextboxBorderColor = loaded.DefaultTextboxBorderColor;
        State.DefaultTextboxBorderWidth = loaded.DefaultTextboxBorderWidth;
        State.DefaultTextboxBorderOpacity = loaded.DefaultTextboxBorderOpacity;
        State.DefaultTextboxShadowColor = loaded.DefaultTextboxShadowColor;
        State.DefaultTextboxShadowOffsetX = loaded.DefaultTextboxShadowOffsetX;
        State.DefaultTextboxShadowOffsetY = loaded.DefaultTextboxShadowOffsetY;
        State.DefaultTextboxShadowAlpha = loaded.DefaultTextboxShadowAlpha;
        State.ChoiceWidth = loaded.ChoiceWidth;
        State.ChoiceHeight = loaded.ChoiceHeight;
        State.ChoiceSpacing = loaded.ChoiceSpacing;
        State.ChoiceFontSize = loaded.ChoiceFontSize;
        State.ChoiceTextColor = loaded.ChoiceTextColor;
        State.ChoiceBgColor = loaded.ChoiceBgColor;
        State.ChoiceBgAlpha = loaded.ChoiceBgAlpha;
        State.ChoiceHoverColor = loaded.ChoiceHoverColor;
        State.ChoiceCornerRadius = loaded.ChoiceCornerRadius;
        State.ChoiceBorderColor = loaded.ChoiceBorderColor;
        State.ChoiceBorderWidth = loaded.ChoiceBorderWidth;
        State.ChoiceBorderOpacity = loaded.ChoiceBorderOpacity;
        State.ChoicePaddingX = loaded.ChoicePaddingX;
        State.FontFilter = loaded.FontFilter;
        State.TextSpeedMs = loaded.TextSpeedMs;
        State.DefaultTextShadowColor = loaded.DefaultTextShadowColor;
        State.DefaultTextShadowX = loaded.DefaultTextShadowX;
        State.DefaultTextShadowY = loaded.DefaultTextShadowY;
        State.DefaultTextOutlineColor = loaded.DefaultTextOutlineColor;
        State.DefaultTextOutlineSize = loaded.DefaultTextOutlineSize;
        State.DefaultTextEffect = loaded.DefaultTextEffect;
        State.DefaultTextEffectStrength = loaded.DefaultTextEffectStrength;
        State.DefaultTextEffectSpeed = loaded.DefaultTextEffectSpeed;
        State.SkipAdvancePerFrame = loaded.SkipAdvancePerFrame;
        State.ForceSkipAdvancePerFrame = loaded.ForceSkipAdvancePerFrame;
        State.ShowClickCursor = loaded.ShowClickCursor;
        State.ClickCursorMode = loaded.ClickCursorMode;
        State.ClickCursorPath = loaded.ClickCursorPath;
        State.ClickCursorOffsetX = loaded.ClickCursorOffsetX;
        State.ClickCursorOffsetY = loaded.ClickCursorOffsetY;
        State.ClickCursorSize = loaded.ClickCursorSize;
        State.ClickCursorColor = loaded.ClickCursorColor;
        State.RightMenuWidth = loaded.RightMenuWidth;
        State.RightMenuAlign = loaded.RightMenuAlign;
        State.SaveLoadColumns = loaded.SaveLoadColumns;
        State.SaveLoadWidth = loaded.SaveLoadWidth;
        State.BacklogWidth = loaded.BacklogWidth;
        State.SettingsWidth = loaded.SettingsWidth;
        State.MenuFillColor = loaded.MenuFillColor;
        State.MenuFillAlpha = loaded.MenuFillAlpha;
        State.MenuTextColor = loaded.MenuTextColor;
        State.MenuLineColor = loaded.MenuLineColor;
        State.MenuCornerRadius = loaded.MenuCornerRadius;
        State.UiGroups = loaded.UiGroups.ToDictionary(pair => pair.Key, pair => new List<int>(pair.Value));
        State.UiLayouts = new Dictionary<int, string>(loaded.UiLayouts);
        State.UiAnchors = new Dictionary<int, string>(loaded.UiAnchors);
        State.UiEvents = new Dictionary<string, string>(loaded.UiEvents, StringComparer.OrdinalIgnoreCase);
        State.UiHotkeys = new Dictionary<string, string>(loaded.UiHotkeys, StringComparer.OrdinalIgnoreCase);
        State.UiHoverActive = new HashSet<int>();

        State.CurrentChapter = loaded.CurrentChapter;
        _currentScriptFile = data.ScriptFile;

        NormalizeLoadedUiState();
    }

    private void NormalizeLoadedUiState()
    {
        if (State.TextboxBackgroundSpriteId >= 0 && !State.Sprites.ContainsKey(State.TextboxBackgroundSpriteId))
        {
            State.TextboxBackgroundSpriteId = -1;
        }

        if (State.TextTargetSpriteId >= 0 && !State.Sprites.ContainsKey(State.TextTargetSpriteId))
        {
            State.TextTargetSpriteId = -1;
        }

        foreach (var sprite in State.Sprites.Values)
        {
            sprite.IsHovered = false;
        }

        State.SpriteButtonMap = State.SpriteButtonMap
            .Where(pair => State.Sprites.TryGetValue(pair.Key, out var sprite) && sprite.IsButton)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        NormalizeRuntimeTextSprites();

        _activeCompatUiSpriteIds.Clear();
        foreach (var sprite in State.Sprites.Values)
        {
            if (IsTransientCompatUiSprite(sprite))
            {
                _activeCompatUiSpriteIds.Add(sprite.Id);
            }
        }

        _nextCompatUiSpriteId = Math.Max(50000, State.Sprites.Count == 0 ? 50000 : State.Sprites.Keys.Max() + 1);
    }

    private static bool IsTransientCompatUiSprite(Sprite sprite)
    {
        return sprite.Id >= 50000 && sprite.Z >= 9500;
    }

    private void NormalizeRuntimeTextSprites()
    {
        if (!State.CompatAutoUi || State.UseManualTextLayout) return;

        if (State.TextboxBackgroundSpriteId >= 0 &&
            State.Sprites.TryGetValue(State.TextboxBackgroundSpriteId, out var bg) &&
            bg.Type == SpriteType.Rect)
        {
            bg.X = State.DefaultTextboxX;
            bg.Y = State.DefaultTextboxY;
            bg.Width = State.DefaultTextboxW;
            bg.Height = State.DefaultTextboxH;
            bg.FillColor = State.DefaultTextboxBgColor;
            bg.FillAlpha = State.DefaultTextboxBgAlpha;
            bg.CornerRadius = State.DefaultTextboxCornerRadius;
            bg.BorderColor = State.DefaultTextboxBorderColor;
            bg.BorderWidth = State.DefaultTextboxBorderWidth;
            bg.BorderOpacity = State.DefaultTextboxBorderOpacity;
            bg.ShadowColor = State.DefaultTextboxShadowColor;
            bg.ShadowOffsetX = State.DefaultTextboxShadowOffsetX;
            bg.ShadowOffsetY = State.DefaultTextboxShadowOffsetY;
            bg.ShadowAlpha = State.DefaultTextboxShadowAlpha;
            bg.Visible = State.TextboxVisible;
            bg.Z = 9000;
        }

        if (State.TextTargetSpriteId >= 0 &&
            State.Sprites.TryGetValue(State.TextTargetSpriteId, out var text) &&
            text.Type == SpriteType.Text)
        {
            text.X = State.DefaultTextboxX + State.DefaultTextboxPaddingX;
            text.Y = State.DefaultTextboxY + State.DefaultTextboxPaddingY;
            text.Width = Math.Max(0, State.DefaultTextboxW - (State.DefaultTextboxPaddingX * 2));
            text.Height = Math.Max(0, State.DefaultTextboxH - (State.DefaultTextboxPaddingY * 2));
            text.FontSize = State.DefaultFontSize;
            text.Color = State.DefaultTextColor;
            text.TextShadowColor = State.DefaultTextShadowColor;
            text.TextShadowX = State.DefaultTextShadowX;
            text.TextShadowY = State.DefaultTextShadowY;
            text.TextOutlineColor = State.DefaultTextOutlineColor;
            text.TextOutlineSize = State.DefaultTextOutlineSize;
            text.TextEffect = State.DefaultTextEffect;
            text.TextEffectStrength = State.DefaultTextEffectStrength;
            text.TextEffectSpeed = State.DefaultTextEffectSpeed;
            text.Visible = State.TextboxVisible;
            text.Z = 9001;
            int length = Math.Clamp(State.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
            text.Text = State.CurrentTextBuffer.Substring(0, length);
        }
    }
}





