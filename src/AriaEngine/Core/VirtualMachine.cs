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
        State.Execution.LocalIntStacks.Push(intScope);
        State.Execution.LocalStringStacks.Push(strScope);
        // Push lifetime tracker for sprites created in this scope
        State.Execution.SpriteLifetimeStacks.Push(new HashSet<int>());
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
            if (State.Execution.SpriteLifetimeStacks.Count > 0)
            {
                var lifetimeSet = State.Execution.SpriteLifetimeStacks.Pop();
                foreach (var sid in lifetimeSet)
                {
                    State.Render.Sprites.Remove(sid);
                }
            }

            // Pop local scope dictionaries
            if (State.Execution.LocalIntStacks.Count > 0) State.Execution.LocalIntStacks.Pop();
            if (State.Execution.LocalStringStacks.Count > 0) State.Execution.LocalStringStacks.Pop();
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
        State.TextRuntime.TextSpeedMs = Config.Config.GlobalTextSpeedMs;
        State.Audio.BgmVolume = Config.Config.BgmVolume;
        State.Audio.SeVolume = Config.Config.SeVolume;
        State.Playback.AutoModeWaitTimeMs = Config.Config.AutoModeWaitTimeMs;
        var persistent = Config.LoadPersistentGameData();
        State.Playback.SkipUnread = persistent.SkipUnread;
        State.RegisterState.Registers = new Dictionary<string, int>(persistent.Registers, StringComparer.OrdinalIgnoreCase);
        State.FlagRuntime.Flags = new Dictionary<string, bool>(persistent.Flags, StringComparer.OrdinalIgnoreCase);
        State.FlagRuntime.SaveFlags = new Dictionary<string, bool>(persistent.SaveFlags, StringComparer.OrdinalIgnoreCase);
        State.FlagRuntime.Counters = new Dictionary<string, int>(persistent.Counters, StringComparer.OrdinalIgnoreCase);
        State.TextRuntime.ReadKeys = new HashSet<string>(persistent.ReadKeys, StringComparer.OrdinalIgnoreCase);
        State.FlagRuntime.UnlockedCgs = new HashSet<string>(persistent.UnlockedCgs, StringComparer.OrdinalIgnoreCase);

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
        State.Execution.ProgramCounter = 0;
        State.Execution.State = VmState.Running;
        State.FlagRuntime.TotalScriptLines = result.SourceLines.Length;
        
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
        State.Execution.ProgramCounter = 0;
        State.Execution.State = VmState.Running;
    }

    public void ResumeFromClick()
    {
        if (State.Execution.State == VmState.WaitingForClick)
        {
            State.Execution.State = VmState.Running;
        }
    }

    public void ResumeFromButton(int buttonId)
    {
        if (State.Execution.State == VmState.WaitingForButton)
        {
            int resultValue = State.Interaction.SpriteButtonMap.TryGetValue(buttonId, out int mappedValue) ? mappedValue : buttonId;

            // Set explicit target register + compatibility registers
            string targetReg = State.Interaction.ButtonResultRegister.TrimStart('%').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(targetReg)) targetReg = "0";
            SetReg(targetReg, resultValue);
            SetReg("0", resultValue);
            SetReg("r0", resultValue);

            ClearCompatUiSprites();

            State.Execution.State = VmState.Running;
            AutoSaveGame();
            if (State.UiComposition.Events.TryGetValue($"{buttonId}:click", out string? label) ||
                State.UiComposition.Events.TryGetValue($"{resultValue}:click", out label))
            {
                JumpTo(label);
            }
            State.Interaction.ButtonTimeoutMs = 0;
            State.Interaction.ButtonTimer = 0f;
            State.Interaction.ButtonResultRegister = "0";
            State.Interaction.FocusedButtonId = -1;
            State.UiComposition.HoverActive.Clear();
        }
    }

    public void SignalTimeout()
    {
        if (State.Execution.State == VmState.WaitingForButton)
        {
            string targetReg = State.Interaction.ButtonResultRegister.TrimStart('%').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(targetReg)) targetReg = "0";
            SetReg(targetReg, -1);
            SetReg("0", -1);
            SetReg("r0", -1);
            State.Execution.State = VmState.Running;
            State.Interaction.ButtonTimeoutMs = 0;
            State.Interaction.ButtonTimer = 0f;
            State.Interaction.ButtonResultRegister = "0";
            State.Interaction.FocusedButtonId = -1;
            State.UiComposition.HoverActive.Clear();
        }
    }

    public void FinishFade()
    {
        if (State.Execution.State == VmState.FadingIn || State.Execution.State == VmState.FadingOut)
        {
            State.Render.IsFading = false;
            State.Render.TransitionStyle = TransitionType.Fade;
            State.Execution.State = VmState.Running;
        }
    }

    public void Step()
    {
        var state = State; // ローカルキャッシュ
        int maxSteps = (state.Playback.SkipMode || state.Playback.ForceSkipMode) ? int.MaxValue : MaxInstructionsPerFrame;
        int executed = 0;
        
        while (state.Execution.State == VmState.Running && executed < maxSteps)
        {
            if (state.Execution.ProgramCounter < 0 || state.Execution.ProgramCounter > _instructions.Count)
            {
                string scriptFile = (state.Execution.ProgramCounter >= 0 && state.Execution.ProgramCounter < _instructions.Count)
                    ? _instructions[state.Execution.ProgramCounter].ScriptFile
                    : _currentScriptFile;
                _reporter.Report(new AriaError(
                    $"プログラムカウンタが範囲外です (PC={state.Execution.ProgramCounter}, 命令数={_instructions.Count})。スクリプト実行を終了します。",
                    0, scriptFile, AriaErrorLevel.Error, "VM_PC_OUT_OF_BOUNDS"));
                state.Execution.State = VmState.Ended;
                break;
            }
            if (state.Execution.ProgramCounter >= _instructions.Count) break;

            // T20: Autosave at chapter label start
            if (_labelAddresses.Contains(state.Execution.ProgramCounter))
            {
                if (TryGetCurrentLabelAndOffset(out string labelName, out int offset) && offset == 0 &&
                    labelName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase))
                {
                    AutoSaveGame();
                }
            }

            var inst = _instructions[state.Execution.ProgramCounter];
            state.Execution.ProgramCounter++;
            state.TextRuntime.CurrentInstructionWasRead = IsInstructionRead(inst);
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
                string details = $"op={inst.Op}; pc={state.Execution.ProgramCounter - 1}; args=[{string.Join(", ", inst.Arguments)}]";
                _reporter.Report(new AriaError(
                    $"実行時エラー: {ex.Message}",
                    inst.SourceLine,
                    scriptFile,
                    AriaErrorLevel.Error,
                    "VM_EXEC_ERROR",
                    details: details,
                    exceptionType: ex.GetType().Name));
            }
            
            executed++;
        }

        if (state.Execution.State == VmState.Running && state.Execution.ProgramCounter >= _instructions.Count)
        {
            state.Execution.State = VmState.Ended;
        }
    }

    public void Update(float deltaTimeMs)
    {
        if (State.Render.QuakeTimerMs > 0f)
        {
            State.Render.QuakeTimerMs = Math.Max(0f, State.Render.QuakeTimerMs - deltaTimeMs);
            if (State.Render.QuakeTimerMs <= 0f) State.Render.QuakeAmplitude = 0;
        }

        if (State.Render.ScreenTintTimerMs > 0f)
        {
            State.Render.ScreenTintTimerMs = Math.Max(0f, State.Render.ScreenTintTimerMs - deltaTimeMs);
            if (State.Render.ScreenTintTimerMs <= 0f && State.Render.ScreenTintOpacity > 0f)
            {
                State.Render.ScreenTintOpacity = 0f;
                State.Render.ActiveEffects.RemoveAll(e => e.StartsWith("screen:", StringComparison.OrdinalIgnoreCase));
            }
        }

        // Typewriter effect tracking
        if (State.TextRuntime.TextSpeedMs > 0 && State.TextRuntime.DisplayedTextLength < State.TextRuntime.CurrentTextBuffer.Length)
        {
            State.TextRuntime.TextTimerMs += deltaTimeMs;
            while (State.TextRuntime.TextTimerMs >= State.TextRuntime.TextSpeedMs && State.TextRuntime.DisplayedTextLength < State.TextRuntime.CurrentTextBuffer.Length)
            {
                State.TextRuntime.TextTimerMs -= State.TextRuntime.TextSpeedMs;
                State.TextRuntime.DisplayedTextLength++;

                // Per-character SE: find which segment the newly revealed character belongs to and play its voice/SE
                if (Audio != null && State.TextRuntime.CurrentTextSegments is { Count: > 0 } segments)
                {
                    int pos = State.TextRuntime.DisplayedTextLength - 1;
                    int segStart = 0;
                    foreach (var seg in segments)
                    {
                        int segLen = seg.Text.Length + (seg.IsNewLine ? 1 : 0);
                        if (pos < segStart + segLen)
                        {
                            if (!string.IsNullOrEmpty(seg.Style.VoiceSePath))
                            {
                                Audio.PlayVoice(seg.Style.VoiceSePath, seg.Style.VoiceSeVolume);
                            }
                            break;
                        }
                        segStart += segLen;
                    }
                }
            }

            if (State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var txtSprite))
            {
                int length = Math.Clamp(State.TextRuntime.DisplayedTextLength, 0, State.TextRuntime.CurrentTextBuffer.Length);
                txtSprite.Text = State.TextRuntime.CurrentTextBuffer.Substring(0, length);
            }

            if (State.TextRuntime.DisplayedTextLength >= State.TextRuntime.CurrentTextBuffer.Length && State.Execution.State == VmState.WaitingForAnimation)
            {
                if (!HasBlockingEffect())
                {
                    State.Execution.State = VmState.Running; // Resume script
                }
            }
        }

        if (State.Execution.State == VmState.WaitingForDelay)
        {
            State.Execution.DelayTimerMs -= deltaTimeMs;
            if (State.Execution.DelayTimerMs <= 0) State.Execution.State = VmState.Running;
        }

        if (State.Execution.State == VmState.WaitingForClick && State.Playback.AutoMode)
        {
            State.Playback.AutoModeTimerMs += deltaTimeMs;
            if (State.Playback.AutoModeTimerMs >= State.Playback.AutoModeWaitTimeMs)
            {
                State.Playback.AutoModeTimerMs = 0;
                ResumeFromClick();
            }
        }

        // Tween/画面効果完了チェック: awaitで止まっているとき、効果が終わったらRunningに戻す
        if (State.Execution.State == VmState.WaitingForAnimation && !HasBlockingEffect())
        {
            // タイプライター中でもなければ復帰
            bool typewriterActive = State.TextRuntime.TextSpeedMs > 0 && State.TextRuntime.DisplayedTextLength < State.TextRuntime.CurrentTextBuffer.Length;
            if (!typewriterActive)
            {
                State.Execution.State = VmState.Running;
            }
        }
        
        State.Execution.ScriptTimerMs += deltaTimeMs;
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
        if (!State.TextRuntime.KidokuMode) return;
        if (State.TextRuntime.ReadKeys.Add(_currentReadKeyPrefix + inst.SourceLine))
        {
            MarkPersistentDirty();
        }
    }

    private bool IsInstructionRead(Instruction inst)
    {
        return State.TextRuntime.ReadKeys.Contains(_currentReadKeyPrefix + inst.SourceLine);
    }

    internal void MarkPersistentDirty()
    {
        _persistentDirty = true;
    }

    public void SavePersistentState()
    {
        Config.Config.GlobalTextSpeedMs = State.TextRuntime.TextSpeedMs;
        Config.Config.BgmVolume = State.Audio.BgmVolume;
        Config.Config.SeVolume = State.Audio.SeVolume;
        Config.Config.SkipUnread = State.Playback.SkipUnread;
        Config.Config.AutoModeWaitTimeMs = State.Playback.AutoModeWaitTimeMs;
        Config.SavePersistentGameData(new PersistentGameData
        {
            SkipUnread = State.Playback.SkipUnread,
            Registers = State.RegisterState.Registers
                .Where(pair => RegisterStoragePolicy.IsPersistent(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Flags = new Dictionary<string, bool>(State.FlagRuntime.Flags, StringComparer.OrdinalIgnoreCase),
            SaveFlags = new Dictionary<string, bool>(State.FlagRuntime.SaveFlags, StringComparer.OrdinalIgnoreCase),
            Counters = new Dictionary<string, int>(State.FlagRuntime.Counters, StringComparer.OrdinalIgnoreCase),
            ReadKeys = State.TextRuntime.ReadKeys.ToList(),
            UnlockedCgs = State.FlagRuntime.UnlockedCgs.ToList()
        });
        Config.Save();
        _persistentDirty = false;
        _persistentSaveTimerMs = 0f;
    }

    internal void AddBacklogEntry()
    {
        if (!State.TextRuntime.BacklogEnabled) return;
        string text = State.TextRuntime.CurrentTextBuffer.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (State.TextRuntime.TextHistory.Count == 0 || State.TextRuntime.TextHistory[^1].Text != text)
        {
            var entry = new BacklogEntry
            {
                Text = text,
                VoicePath = string.IsNullOrEmpty(State.Audio.LastVoicePath) ? null : State.Audio.LastVoicePath,
                ProgramCounter = State.Execution.ProgramCounter,
                IsRead = false,
                Timestamp = DateTime.Now,
                StateSnapshot = CaptureBacklogSnapshot()
            };
            State.TextRuntime.TextHistory.Add(entry);
            State.Audio.LastVoicePath = "";
            const int maxTextHistory = 300;
            if (State.TextRuntime.TextHistory.Count > maxTextHistory)
            {
                int removeCount = State.TextRuntime.TextHistory.Count - maxTextHistory;
                State.TextRuntime.TextHistory.RemoveRange(0, removeCount);
                State.TextRuntime.TextHistoryStartNumber += removeCount;
            }
        }
    }

    private BacklogStateSnapshot CaptureBacklogSnapshot()
    {
        var sprites = new FastSpriteDictionary();
        foreach (var kvp in State.Render.Sprites)
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
            Registers = new Dictionary<string, int>(State.RegisterState.Registers, StringComparer.OrdinalIgnoreCase),
            StringRegisters = new Dictionary<string, string>(State.RegisterState.StringRegisters, StringComparer.OrdinalIgnoreCase),
            Flags = new Dictionary<string, bool>(State.FlagRuntime.Flags),
            SaveFlags = new Dictionary<string, bool>(State.FlagRuntime.SaveFlags),
            Counters = new Dictionary<string, int>(State.FlagRuntime.Counters, StringComparer.OrdinalIgnoreCase),
            Sprites = sprites,
            CurrentBgm = State.Audio.CurrentBgm,
            BgmVolume = State.Audio.BgmVolume,
            SeVolume = State.Audio.SeVolume
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
        if (State.Execution.CurrentRefMap.TryGetValue(name, out string? originalReg))
        {
            name = RegisterStoragePolicy.Normalize(originalReg);
            SetGlobalReg(name, val);
            return;
        }
        
        // ローカルスコープ優先
        if (State.Execution.LocalIntStacks.Count > 0)
        {
            State.Execution.LocalIntStacks.Peek()[name] = val;
            return;
        }
        
        // 高速パス: %0-%9
        if (name.Length == 1 && name[0] >= '0' && name[0] <= '9')
        {
            _fastRegisters[name[0] - '0'] = val;
        }
        
        State.RegisterState.Registers[name] = val;
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

        State.RegisterState.Registers[name] = val;
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
        if (State.Execution.CurrentRefMap.TryGetValue(normalized, out string? originalReg))
        {
            normalized = RegisterStoragePolicy.Normalize(originalReg);
        }
        
        // ローカルスコープ優先
        if (State.Execution.LocalIntStacks.Count > 0 && State.Execution.LocalIntStacks.Peek().TryGetValue(normalized, out int localVal))
        {
            return localVal;
        }
        
        // 高速パス: %0-%9
        if (normalized.Length == 1 && normalized[0] >= '0' && normalized[0] <= '9')
        {
            return _fastRegisters[normalized[0] - '0'];
        }
        
        return State.RegisterState.Registers.TryGetValue(normalized, out int v) ? v : 0;
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

    private bool HasBlockingEffect()
    {
        return Tweens.IsAnimating || State.Render.ScreenTintTimerMs > 0f || State.Render.QuakeTimerMs > 0f;
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
        if (regValue != 0 || State.RegisterState.Registers.ContainsKey(RegisterStoragePolicy.Normalize(valStr)))
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
        if (State.Execution.LocalStringStacks.Count > 0)
        {
            State.Execution.LocalStringStacks.Peek()[key] = val;
            return;
        }
        State.RegisterState.StringRegisters[key] = val;
    }

    // Public API to set a global (bypass local scopes) integer register
    public void SetGlobalRegister(string reg, int value)
    {
        string name = RegisterStoragePolicy.Normalize(reg);
        // Respect ref aliasing similar to SetReg
        if (State.Execution.CurrentRefMap.TryGetValue(name, out string? originalReg))
        {
            name = RegisterStoragePolicy.Normalize(originalReg);
        }
        // Write directly to global registers (bypass LocalIntStacks)
        State.RegisterState.Registers[name] = value;
        if (RegisterStoragePolicy.IsPersistent(name))
        {
            MarkPersistentDirty();
        }
    }

    // Public API to set a global string register
    public void SetGlobalString(string reg, string value)
    {
        string key = reg.TrimStart('$');
        if (State.Execution.LocalStringStacks.Count > 0)
        {
            State.Execution.LocalStringStacks.Peek()[key] = value;
            return;
        }
        State.RegisterState.StringRegisters[key] = value;
    }

    /// <summary>
    /// 文字列を解決。$レジスタ、文字列補間 ${...} に対応
    /// </summary>
    internal string GetString(string valStr)
    {
        if (valStr.StartsWith("$"))
        {
            string key = valStr.TrimStart('$');
            if (State.Execution.LocalStringStacks.Count > 0 && State.Execution.LocalStringStacks.Peek().TryGetValue(key, out string? localVal))
            {
                return localVal ?? "";
            }
            return State.RegisterState.StringRegisters.TryGetValue(key, out string? v) ? (v ?? "") : "";
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
                    if (State.Execution.LocalStringStacks.Count > 0 && State.Execution.LocalStringStacks.Peek().TryGetValue(key, out string? localValue))
                    {
                        return localValue ?? "";
                    }
                    return State.RegisterState.StringRegisters.TryGetValue(key, out string? sv) ? (sv ?? "") : "";
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
                State.MenuRuntime.ShowSystemCloseButton = visible;
                break;
            case "reset":
                State.MenuRuntime.ShowSystemResetButton = visible;
                break;
            case "skip":
                State.MenuRuntime.ShowSystemSkipButton = visible;
                break;
            case "save":
                State.MenuRuntime.ShowSystemSaveButton = visible;
                break;
            case "load":
                State.MenuRuntime.ShowSystemLoadButton = visible;
                break;
        }
    }

    /// <summary>
    /// 現在のPC位置から、所属するラベル名とラベル先頭からのオフセットを取得する。
    /// </summary>
    public bool TryGetCurrentLabelAndOffset(out string labelName, out int offset)
    {
        int pc = State.Execution.ProgramCounter;
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
        if (_labels.TryGetValue(label, out int pc)) State.Execution.ProgramCounter = pc;
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
            State.RegisterState.Registers = new Dictionary<string, int>(entry.StateSnapshot.Registers, StringComparer.OrdinalIgnoreCase);
            State.RegisterState.StringRegisters = new Dictionary<string, string>(entry.StateSnapshot.StringRegisters, StringComparer.OrdinalIgnoreCase);
            State.FlagRuntime.Flags = new Dictionary<string, bool>(entry.StateSnapshot.Flags);
            State.FlagRuntime.SaveFlags = new Dictionary<string, bool>(entry.StateSnapshot.SaveFlags);
            State.FlagRuntime.Counters = new Dictionary<string, int>(entry.StateSnapshot.Counters, StringComparer.OrdinalIgnoreCase);
            State.Render.Sprites = entry.StateSnapshot.Sprites;
            State.Audio.CurrentBgm = entry.StateSnapshot.CurrentBgm;
            State.Audio.BgmVolume = entry.StateSnapshot.BgmVolume;
            State.Audio.SeVolume = entry.StateSnapshot.SeVolume;
        }

        State.Execution.ProgramCounter = entry.ProgramCounter;
        State.TextRuntime.CurrentTextBuffer = entry.Text;
        State.TextRuntime.DisplayedTextLength = entry.Text.Length;
        State.TextRuntime.IsWaitingPageClear = false;
        State.Execution.State = VmState.Running;
        Menu.CloseMenu();
    }

    internal void ReturnFromSubroutine()
    {
        if (State.Execution.CallStack.Count > 0)
        {
            State.Execution.ProgramCounter = State.Execution.CallStack.Pop();
        }
        else if (State.Execution.ProgramCounter >= _instructions.Count)
        {
            State.Execution.State = VmState.Ended;
        }
    }

    /// <summary>
    /// 関数呼び出し時にローカルスコープとスプライト寿命フレームをプッシュ
    /// </summary>
    internal void PushFunctionScope()
    {
        State.Execution.LocalIntStacks.Push(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        State.Execution.LocalStringStacks.Push(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        State.Execution.SpriteLifetimeStacks.Push(new HashSet<int>());
    }

    /// <summary>
    /// 関数からのリターン時にローカルスコープをポップし、スプライトを自動破棄
    /// </summary>
    internal void PopFunctionScope()
    {
        if (State.Execution.SpriteLifetimeStacks.Count > 0)
        {
            var spritesToRemove = State.Execution.SpriteLifetimeStacks.Pop();
            foreach (int spriteId in spritesToRemove)
            {
                State.Render.Sprites.Remove(spriteId);
            }
        }
        if (State.Execution.LocalStringStacks.Count > 0)
            State.Execution.LocalStringStacks.Pop();
        if (State.Execution.LocalIntStacks.Count > 0)
            State.Execution.LocalIntStacks.Pop();
    }

    /// <summary>
    /// サブルーチンを呼び出します。
    /// </summary>
    public void CallSub(string labelName)
    {
        string label = labelName.TrimStart('*');
        if (_labels.TryGetValue(label, out int pc))
        {
            State.Execution.CallStack.Push(State.Execution.ProgramCounter);
            State.Execution.ProgramCounter = pc;
            State.Execution.State = VmState.Running;
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
        State.Execution.State = VmState.Ended;
        State.UiRuntime.RequestClose = true;
    }

    public void ResetGame()
    {
        State.Render.Sprites.Clear();
        State.Interaction.SpriteButtonMap.Clear();
        State.Interaction.FocusedButtonId = -1;
        State.TextRuntime.CurrentTextBuffer = "";
        State.TextRuntime.DisplayedTextLength = 0;
        State.Execution.State = VmState.Running;

        if (_labels.ContainsKey("start")) JumpTo("*start");
        else if (_labels.ContainsKey("title_start")) JumpTo("*title_start");
        else State.Execution.ProgramCounter = 0;
    }

    public void ToggleSkip()
    {
        State.Playback.SkipMode = !State.Playback.SkipMode;
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
        State.Playback.SkipMode = false;
        State.Playback.ForceSkipMode = false;
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
        State.TextRuntime.DisplayedTextLength = State.TextRuntime.CurrentTextBuffer.Length;
        if (State.TextWindow.TextTargetSpriteId >= 0 &&
            State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var textSprite) &&
            textSprite.Type == SpriteType.Text)
        {
            textSprite.Text = State.TextRuntime.CurrentTextBuffer;
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
        if (State.TextWindow.TextboxBackgroundSpriteId >= 0 && !State.Render.Sprites.ContainsKey(State.TextWindow.TextboxBackgroundSpriteId))
        {
            State.TextWindow.TextboxBackgroundSpriteId = -1;
        }

        if (State.TextWindow.TextTargetSpriteId >= 0 && !State.Render.Sprites.ContainsKey(State.TextWindow.TextTargetSpriteId))
        {
            State.TextWindow.TextTargetSpriteId = -1;
        }

        foreach (var sprite in State.Render.Sprites.Values)
        {
            sprite.IsHovered = false;
        }

        State.Interaction.SpriteButtonMap = State.Interaction.SpriteButtonMap
            .Where(pair => State.Render.Sprites.TryGetValue(pair.Key, out var sprite) && sprite.IsButton)
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
        if (!State.TextWindow.CompatAutoUi || State.TextWindow.UseManualTextLayout) return;

        if (State.TextWindow.TextboxBackgroundSpriteId >= 0 &&
            State.Render.Sprites.TryGetValue(State.TextWindow.TextboxBackgroundSpriteId, out var bg) &&
            bg.Type == SpriteType.Rect)
        {
            bg.X = State.TextWindow.DefaultTextboxX;
            bg.Y = State.TextWindow.DefaultTextboxY;
            bg.Width = State.TextWindow.DefaultTextboxW;
            bg.Height = State.TextWindow.DefaultTextboxH;
            bg.FillColor = State.TextWindow.DefaultTextboxBgColor;
            bg.FillAlpha = State.TextWindow.DefaultTextboxBgAlpha;
            bg.CornerRadius = State.TextWindow.DefaultTextboxCornerRadius;
            bg.BorderColor = State.TextWindow.DefaultTextboxBorderColor;
            bg.BorderWidth = State.TextWindow.DefaultTextboxBorderWidth;
            bg.BorderOpacity = State.TextWindow.DefaultTextboxBorderOpacity;
            bg.ShadowColor = State.TextWindow.DefaultTextboxShadowColor;
            bg.ShadowOffsetX = State.TextWindow.DefaultTextboxShadowOffsetX;
            bg.ShadowOffsetY = State.TextWindow.DefaultTextboxShadowOffsetY;
            bg.ShadowAlpha = State.TextWindow.DefaultTextboxShadowAlpha;
            bg.Visible = State.TextWindow.TextboxVisible;
            bg.Z = 9000;
        }

        if (State.TextWindow.TextTargetSpriteId >= 0 &&
            State.Render.Sprites.TryGetValue(State.TextWindow.TextTargetSpriteId, out var text) &&
            text.Type == SpriteType.Text)
        {
            text.X = State.TextWindow.DefaultTextboxX + State.TextWindow.DefaultTextboxPaddingX;
            text.Y = State.TextWindow.DefaultTextboxY + State.TextWindow.DefaultTextboxPaddingY;
            text.Width = Math.Max(0, State.TextWindow.DefaultTextboxW - (State.TextWindow.DefaultTextboxPaddingX * 2));
            text.Height = Math.Max(0, State.TextWindow.DefaultTextboxH - (State.TextWindow.DefaultTextboxPaddingY * 2));
            text.FontSize = State.TextWindow.DefaultFontSize;
            text.Color = State.TextWindow.DefaultTextColor;
            text.TextShadowColor = State.TextRuntime.DefaultTextShadowColor;
            text.TextShadowX = State.TextRuntime.DefaultTextShadowX;
            text.TextShadowY = State.TextRuntime.DefaultTextShadowY;
            text.TextOutlineColor = State.TextRuntime.DefaultTextOutlineColor;
            text.TextOutlineSize = State.TextRuntime.DefaultTextOutlineSize;
            text.TextEffect = State.TextRuntime.DefaultTextEffect;
            text.TextEffectStrength = State.TextRuntime.DefaultTextEffectStrength;
            text.TextEffectSpeed = State.TextRuntime.DefaultTextEffectSpeed;
            text.Visible = State.TextWindow.TextboxVisible;
            text.Z = 9001;
            int length = Math.Clamp(State.TextRuntime.DisplayedTextLength, 0, State.TextRuntime.CurrentTextBuffer.Length);
            text.Text = State.TextRuntime.CurrentTextBuffer.Substring(0, length);
        }
    }
}


