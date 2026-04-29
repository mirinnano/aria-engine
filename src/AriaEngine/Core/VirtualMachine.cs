using System;
using System.Collections.Generic;
using System.Globalization;
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
    private Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
    public GameState State { get; set; }
    private readonly ErrorReporter _reporter;
    private string _currentScriptFile = "";
    private string _currentReadKeyPrefix = "";
    internal ErrorReporter Reporter => _reporter;
    public string CurrentScriptFile => _currentScriptFile;
    public IReadOnlyDictionary<string, int> Labels => _labels;
    public IReadOnlyList<Instruction> Instructions => _instructions;

    public TweenManager Tweens { get; private set; }
    public SaveManager Saves { get; private set; }
    public ConfigManager Config { get; private set; }
    public MenuSystem Menu { get; private set; }
    public SpritePool SpritePool { get; private set; }
    public Audio.AudioManager? Audio { get; set; }

    // マネージャー
    public UiThemeManager UiThemeManager { get; private set; }
    public CompatUiManager CompatUiManager { get; private set; }
    public SkipModeManager SkipModeManager { get; private set; }
    public SaveStateNormalizer SaveStateNormalizer { get; private set; }

    // Scope management helpers (for T5)
    internal void EnterScope()
    {
        // Push new local scopes for ints/strings
        var intScope = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var strScope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        State.LocalIntStacks.Push(intScope);
        State.LocalStringStacks.Push(strScope);
        // Push lifetime tracker for sprites created in this scope
        State.SpriteLifetimeStacks.Push(new HashSet<int>());
        // Create scope frame and link to current local dictionaries
        var frame = new ScopeFrame
        {
            LocalInt = intScope,
            LocalString = strScope,
            SpriteIds = new HashSet<int>(),
            Defer = new List<Instruction>()
        };
        State.Execution.ScopeStack.Push(frame);
    }

    internal void ExitScopesUntil(int targetDepth)
    {
        while (State.Execution.ScopeStack.Count > targetDepth)
        {
            // pop one scope and cleanup
            var frame = State.Execution.ScopeStack.Pop();
            // Execute deferred instructions in LIFO order
            for (int i = frame.Defer.Count - 1; i >= 0; i--)
            {
                var defInst = frame.Defer[i];
                // Execute the deferred instruction. If one throws, continue with the rest.
                try
                {
                    ExecuteInstruction(defInst);
                }
                catch (Exception ex)
                {
                    // Log error but continue executing remaining defers
                    Reporter.Report(new AriaError(
                        $"Deferred instruction threw an exception: {ex.Message}",
                        defInst.SourceLine, CurrentScriptFile, AriaErrorLevel.Warning, "VM_DEFER_THROW"));
                }
            }
            frame.Defer.Clear();

            // Remove sprites created in this scope
            if (State.SpriteLifetimeStacks.Count > 0)
            {
                var lifetimeSet = State.SpriteLifetimeStacks.Pop();
                foreach (var sid in lifetimeSet)
                {
                    State.Sprites.Remove(sid);
                }
            }

            // Pop local scope dictionaries
            if (State.LocalIntStacks.Count > 0) State.LocalIntStacks.Pop();
            if (State.LocalStringStacks.Count > 0) State.LocalStringStacks.Pop();
        }
    }

    // パフォーマンス最適化: 共通オブジェクトのキャッシュ
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private Dictionary<string, string> _characterPaths = new();
    private bool _persistentDirty;
    private float _persistentSaveTimerMs;
    private readonly List<ICommandHandler> _commandHandlers;
    private readonly ICommandHandler?[] _handlerTable;

    // レジスタ高速化: %0-%9 を固定配列でキャッシュ
    private readonly int[] _fastRegisters = new int[10];

    // 文字列リテラルキャッシュ（intern）
    private readonly Dictionary<string, string> _stringCache = new();

    // 動的 include
    private readonly HashSet<string> _includedFiles = new(StringComparer.OrdinalIgnoreCase);
    private Func<string, ParseResult?>? _includeResolver;

    // VMステップ制限（SkipMode時は解除）
    private const int MaxInstructionsPerFrame = 500;

    // T20: ラベルアドレスセット（チャプターラベル検出用）
    private HashSet<int> _labelAddresses = new();

    public VirtualMachine(ErrorReporter reporter, TweenManager tweens, SaveManager saves, ConfigManager config)
    {
        _reporter = reporter;
        Tweens = tweens;
        Saves = saves;
        Config = config;
        State = new GameState();
        Menu = new MenuSystem(this);
        SpritePool = new SpritePool(CacheConstants.SpritePoolDefaultSize);

        // マネージャーを初期化
        UiThemeManager = new UiThemeManager(State);
        CompatUiManager = new CompatUiManager(State);
        SkipModeManager = new SkipModeManager(State, this);
        SaveStateNormalizer = new SaveStateNormalizer(State, reporter);

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
        _handlerTable = new ICommandHandler?[(int)OpCode.FontFilter + 1];
        foreach (var handler in _commandHandlers)
        {
            foreach (var code in handler.HandledCodes)
            {
                int index = (int)code;
                if (index >= 0 && index < _handlerTable.Length)
                    _handlerTable[index] = handler;
            }
        }

        // Load initial config into GameState
        State.TextSpeedMs = Config.Config.GlobalTextSpeedMs;
        State.BgmVolume = Config.Config.BgmVolume;
        State.SeVolume = Config.Config.SeVolume;
        State.AutoModeWaitTimeMs = Config.Config.AutoModeWaitTimeMs;
        var persistent = Config.LoadPersistentGameData();
        State.SkipUnread = persistent.SkipUnread;
        State.Registers = new Dictionary<string, int>(persistent.Registers, StringComparer.OrdinalIgnoreCase);
        State.Flags = new Dictionary<string, bool>(persistent.Flags, StringComparer.OrdinalIgnoreCase);
        State.SaveFlags = new Dictionary<string, bool>(persistent.SaveFlags, StringComparer.OrdinalIgnoreCase);
        State.Counters = new Dictionary<string, int>(persistent.Counters, StringComparer.OrdinalIgnoreCase);
        State.ReadKeys = new HashSet<string>(persistent.ReadKeys, StringComparer.OrdinalIgnoreCase);
        State.UnlockedCgs = new HashSet<string>(persistent.UnlockedCgs, StringComparer.OrdinalIgnoreCase);

        // Initialize new managers
        ChapterManager = new ChapterManager(reporter);
        ChapterManager.LoadChapters();

        GameFlow = new GameFlowManager();
        GameFlow.Initialize();

        CharacterManager = new CharacterManager(State, tweens, reporter);
        CharacterManager.LoadCharacterData();
        
        FunctionTable = new FunctionTable();
        StructManager = new StructManager();
        NamespaceManager = new NamespaceManager();
        VersionManager = new VersionManager();
        AriaCheck = new AriaCheck(reporter, VersionManager);
    }

    public ChapterManager ChapterManager { get; private set; }
    public GameFlowManager GameFlow { get; private set; }
    public CharacterManager CharacterManager { get; private set; }
    
    // C++的現代構文マネージャー
    public FunctionTable FunctionTable { get; private set; }
    public StructManager StructManager { get; private set; }
    public NamespaceManager NamespaceManager { get; private set; }
    
    // バージョン管理・診断
    public VersionManager VersionManager { get; private set; }
    public AriaCheck AriaCheck { get; private set; }

    public void LoadScript(ParseResult result, string file)
    {
        _instructions = result.Instructions;
        _labels = result.Labels;
        _labelAddresses = new HashSet<int>(_labels.Values);
        _currentScriptFile = file;
        _currentReadKeyPrefix = file + ":";
        State.ProgramCounter = 0;
        State.State = VmState.Running;
        State.TotalScriptLines = result.SourceLines.Length;
        
        // Register functions and structs from parse result
        foreach (var func in result.Functions)
        {
            func.EntryPC = _labels.GetValueOrDefault(func.QualifiedName, -1);
            FunctionTable.Register(func);
        }
        
        foreach (var st in result.Structs)
        {
            StructManager.RegisterDefinition(st);
        }

        foreach (var en in result.Enums)
        {
            FunctionTable.RegisterEnum(en);
        }

        // T13: Load owned sprite declarations into GameState for lifetime tracking
        State.OwnedSprites = new HashSet<string>(result.OwnedSprites, StringComparer.OrdinalIgnoreCase);

        // Run compatibility check
        AriaCheck.CheckScript(result.SourceLines, file);
        AriaCheck.CheckCompatibility();
    }

    /// <summary>
    /// 追加スクリプトを現在の命令リストにマージする。
    /// 別ファイルのラベルを呼び出せるようにするための実行時 include。
    /// </summary>
    public void AppendScript(ParseResult result, string file)
    {
        int offset = _instructions.Count;

        foreach (var inst in result.Instructions)
        {
            var shifted = new Instruction(inst.Op, inst.Arguments, inst.SourceLine, inst.Condition)
            {
                ScriptFile = file
            };
            _instructions.Add(shifted);
        }

        foreach (var pair in result.Labels)
        {
            string key = pair.Key;
            if (!_labels.ContainsKey(key))
            {
                _labels[key] = pair.Value + offset;
            }
        }

        foreach (var func in result.Functions)
        {
            func.EntryPC = _labels.GetValueOrDefault(func.QualifiedName, -1);
            FunctionTable.Register(func);
        }

        foreach (var st in result.Structs)
        {
            StructManager.RegisterDefinition(st);
        }

        foreach (var en in result.Enums)
        {
            FunctionTable.RegisterEnum(en);
        }

        _labelAddresses = new HashSet<int>(_labels.Values);
    }
    
    [Obsolete("Use LoadScript(ParseResult, string) instead")]
    public void LoadScript(List<Instruction> instructions, Dictionary<string, int> labels, string file)
    {
        _instructions = instructions;
        _labels = labels;
        _currentScriptFile = file;
        _currentReadKeyPrefix = file + ":";
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
            AutoSaveGame();
            if (State.UiEvents.TryGetValue($"{buttonId}:click", out string? label) ||
                State.UiEvents.TryGetValue($"{resultValue}:click", out label))
            {
                JumpTo(label);
            }
            State.ButtonTimeoutMs = 0;
            State.ButtonTimer = 0f;
            State.ButtonResultRegister = "0";
            State.FocusedButtonId = -1;
            State.UiHoverActive.Clear();
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
            State.FocusedButtonId = -1;
            State.UiHoverActive.Clear();
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
        var state = State; // ローカルキャッシュ
        int maxSteps = (state.SkipMode || state.ForceSkipMode) ? int.MaxValue : MaxInstructionsPerFrame;
        int executed = 0;
        
        while (state.State == VmState.Running && executed < maxSteps)
        {
            if (state.ProgramCounter < 0 || state.ProgramCounter > _instructions.Count)
            {
                string scriptFile = (state.ProgramCounter >= 0 && state.ProgramCounter < _instructions.Count)
                    ? _instructions[state.ProgramCounter].ScriptFile
                    : _currentScriptFile;
                _reporter.Report(new AriaError(
                    $"プログラムカウンタが範囲外です (PC={state.ProgramCounter}, 命令数={_instructions.Count})。スクリプト実行を終了します。",
                    0, scriptFile, AriaErrorLevel.Error, "VM_PC_OUT_OF_BOUNDS"));
                state.State = VmState.Ended;
                break;
            }
            if (state.ProgramCounter >= _instructions.Count) break;

            // T20: Autosave at chapter label start
            if (_labelAddresses.Contains(state.ProgramCounter))
            {
                if (TryGetCurrentLabelAndOffset(out string labelName, out int offset) && offset == 0 &&
                    labelName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase))
                {
                    AutoSaveGame();
                }
            }

            var inst = _instructions[state.ProgramCounter];
            state.ProgramCounter++;
            state.CurrentInstructionWasRead = IsInstructionRead(inst);
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
                string scriptFile = !string.IsNullOrEmpty(inst.ScriptFile) ? inst.ScriptFile : _currentScriptFile;
                _reporter.Report(new AriaError($"実行時エラー: {ex.Message}", inst.SourceLine, scriptFile, AriaErrorLevel.Error));
            }
            
            executed++;
        }

        if (state.State == VmState.Running && state.ProgramCounter >= _instructions.Count)
        {
            state.State = VmState.Ended;
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
                if (!Tweens.IsAnimating)
                {
                    State.State = VmState.Running; // Resume script
                }
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

    internal bool EvaluateCondition(Condition condition)
    {
        if (condition.IsEmpty) return true;

        // 新しい式システム: Expression ASTを評価
        if (condition.Expression != null)
        {
            return condition.Expression.EvaluateInt(State, this) != 0;
        }

        // フォールバック: 従来のConditionTerm方式
        foreach (var term in condition.Terms)
        {
            if (term.IsAndConnector) continue;

            if (term.Op == "truthy")
            {
                if (GetVal(term.Lhs) == 0) return false;
                continue;
            }

            int lhs = GetVal(term.Lhs);
            int rhs = GetVal(term.Rhs);
            bool result = false;
            switch (term.Op)
            {
                case "==": result = lhs == rhs; break;
                case "!=": result = lhs != rhs; break;
                case ">":  result = lhs > rhs; break;
                case "<":  result = lhs < rhs; break;
                case ">=": result = lhs >= rhs; break;
                case "<=": result = lhs <= rhs; break;
            }
            if (!result) return false;
        }
        return true;
    }

    /// <summary>
    /// 旧式条件評価（移行期間用）
    /// </summary>
    internal bool EvaluateCondition(IReadOnlyList<string>? condTokens)
    {
        if (condTokens == null || condTokens.Count == 0) return true;
        return EvaluateCondition(Condition.FromTokens(condTokens));
    }

    private void ExecuteInstruction(Instruction inst)
    {
        int index = (int)inst.Op;
        if (index >= 0 && index < _handlerTable.Length && _handlerTable[index] is { } handler)
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
        if (State.ReadKeys.Add(_currentReadKeyPrefix + inst.SourceLine))
        {
            MarkPersistentDirty();
        }
    }

    private bool IsInstructionRead(Instruction inst)
    {
        return State.ReadKeys.Contains(_currentReadKeyPrefix + inst.SourceLine);
    }

    internal void MarkPersistentDirty()
    {
        _persistentDirty = true;
    }

    public void SavePersistentState()
    {
        Config.Config.GlobalTextSpeedMs = State.TextSpeedMs;
        Config.Config.BgmVolume = State.BgmVolume;
        Config.Config.SeVolume = State.SeVolume;
        Config.Config.SkipUnread = State.SkipUnread;
        Config.Config.AutoModeWaitTimeMs = State.AutoModeWaitTimeMs;
        Config.SavePersistentGameData(new PersistentGameData
        {
            SkipUnread = State.SkipUnread,
            Registers = State.Registers
                .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Flags = new Dictionary<string, bool>(State.Flags, StringComparer.OrdinalIgnoreCase),
            SaveFlags = new Dictionary<string, bool>(State.SaveFlags, StringComparer.OrdinalIgnoreCase),
            Counters = new Dictionary<string, int>(State.Counters, StringComparer.OrdinalIgnoreCase),
            ReadKeys = State.ReadKeys.ToList(),
            UnlockedCgs = State.UnlockedCgs.ToList()
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
        if (State.TextHistory.Count == 0 || State.TextHistory[^1].Text != text)
        {
            var entry = new BacklogEntry
            {
                Text = text,
                VoicePath = string.IsNullOrEmpty(State.LastVoicePath) ? null : State.LastVoicePath,
                ProgramCounter = State.ProgramCounter,
                IsRead = false,
                Timestamp = DateTime.Now,
                StateSnapshot = CaptureBacklogSnapshot()
            };
            State.TextHistory.Add(entry);
            State.LastVoicePath = "";
            const int maxTextHistory = 300;
            if (State.TextHistory.Count > maxTextHistory)
            {
                int removeCount = State.TextHistory.Count - maxTextHistory;
                State.TextHistory.RemoveRange(0, removeCount);
                State.TextHistoryStartNumber += removeCount;
            }
        }
    }

    private BacklogStateSnapshot CaptureBacklogSnapshot()
    {
        var sprites = new FastSpriteDictionary();
        foreach (var kvp in State.Sprites)
        {
            sprites[kvp.Key] = new Sprite
            {
                Id = kvp.Value.Id,
                Type = kvp.Value.Type,
                X = kvp.Value.X,
                Y = kvp.Value.Y,
                Z = kvp.Value.Z,
                Visible = kvp.Value.Visible,
                Opacity = kvp.Value.Opacity,
                ScaleX = kvp.Value.ScaleX,
                ScaleY = kvp.Value.ScaleY,
                Rotation = kvp.Value.Rotation,
                ImagePath = kvp.Value.ImagePath,
                Text = kvp.Value.Text,
                FontSize = kvp.Value.FontSize,
                Color = kvp.Value.Color,
                TextAlign = kvp.Value.TextAlign,
                TextVAlign = kvp.Value.TextVAlign,
                TextShadowColor = kvp.Value.TextShadowColor,
                TextShadowX = kvp.Value.TextShadowX,
                TextShadowY = kvp.Value.TextShadowY,
                TextOutlineColor = kvp.Value.TextOutlineColor,
                TextOutlineSize = kvp.Value.TextOutlineSize,
                TextEffect = kvp.Value.TextEffect,
                TextEffectStrength = kvp.Value.TextEffectStrength,
                TextEffectSpeed = kvp.Value.TextEffectSpeed,
                Width = kvp.Value.Width,
                Height = kvp.Value.Height,
                FillColor = kvp.Value.FillColor,
                FillAlpha = kvp.Value.FillAlpha,
                CornerRadius = kvp.Value.CornerRadius,
                BorderColor = kvp.Value.BorderColor,
                BorderWidth = kvp.Value.BorderWidth,
                BorderOpacity = kvp.Value.BorderOpacity,
                GradientTo = kvp.Value.GradientTo,
                GradientDirection = kvp.Value.GradientDirection,
                ShadowColor = kvp.Value.ShadowColor,
                ShadowOffsetX = kvp.Value.ShadowOffsetX,
                ShadowOffsetY = kvp.Value.ShadowOffsetY,
                ShadowAlpha = kvp.Value.ShadowAlpha,
                IsButton = kvp.Value.IsButton,
                ClickAreaX = kvp.Value.ClickAreaX,
                ClickAreaY = kvp.Value.ClickAreaY,
                ClickAreaW = kvp.Value.ClickAreaW,
                ClickAreaH = kvp.Value.ClickAreaH,
                HoverFillColor = kvp.Value.HoverFillColor,
                HoverScale = kvp.Value.HoverScale,
                IsHovered = false,
                Cursor = kvp.Value.Cursor,
                SliderMin = kvp.Value.SliderMin,
                SliderMax = kvp.Value.SliderMax
            };
        }

        return new BacklogStateSnapshot
        {
            Registers = new Dictionary<string, int>(State.Registers, StringComparer.OrdinalIgnoreCase),
            StringRegisters = new Dictionary<string, string>(State.StringRegisters, StringComparer.OrdinalIgnoreCase),
            Flags = new Dictionary<string, bool>(State.Flags),
            SaveFlags = new Dictionary<string, bool>(State.SaveFlags),
            Counters = new Dictionary<string, int>(State.Counters, StringComparer.OrdinalIgnoreCase),
            Sprites = sprites,
            CurrentBgm = State.CurrentBgm,
            BgmVolume = State.BgmVolume,
            SeVolume = State.SeVolume
        };
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
        return CompatUiManager.AllocateSpriteId();
    }

    internal void TrackCompatUiSprite(int spriteId)
    {
        CompatUiManager.TrackSprite(spriteId);
    }

    internal void ClearCompatUiSprites()
    {
        CompatUiManager.ClearAllSprites();
    }

    internal bool HasAnyVisibleButton()
    {
        return CompatUiManager.HasAnyVisibleButton();
    }

    internal void ApplyUiTheme(string themeName)
    {
        UiThemeManager.ApplyTheme(themeName);
    }

    internal void SetReg(string reg, int val)
    {
        string name = RegisterStoragePolicy.Normalize(reg);
        
        // ref マッピングを解決
        if (State.CurrentRefMap.TryGetValue(name, out string? originalReg))
        {
            name = RegisterStoragePolicy.Normalize(originalReg);
            SetGlobalReg(name, val);
            return;
        }
        
        // ローカルスコープ優先
        if (State.LocalIntStacks.Count > 0)
        {
            State.LocalIntStacks.Peek()[name] = val;
            return;
        }
        
        // 高速パス: %0-%9
        if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
        {
            _fastRegisters[name[0] - '0'] = val;
        }
        
        State.Registers[name] = val;
        if (RegisterStoragePolicy.IsPersistent(name))
        {
            MarkPersistentDirty();
        }
    }

    private void SetGlobalReg(string name, int val)
    {
        if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
        {
            _fastRegisters[name[0] - '0'] = val;
        }

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
    
    internal int GetReg(string reg)
    {
        string normalized = reg.TrimStart('%');
        
        // ref マッピングを解決
        if (State.CurrentRefMap.TryGetValue(normalized, out string? originalReg))
        {
            normalized = RegisterStoragePolicy.Normalize(originalReg);
        }
        
        // ローカルスコープ優先
        if (State.LocalIntStacks.Count > 0 && State.LocalIntStacks.Peek().TryGetValue(normalized, out int localVal))
        {
            return localVal;
        }
        
        // 高速パス: %0-%9
        if (normalized.Length == 1 && normalized[0] >= '0' && normalized[0] <= '9')
        {
            return _fastRegisters[normalized[0] - '0'];
        }
        
        return State.Registers.TryGetValue(normalized, out int v) ? v : 0;
    }
    
    internal int GetVal(string valStr)
    {
        if (string.IsNullOrEmpty(valStr)) return 0;
        
        char first = valStr[0];
        
        // 高速パス: %0-%9
        if (first == '%' && valStr.Length == 2 && valStr[1] >= '0' && valStr[1] <= '9')
        {
            return _fastRegisters[valStr[1] - '0'];
        }
        
        // %10以上または名前付きレジスタ
        if (first == '%')
        {
            string regName = valStr.Substring(1);
            return GetReg(regName);
        }
        
        // 数値リテラル
        if ((first >= '0' && first <= '9') || first == '-')
        {
            if (int.TryParse(valStr, out int val)) return val;
        }
        
        // フォールバック: レジスタ名として解釈
        return GetReg(valStr);
    }

    /// <summary>
    /// Capture a literal argument value at the time a defer is defined.
    /// - If the argument starts with '%', evaluate its current integer value and return as string.
    /// - If the argument starts with '$', capture the current string value.
    /// - Otherwise, return the argument as-is (literal).
    /// </summary>
    internal string CaptureLiteralArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return arg;
        if (arg.StartsWith("%"))
        {
            return GetVal(arg).ToString();
        }
        if (arg.StartsWith("$"))
        {
            return GetString(arg);
        }
        return arg;
    }

    internal float GetFloat(string valStr, Instruction? inst = null, float fallback = 0f)
    {
        if (valStr.StartsWith("%"))
        {
            return GetVal(valStr);
        }

        if (float.TryParse(valStr, NumberStyles.Float, InvariantCulture, out float value))
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
    
    private static readonly System.Text.RegularExpressions.Regex InterpolationRegex = new(
        @"\$\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    internal void SetStr(string reg, string val)
    {
        string key = reg.TrimStart('$');
        if (State.LocalStringStacks.Count > 0)
        {
            State.LocalStringStacks.Peek()[key] = val;
            return;
        }
        State.StringRegisters[key] = val;
    }

    // Public API to set a global (bypass local scopes) integer register
    public void SetGlobalRegister(string reg, int value)
    {
        string name = RegisterStoragePolicy.Normalize(reg);
        // Respect ref aliasing similar to SetReg
        if (State.CurrentRefMap.TryGetValue(name, out string? originalReg))
        {
            name = RegisterStoragePolicy.Normalize(originalReg);
        }
        // Write directly to global registers (bypass LocalIntStacks)
        State.Registers[name] = value;
        if (RegisterStoragePolicy.IsPersistent(name))
        {
            MarkPersistentDirty();
        }
    }

    // Public API to set a global string register
    public void SetGlobalString(string reg, string value)
    {
        string key = reg.TrimStart('$');
        if (State.LocalStringStacks.Count > 0)
        {
            State.LocalStringStacks.Peek()[key] = value;
            return;
        }
        State.StringRegisters[key] = value;
    }

    /// <summary>
    /// 文字列を解決。$レジスタ、文字列補間 ${...} に対応
    /// </summary>
    internal string GetString(string valStr)
    {
        if (valStr.StartsWith("$"))
        {
            string key = valStr.TrimStart('$');
            if (State.LocalStringStacks.Count > 0 && State.LocalStringStacks.Peek().TryGetValue(key, out string? localVal))
            {
                return localVal ?? "";
            }
            return State.StringRegisters.TryGetValue(key, out string? v) ? (v ?? "") : "";
        }

        // 文字列補間 ${...} の処理
        if (valStr.Contains("${"))
        {
            return InterpolationRegex.Replace(valStr, m =>
            {
                string inner = m.Groups[1].Value.Trim();

                // ${$name} → 文字列レジスタ
                if (inner.StartsWith("$"))
                {
                    string key = inner.TrimStart('$');
                    if (State.LocalStringStacks.Count > 0 && State.LocalStringStacks.Peek().TryGetValue(key, out string? localValue))
                    {
                        return localValue ?? "";
                    }
                    return State.StringRegisters.TryGetValue(key, out string? sv) ? (sv ?? "") : "";
                }

                // ${%name} または ${%0} → 整数レジスタを文字列化
                if (inner.StartsWith("%"))
                {
                    // 単純なレジスタ名か確認
                    string regName = inner.TrimStart('%');
                    if (!regName.Contains(' ') && !regName.Contains('+') && !regName.Contains('-'))
                    {
                        return GetReg(inner).ToString();
                    }
                }

                // ${expression} → 式を評価
                var tokens = TokenizeForExpression(inner);
                var expr = ExpressionParser.TryParse(tokens);
                if (expr != null)
                {
                    return expr.EvaluateString(State, this);
                }

                return m.Value; // パース失敗時はそのまま
            });
        }

        // 文字列リテラルをキャッシュ（intern）
        if (!_stringCache.TryGetValue(valStr, out string? cached))
        {
            cached = valStr;
            _stringCache[valStr] = cached;
        }
        return cached;
    }

    /// <summary>
    /// 文字列補間内の式用に簡易トークン化
    /// </summary>
    private static List<string> TokenizeForExpression(string text)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i])) { i++; continue; }

            if (text[i] == '"')
            {
                i++;
                int start = i;
                while (i < text.Length && text[i] != '"') i++;
                tokens.Add(text.Substring(start, i - start));
                if (i < text.Length) i++;
                continue;
            }

            // 2文字演算子
            if (i + 1 < text.Length)
            {
                string two = text.Substring(i, 2);
                if (two is "==" or "!=" or ">=" or "<=" or "&&" or "||")
                {
                    tokens.Add(two);
                    i += 2;
                    continue;
                }
            }

            // 1文字演算子・記号
            char c = text[i];
            if (c is '+' or '-' or '*' or '/' or '%' or '(' or ')' or '[' or ']' or '>' or '<' or '=' or '!')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // オペランド（レジスタ名、数値、識別子）
            int start2 = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) &&
                   text[i] is not '+' and not '-' and not '*' and not '/' and not '%' and
                   not '(' and not ')' and not '[' and not ']' and not '"')
            {
                i++;
            }
            tokens.Add(text.Substring(start2, i - start2));
        }
        return tokens;
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

    /// <summary>
    /// 現在のPC位置から、所属するラベル名とラベル先頭からのオフセットを取得する。
    /// </summary>
    public bool TryGetCurrentLabelAndOffset(out string labelName, out int offset)
    {
        int pc = State.ProgramCounter;
        labelName = "";
        offset = 0;
        int bestPc = -1;

        foreach (var pair in _labels)
        {
            if (pair.Value <= pc && pair.Value > bestPc)
            {
                bestPc = pair.Value;
                labelName = pair.Key;
            }
        }

        if (bestPc < 0) return false;
        offset = pc - bestPc;
        return true;
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

    /// <summary>
    /// Jumps back to a specific backlog entry, restoring its captured state.
    /// </summary>
    public void JumpToBacklogEntry(BacklogEntry entry)
    {
        if (entry.StateSnapshot != null)
        {
            State.Registers = new Dictionary<string, int>(entry.StateSnapshot.Registers, StringComparer.OrdinalIgnoreCase);
            State.StringRegisters = new Dictionary<string, string>(entry.StateSnapshot.StringRegisters, StringComparer.OrdinalIgnoreCase);
            State.Flags = new Dictionary<string, bool>(entry.StateSnapshot.Flags);
            State.SaveFlags = new Dictionary<string, bool>(entry.StateSnapshot.SaveFlags);
            State.Counters = new Dictionary<string, int>(entry.StateSnapshot.Counters, StringComparer.OrdinalIgnoreCase);
            State.Sprites = entry.StateSnapshot.Sprites;
            State.CurrentBgm = entry.StateSnapshot.CurrentBgm;
            State.BgmVolume = entry.StateSnapshot.BgmVolume;
            State.SeVolume = entry.StateSnapshot.SeVolume;
        }

        State.ProgramCounter = entry.ProgramCounter;
        State.CurrentTextBuffer = entry.Text;
        State.DisplayedTextLength = entry.Text.Length;
        State.IsWaitingPageClear = false;
        State.State = VmState.Running;
        Menu.CloseMenu();
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
    /// 関数呼び出し時にローカルスコープとスプライト寿命フレームをプッシュ
    /// </summary>
    internal void PushFunctionScope()
    {
        State.LocalIntStacks.Push(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        State.LocalStringStacks.Push(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        State.SpriteLifetimeStacks.Push(new HashSet<int>());
    }

    /// <summary>
    /// 関数からのリターン時にローカルスコープをポップし、スプライトを自動破棄
    /// </summary>
    internal void PopFunctionScope()
    {
        if (State.SpriteLifetimeStacks.Count > 0)
        {
            var spritesToRemove = State.SpriteLifetimeStacks.Pop();
            foreach (int spriteId in spritesToRemove)
            {
                State.Sprites.Remove(spriteId);
            }
        }
        if (State.LocalStringStacks.Count > 0)
            State.LocalStringStacks.Pop();
        if (State.LocalIntStacks.Count > 0)
            State.LocalIntStacks.Pop();
    }

    /// <summary>
    /// サブルーチンを呼び出します。
    /// </summary>
    public void CallSub(string labelName)
    {
        string label = labelName.TrimStart('*');
        if (_labels.TryGetValue(label, out int pc))
        {
            State.CallStack.Push(State.ProgramCounter);
            State.ProgramCounter = pc;
            State.State = VmState.Running;
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
        State.FocusedButtonId = -1;
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

    public void ToggleFullscreen()
    {
        bool goingFullscreen = !Config.Config.IsFullscreen;
        if (goingFullscreen)
        {
            // ウィンドウモード時のサイズを保存
            Config.Config.WindowWidth = Raylib_cs.Raylib.GetScreenWidth();
            Config.Config.WindowHeight = Raylib_cs.Raylib.GetScreenHeight();
            Raylib_cs.Raylib.ToggleFullscreen();
            int monitor = Raylib_cs.Raylib.GetCurrentMonitor();
            int mw = Raylib_cs.Raylib.GetMonitorWidth(monitor);
            int mh = Raylib_cs.Raylib.GetMonitorHeight(monitor);
            Raylib_cs.Raylib.SetWindowSize(mw, mh);
        }
        else
        {
            Raylib_cs.Raylib.ToggleFullscreen();
            Raylib_cs.Raylib.SetWindowSize(Config.Config.WindowWidth, Config.Config.WindowHeight);
        }
        Config.Config.IsFullscreen = goingFullscreen;
        Config.Save();
    }

    public void StopSkip()
    {
        State.SkipMode = false;
        State.ForceSkipMode = false;
    }

    public void FinishAllTweens()
    {
        Tweens.FinishAll(State);
    }

    public void SetIncludeResolver(Func<string, ParseResult?> resolver)
    {
        _includeResolver = resolver;
    }

    public void ClearIncludedFiles()
    {
        _includedFiles.Clear();
    }

    public bool IncludeScript(string path)
    {
        if (_includedFiles.Contains(path)) return true;
        if (_includeResolver == null) return false;

        var result = _includeResolver(path);
        if (result == null) return false;

        _includedFiles.Add(path);
        AppendScript(result, path);
        return true;
    }

    public int ProcessSkipFrame(float deltaTimeMs)
    {
        return SkipModeManager.ProcessSkipFrame(deltaTimeMs);
    }

    private bool CanSkipCurrentWait()
    {
        return SkipModeManager.CanSkipCurrentWait();
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
        byte[]? screenshot = CaptureThumbnail();
        Saves.Save(slot, State, _currentScriptFile, screenshot);
    }

    private byte[]? CaptureThumbnail()
    {
        try
        {
            if (!Raylib.IsWindowReady()) return null;
            var image = Raylib.LoadImageFromScreen();
            Raylib.ImageResize(ref image, 320, 180);
            string tempPath = Path.Combine(Path.GetTempPath(), $"aria_thumb_{Guid.NewGuid():N}.png");
            Raylib.ExportImage(image, tempPath);
            Raylib.UnloadImage(image);
            var bytes = File.ReadAllBytes(tempPath);
            try { File.Delete(tempPath); } catch (Exception ex) { _reporter.Report(new AriaError($"Failed to delete temp thumbnail: {ex.Message}", -1, "VirtualMachine.CaptureThumbnail", AriaErrorLevel.Warning, "THUMB_CLEANUP")); }
            return bytes;
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"Thumbnail capture failed: {ex.Message}", -1, "VirtualMachine.CaptureThumbnail", AriaErrorLevel.Warning, "THUMB_CAPTURE"));
            return null;
        }
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
            SaveStateNormalizer.NormalizeLoadedState(data);
            _currentScriptFile = SaveStateNormalizer.CurrentScriptFile;
            _currentReadKeyPrefix = _currentScriptFile + ":";
            CompatUiManager.ScanAndTrackTransientSprites();

            // "load_restore"ラベルが存在すればそちらに飛ぶことで初期化をスクリプトから行う
            if (_labels.ContainsKey("load_restore")) JumpTo("*load_restore");
            else
            {
                // Fallback: If no custom setup is provided, attempt to update the renderer if needed
            }

            NormalizeLoadedUiState();
        }
        else
        {
            _reporter.Report(new AriaError($"Failed to load game from slot {slot}", -1, _currentScriptFile, AriaErrorLevel.Warning, "LOAD_FAILED"));
        }
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

        CompatUiManager.ScanAndTrackTransientSprites();
    }

    private static bool IsTransientCompatUiSprite(Sprite sprite)
    {
        return CompatUiManager.IsTransientCompatSprite(sprite);
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


