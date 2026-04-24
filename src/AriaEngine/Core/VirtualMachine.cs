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
    private readonly FlagCommandExecutor _flagCommands;

    public VirtualMachine(ErrorReporter reporter, TweenManager tweens, SaveManager saves, ConfigManager config)
    {
        _reporter = reporter;
        Tweens = tweens;
        Saves = saves;
        Config = config;
        State = new GameState();
        Menu = new MenuSystem(this);
        SpritePool = new SpritePool(128); // スプライトプールを初期化
        _flagCommands = new FlagCommandExecutor(this);

        // Load initial config into GameState
        State.TextSpeedMs = Config.Config.GlobalTextSpeedMs;
        State.BgmVolume = Config.Config.BgmVolume;
        State.SeVolume = Config.Config.SeVolume;
        var persistent = Config.LoadPersistentGameData();
        State.SkipUnread = persistent.SkipUnread;
        State.Registers = new Dictionary<string, int>(persistent.Registers, StringComparer.OrdinalIgnoreCase);
        State.Flags = new Dictionary<string, bool>(persistent.Flags, StringComparer.OrdinalIgnoreCase);
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

    private bool EvaluateCondition(IReadOnlyList<string>? condTokens)
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
        switch (inst.Op)
        {
            case OpCode.Window:
                if (!ValidateArgs(inst, 3)) break;
                State.WindowWidth = GetVal(inst.Arguments[0]);
                State.WindowHeight = GetVal(inst.Arguments[1]);
                State.Title = inst.Arguments[2];
                break;
            case OpCode.Caption:
                if (!ValidateArgs(inst, 1)) break;
                State.Title = inst.Arguments[0];
                break;
            case OpCode.WindowTitle:
                if (!ValidateArgs(inst, 1)) break;
                State.Title = GetString(inst.Arguments[0]);
                break;
            case OpCode.Font:
                if (!ValidateArgs(inst, 1)) break;
                State.FontPath = inst.Arguments[0];
                break;
            case OpCode.FontAtlasSize:
                if (!ValidateArgs(inst, 1)) break;
                State.FontAtlasSize = Math.Clamp(GetVal(inst.Arguments[0]), 8, 512);
                break;
            case OpCode.Script:
                if (!ValidateArgs(inst, 1)) break;
                State.MainScript = inst.Arguments[0];
                break;
            case OpCode.Debug:
                if (inst.Arguments.Count > 0 && inst.Arguments[0] == "on") State.DebugMode = true;
                else State.DebugMode = false;
                break;
            
            // Subroutines and Control Flow
            case OpCode.Gosub:
                if (!ValidateArgs(inst, 1)) break;
                State.CallStack.Push(State.ProgramCounter);
                JumpTo(inst.Arguments[0]);
                for (int i = inst.Arguments.Count - 1; i >= 1; i--)
                    State.ParamStack.Push(inst.Arguments[i]);
                break;
            case OpCode.Return:
                if (State.CallStack.Count > 0) State.ProgramCounter = State.CallStack.Pop();
                else
                {
                    // Stack is empty - this might be a direct jump to a label with return
                    // Just continue to next instruction or end if at end
                    if (State.ProgramCounter >= _instructions.Count)
                        State.State = VmState.Ended;
                }
                break;
            case OpCode.Defsub:
                // Parser handled this
                break;
            case OpCode.Getparam:
                foreach (var arg in inst.Arguments)
                {
                    if (State.ParamStack.Count > 0)
                    {
                        var val = State.ParamStack.Pop();
                        if (arg.StartsWith("$")) SetStr(arg.TrimStart('$'), GetString(val));
                        else SetReg(arg, GetVal(val));
                    }
                }
                break;
            case OpCode.Alias:
                if (!ValidateArgs(inst, 2)) break;
                // We use dynamic mapping via SetReg to preserve values, or let the parser rewrite.
                // In this basic VM, we will just use variables natively. Alias sets initial mapping or just ignores.
                break;
            case OpCode.SystemCall:
                if (!ValidateArgs(inst, 1)) break;
                switch (inst.Arguments[0].ToLowerInvariant())
                {
                    case "rmenu":
                        Menu.OpenMainMenu();
                        break;
                    case "lookback":
                        Menu.OpenBacklog();
                        break;
                    case "load":
                        Menu.OpenSaveLoadMenu(false);
                        break;
                }
                break;

            // Sprites
            case OpCode.Lsp:
                if (!ValidateArgs(inst, 4)) break;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Sprites[id] = new Sprite
                    {
                        Id = id, Type = SpriteType.Image, ImagePath = GetString(inst.Arguments[1]),
                        X = GetVal(inst.Arguments[2]), Y = GetVal(inst.Arguments[3])
                    };
                }
                break;
            case OpCode.LspText:
                if (!ValidateArgs(inst, 4)) break;
                {
                    int id = GetVal(inst.Arguments[0]);
                    int size = State.DefaultFontSize;
                    string color = State.DefaultTextColor;
                    State.Sprites[id] = new Sprite
                    {
                        Id = id, Type = SpriteType.Text, Text = GetString(inst.Arguments[1]),
                        X = GetVal(inst.Arguments[2]), Y = GetVal(inst.Arguments[3]),
                        FontSize = size, Color = color
                    };
                }
                break;
            case OpCode.LspRect:
                if (!ValidateArgs(inst, 5)) break;
                {
                    int id = GetVal(inst.Arguments[0]);
                    State.Sprites[id] = new Sprite
                    {
                        Id = id, Type = SpriteType.Rect, 
                        X = GetVal(inst.Arguments[1]), Y = GetVal(inst.Arguments[2]),
                        Width = GetVal(inst.Arguments[3]), Height = GetVal(inst.Arguments[4])
                    };
                }
                break;
            case OpCode.Csp:
                if (!ValidateArgs(inst, 1)) break;
                {
                    int id = GetVal(inst.Arguments[0]);
                    if (id == -1)
                    {
                        State.Sprites.Clear();
                        State.SpriteButtonMap.Clear();
                    }
                    else
                    {
                        State.Sprites.Remove(id);
                        State.SpriteButtonMap.Remove(id);
                    }
                }
                break;
            case OpCode.Vsp:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var vsp))
                {
                    vsp.Visible = int.TryParse(inst.Arguments[1], out int v) ? v != 0 : inst.Arguments[1] == "on";
                }
                break;
            case OpCode.Msp:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var msp))
                {
                    msp.X = GetVal(inst.Arguments[1]); msp.Y = GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.MspRel:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var mspr))
                {
                    mspr.X += GetVal(inst.Arguments[1]); mspr.Y += GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.SpZ:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spz)) spz.Z = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpAlpha:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spa)) spa.Opacity = GetVal(inst.Arguments[1]) / 255.0f;
                break;
            case OpCode.SpScale:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsc))
                {
                    spsc.ScaleX = GetFloat(inst.Arguments[1], inst);
                    spsc.ScaleY = GetFloat(inst.Arguments[2], inst);
                }
                break;
            case OpCode.SpFontsize:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spf)) spf.FontSize = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpColor:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spc)) spc.Color = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpFill:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spfl))
                {
                    spfl.FillColor = GetString(inst.Arguments[1]); spfl.FillAlpha = GetVal(inst.Arguments[2]);
                }
                break;

            case OpCode.Btn:
                if (!ValidateArgs(inst, 1)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var btn)) btn.IsButton = true;
                // Also add mapping for compatibility with btnwait
                if (!State.SpriteButtonMap.ContainsKey(GetVal(inst.Arguments[0])))
                    State.SpriteButtonMap[GetVal(inst.Arguments[0])] = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BtnArea:
                if (!ValidateArgs(inst, 5)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var ba))
                {
                    ba.IsButton = true;
                    ba.ClickAreaX = GetVal(inst.Arguments[1]); ba.ClickAreaY = GetVal(inst.Arguments[2]);
                    ba.ClickAreaW = GetVal(inst.Arguments[3]); ba.ClickAreaH = GetVal(inst.Arguments[4]);
                }
                break;
            case OpCode.BtnClear:
                if (!ValidateArgs(inst, 1)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var bcl)) bcl.IsButton = false;
                break;
            case OpCode.BtnClearAll:
                foreach (var s in State.Sprites.Values) s.IsButton = false;
                State.SpriteButtonMap.Clear();
                break;
            case OpCode.SpBtn:
                if (!ValidateArgs(inst, 2)) break;
                {
                    int sId = GetVal(inst.Arguments[0]);
                    int bId = GetVal(inst.Arguments[1]);
                    if (State.Sprites.TryGetValue(sId, out var spb)) spb.IsButton = true;
                    State.SpriteButtonMap[sId] = bId;
                }
                break;
                
            // ----- UI Decorators -----
            case OpCode.SpRound:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spr)) spr.CornerRadius = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpBorder:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spbr)) 
                {
                    spbr.BorderColor = GetString(inst.Arguments[1]);
                    spbr.BorderWidth = GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.SpGradient:
                if (!ValidateArgs(inst, 4)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spg)) 
                {
                    spg.GradientTo = GetString(inst.Arguments[2]);
                    spg.GradientDirection = GetString(inst.Arguments[3]);
                }
                break;
            case OpCode.SpShadow:
                if (!ValidateArgs(inst, 5)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsh)) 
                {
                    spsh.ShadowOffsetX = GetVal(inst.Arguments[1]);
                    spsh.ShadowOffsetY = GetVal(inst.Arguments[2]);
                    spsh.ShadowColor = GetString(inst.Arguments[3]);
                    spsh.ShadowAlpha = GetVal(inst.Arguments[4]);
                }
                break;
            case OpCode.SpTextShadow:
                if (!ValidateArgs(inst, 4)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spts)) 
                {
                    spts.TextShadowX = GetVal(inst.Arguments[1]);
                    spts.TextShadowY = GetVal(inst.Arguments[2]);
                    spts.TextShadowColor = GetString(inst.Arguments[3]);
                }
                break;
            case OpCode.SpTextOutline:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spto)) 
                {
                    spto.TextOutlineSize = GetVal(inst.Arguments[1]);
                    spto.TextOutlineColor = GetString(inst.Arguments[2]);
                }
                break;
            case OpCode.SpTextAlign:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spta)) spta.TextAlign = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpTextVAlign:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sptva)) sptva.TextVAlign = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpRotation:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sprot)) sprot.Rotation = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpHoverColor:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphc)) sphc.HoverFillColor = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpHoverScale:
                if (!ValidateArgs(inst, 2)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphs)) sphs.HoverScale = GetFloat(inst.Arguments[1], inst, 1.0f);
                break;

            // ----- Animations (Tween) -----
            case OpCode.Amsp:
                if (!ValidateArgs(inst, 4)) break;
                {
                    int tweenId = GetVal(inst.Arguments[0]);
                    float toX = GetVal(inst.Arguments[1]);
                    float toY = GetVal(inst.Arguments[2]);
                    float dur = GetVal(inst.Arguments[3]);
                    if (State.Sprites.TryGetValue(tweenId, out var sp_amsp))
                    {
                        Tweens.Add(new Tween { SpriteId = sp_amsp.Id, Property = "x", From = sp_amsp.X, To = toX, DurationMs = dur, Ease = Tweens.CurrentEaseType });
                        Tweens.Add(new Tween { SpriteId = sp_amsp.Id, Property = "y", From = sp_amsp.Y, To = toY, DurationMs = dur, Ease = Tweens.CurrentEaseType });
                    }
                }
                break;
            case OpCode.Afade:
                if (!ValidateArgs(inst, 3)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sp_afade))
                {
                    Tweens.Add(new Tween { SpriteId = sp_afade.Id, Property = "opacity", From = sp_afade.Opacity, To = GetVal(inst.Arguments[1])/255f, DurationMs = GetVal(inst.Arguments[2]), Ease = Tweens.CurrentEaseType });
                }
                break;
            case OpCode.Ascale:
                if (!ValidateArgs(inst, 4)) break;
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sp_ascale))
                {
                    Tweens.Add(new Tween { SpriteId = sp_ascale.Id, Property = "scaleX", From = sp_ascale.ScaleX, To = GetFloat(inst.Arguments[1], inst), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                    Tweens.Add(new Tween { SpriteId = sp_ascale.Id, Property = "scaleY", From = sp_ascale.ScaleY, To = GetFloat(inst.Arguments[2], inst), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                }
                break;
            case OpCode.Await:
                State.State = VmState.WaitingForAnimation;
                break;
            case OpCode.Ease:
                if (!ValidateArgs(inst, 1)) break;
                string easeName = GetString(inst.Arguments[0]).ToLowerInvariant();
                if (easeName == "easein") Tweens.CurrentEaseType = EaseType.EaseIn;
                else if (easeName == "easeout") Tweens.CurrentEaseType = EaseType.EaseOut;
                else if (easeName == "easeinout") Tweens.CurrentEaseType = EaseType.EaseInOut;
                else Tweens.CurrentEaseType = EaseType.Linear;
                break;

            case OpCode.BtnTime:
                if (!ValidateArgs(inst, 1)) break;
                State.ButtonTimeoutMs = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BtnWait:
                if (inst.Arguments.Count > 0)
                {
                    State.ButtonResultRegister = inst.Arguments[0];
                }
                else
                {
                    State.ButtonResultRegister = "0";
                }
                State.State = VmState.WaitingForButton;
                break;

            case OpCode.LoadBg:
            case OpCode.Bg:
                if (!ValidateArgs(inst, 1)) break;
                string bgPath = GetString(inst.Arguments[0]);
                if (bgPath.StartsWith("#"))
                {
                    State.Sprites[0] = new Sprite { Id = 0, Type = SpriteType.Rect, FillColor = bgPath, FillAlpha = 255, Width = State.WindowWidth, Height = State.WindowHeight, Z = 0 };
                }
                else
                {
                    State.Sprites[0] = new Sprite { Id = 0, Type = SpriteType.Image, ImagePath = bgPath, Z = 0 };
                }
                // Also trigger effect if present
                if (inst.Arguments.Count > 1) { /* parse effect */ }
                break;
            case OpCode.Print:
                // Explicit screen refresh hook for effects. 
                // We handle transitions independently, so we just acknowledge it.
                break;
            case OpCode.Effect:
                // Pre-define effect rules. Ex: effect 2, 10, 1000
                break;
            case OpCode.Quake:
                int amp = 5; int qtime = 500;
                if (inst.Arguments.Count > 0) amp = GetVal(inst.Arguments[0]);
                if (inst.Arguments.Count > 1) qtime = GetVal(inst.Arguments[1]);
                State.QuakeAmplitude = amp;
                State.QuakeTimerMs = qtime;
                break;

            case OpCode.Clr:
                State.Sprites.Clear();
                State.SpriteButtonMap.Clear();
                ClearCompatUiSprites();
                State.TextboxBackgroundSpriteId = -1;
                break;
            case OpCode.TextClear:
                State.CurrentTextBuffer = "";
                State.DisplayedTextLength = 0;
                State.IsWaitingPageClear = false;
                if (State.UseManualTextLayout || !State.CompatAutoUi)
                {
                    if (State.Sprites.TryGetValue(State.TextTargetSpriteId, out var targetTextSprite) && targetTextSprite.Type == SpriteType.Text)
                    {
                        targetTextSprite.Text = "";
                    }
                }
                else
                {
                    if (State.TextboxBackgroundSpriteId >= 0) State.Sprites.Remove(State.TextboxBackgroundSpriteId);
                    State.TextboxBackgroundSpriteId = -1;
                    if (State.Sprites.ContainsKey(State.TextTargetSpriteId)) State.Sprites.Remove(State.TextTargetSpriteId);
                }
                break;

            case OpCode.Textbox:
            case OpCode.SetWindow:
                if (!ValidateArgs(inst, 4)) break;
                if (inst.Arguments.Count >= 4)
                {
                    State.DefaultTextboxX = GetVal(inst.Arguments[0]);
                    State.DefaultTextboxY = GetVal(inst.Arguments[1]);
                    State.DefaultTextboxW = GetVal(inst.Arguments[2]);
                    State.DefaultTextboxH = GetVal(inst.Arguments[3]);
                }
                if (inst.Op == OpCode.SetWindow && inst.Arguments.Count >= 12)
                {
                    // setwindow 32, 390, 28, 4, 20, 20, 0, 4, 20, 0, 1, "#000000", 0, 380, 639, 479 
                    State.DefaultFontSize = GetVal(inst.Arguments[4]);
                    State.DefaultTextboxBgColor = GetString(inst.Arguments[11]);
                }
                break;
            case OpCode.Fontsize:
                if (!ValidateArgs(inst, 1)) break;
                State.DefaultFontSize = GetVal(inst.Arguments[0]);
                break;
            case OpCode.Textcolor:
                if (!ValidateArgs(inst, 1)) break;
                State.DefaultTextColor = GetString(inst.Arguments[0]);
                break;
            case OpCode.TextboxColor:
                if (!ValidateArgs(inst, 2)) break;
                State.DefaultTextboxBgColor = GetString(inst.Arguments[0]);
                State.DefaultTextboxBgAlpha = GetVal(inst.Arguments[1]);
                break;
            case OpCode.TextboxStyle:
                // textbox_style corner, border_width, border_color, border_opacity, pad_x, pad_y, shadow_x, shadow_y, shadow_color, shadow_alpha
                if (!ValidateArgs(inst, 10)) break;
                State.DefaultTextboxCornerRadius = GetVal(inst.Arguments[0]);
                State.DefaultTextboxBorderWidth = GetVal(inst.Arguments[1]);
                State.DefaultTextboxBorderColor = GetString(inst.Arguments[2]);
                State.DefaultTextboxBorderOpacity = GetVal(inst.Arguments[3]);
                State.DefaultTextboxPaddingX = GetVal(inst.Arguments[4]);
                State.DefaultTextboxPaddingY = GetVal(inst.Arguments[5]);
                State.DefaultTextboxShadowOffsetX = GetVal(inst.Arguments[6]);
                State.DefaultTextboxShadowOffsetY = GetVal(inst.Arguments[7]);
                State.DefaultTextboxShadowColor = GetString(inst.Arguments[8]);
                State.DefaultTextboxShadowAlpha = GetVal(inst.Arguments[9]);
                break;
            case OpCode.ChoiceStyle:
                // choice_style width, height, spacing, fontsize, bg_color, bg_alpha, text_color, corner, border_color, border_width, border_opacity, hover_color, pad_x
                if (!ValidateArgs(inst, 13)) break;
                State.ChoiceWidth = GetVal(inst.Arguments[0]);
                State.ChoiceHeight = GetVal(inst.Arguments[1]);
                State.ChoiceSpacing = GetVal(inst.Arguments[2]);
                State.ChoiceFontSize = GetVal(inst.Arguments[3]);
                State.ChoiceBgColor = GetString(inst.Arguments[4]);
                State.ChoiceBgAlpha = GetVal(inst.Arguments[5]);
                State.ChoiceTextColor = GetString(inst.Arguments[6]);
                State.ChoiceCornerRadius = GetVal(inst.Arguments[7]);
                State.ChoiceBorderColor = GetString(inst.Arguments[8]);
                State.ChoiceBorderWidth = GetVal(inst.Arguments[9]);
                State.ChoiceBorderOpacity = GetVal(inst.Arguments[10]);
                State.ChoiceHoverColor = GetString(inst.Arguments[11]);
                State.ChoicePaddingX = GetVal(inst.Arguments[12]);
                break;
            case OpCode.TextboxHide:
            case OpCode.EraseTextWindow:
                State.TextboxVisible = false;
                if (State.TextboxBackgroundSpriteId >= 0 && State.Sprites.ContainsKey(State.TextboxBackgroundSpriteId)) State.Sprites[State.TextboxBackgroundSpriteId].Visible = false;
                if (State.TextTargetSpriteId >= 0 && State.Sprites.ContainsKey(State.TextTargetSpriteId)) State.Sprites[State.TextTargetSpriteId].Visible = false;
                break;
            case OpCode.TextboxShow:
                State.TextboxVisible = true;
                if (State.TextboxBackgroundSpriteId >= 0 && State.Sprites.ContainsKey(State.TextboxBackgroundSpriteId)) State.Sprites[State.TextboxBackgroundSpriteId].Visible = true;
                if (State.TextTargetSpriteId >= 0 && State.Sprites.ContainsKey(State.TextTargetSpriteId)) State.Sprites[State.TextTargetSpriteId].Visible = true;
                break;
                
            case OpCode.TextMode:
                if (!ValidateArgs(inst, 1)) break;
                string mode = GetString(inst.Arguments[0]).ToLowerInvariant();
                Config.Config.TextMode = mode;
                if (mode == "manual")
                {
                    State.UseManualTextLayout = true;
                    State.TextboxVisible = false;
                }
                else if (mode == "nvl")
                {
                    State.UseManualTextLayout = false;
                    State.TextboxVisible = true;
                    State.DefaultTextboxX = 20;
                    State.DefaultTextboxY = 20;
                    State.DefaultTextboxW = State.WindowWidth - 40;
                    State.DefaultTextboxH = State.WindowHeight - 40;
                    State.DefaultTextboxBgAlpha = 150;
                }
                else
                {
                    State.UseManualTextLayout = false;
                    State.TextboxVisible = true;
                    State.DefaultTextboxX = 50;
                    State.DefaultTextboxY = 500;
                    State.DefaultTextboxW = 1180;
                    State.DefaultTextboxH = 200;
                    State.DefaultTextboxBgAlpha = 180;
                }
                break;
            case OpCode.CompatMode:
                if (!ValidateArgs(inst, 1)) break;
                {
                    string compatModeValue = GetString(inst.Arguments[0]).ToLowerInvariant();
                    State.CompatAutoUi = compatModeValue == "on" || compatModeValue == "1" || compatModeValue == "true" || compatModeValue == "legacy";
                }
                break;
            case OpCode.UiTheme:
                if (!ValidateArgs(inst, 1)) break;
                ApplyUiTheme(GetString(inst.Arguments[0]));
                break;
            case OpCode.TextTarget:
                if (!ValidateArgs(inst, 1)) break;
                State.TextTargetSpriteId = GetVal(inst.Arguments[0]);
                break;
            case OpCode.TextSpeed:
                if (!ValidateArgs(inst, 1)) break;
                State.TextSpeedMs = GetVal(inst.Arguments[0]);
                break;
            case OpCode.DefaultSpeed:
                if (!ValidateArgs(inst, 1)) break;
                int spd = GetVal(inst.Arguments[0]);
                Config.Config.DefaultTextSpeedMs = spd;
                Config.Config.GlobalTextSpeedMs = spd;
                Config.Save();
                State.TextSpeedMs = spd;
                break;
            case OpCode.Br:
                State.CurrentTextBuffer += "\n";
                break;
            case OpCode.WaitClick:
                AddBacklogEntry();
                State.State = VmState.WaitingForClick;
                break;
            case OpCode.WaitClickClear:
                AddBacklogEntry();
                State.IsWaitingPageClear = true;
                State.State = VmState.WaitingForClick;
                break;
            case OpCode.Text:
                {
                    string fullText = string.Join(" ", inst.Arguments);
                    // Process variable interpolation in text: ${name}
                    foreach (var kvp in State.StringRegisters)
                        fullText = fullText.Replace($"${{{kvp.Key}}}", kvp.Value);
                    State.CurrentTextBuffer += fullText;

                    if (State.CompatAutoUi && !State.UseManualTextLayout && State.TextboxVisible)
                    {
                        if (State.TextboxBackgroundSpriteId < 0)
                        {
                            State.TextboxBackgroundSpriteId = AllocateCompatUiSpriteId();
                        }

                        State.Sprites[State.TextboxBackgroundSpriteId] = new Sprite
                        {
                            Id = State.TextboxBackgroundSpriteId, Type = SpriteType.Rect, Z = 9000,
                            X = State.DefaultTextboxX, Y = State.DefaultTextboxY,
                            Width = State.DefaultTextboxW, Height = State.DefaultTextboxH,
                            FillColor = State.DefaultTextboxBgColor, FillAlpha = State.DefaultTextboxBgAlpha,
                            CornerRadius = State.DefaultTextboxCornerRadius,
                            BorderColor = State.DefaultTextboxBorderColor,
                            BorderWidth = State.DefaultTextboxBorderWidth,
                            BorderOpacity = State.DefaultTextboxBorderOpacity,
                            ShadowColor = State.DefaultTextboxShadowColor,
                            ShadowOffsetX = State.DefaultTextboxShadowOffsetX,
                            ShadowOffsetY = State.DefaultTextboxShadowOffsetY,
                            ShadowAlpha = State.DefaultTextboxShadowAlpha,
                            IsHovered = false
                        };
                    }

                    if (State.TextTargetSpriteId < 0)
                    {
                        if (!State.CompatAutoUi)
                        {
                            _reporter.Report(new AriaError("text_target が未設定のため text は描画されません。text_target で出力先を指定してください。", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Warning));
                            break;
                        }

                        State.TextTargetSpriteId = AllocateCompatUiSpriteId();
                    }

                    if (!State.Sprites.TryGetValue(State.TextTargetSpriteId, out var textSprite) || textSprite.Type != SpriteType.Text)
                    {
                        if (!State.CompatAutoUi)
                        {
                            _reporter.Report(new AriaError($"text_target({State.TextTargetSpriteId}) のTextスプライトが存在しないため text は描画されません。lsp_text で先に作成してください。", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Warning));
                            break;
                        }

                        textSprite = new Sprite
                        {
                            Id = State.TextTargetSpriteId, Type = SpriteType.Text, Z = 9001,
                            X = State.DefaultTextboxX + State.DefaultTextboxPaddingX,
                            Y = State.DefaultTextboxY + State.DefaultTextboxPaddingY,
                            Width = State.DefaultTextboxW - (State.DefaultTextboxPaddingX * 2),
                            Height = State.DefaultTextboxH - (State.DefaultTextboxPaddingY * 2),
                            FontSize = State.DefaultFontSize,
                            Color = State.DefaultTextColor
                        };
                        State.Sprites[State.TextTargetSpriteId] = textSprite;
                    }

                    textSprite.FontSize = State.DefaultFontSize;
                    textSprite.Color = State.DefaultTextColor;

                    if (State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
                    {
                        int length = Math.Clamp(State.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
                        textSprite.Text = State.CurrentTextBuffer.Substring(0, length);
                        State.State = VmState.WaitingForAnimation;
                    }
                    else
                    {
                        State.DisplayedTextLength = State.CurrentTextBuffer.Length;
                        textSprite.Text = State.CurrentTextBuffer;
                    }
                }
                break;

            case OpCode.Wait:
                State.State = VmState.WaitingForClick;
                break;

            case OpCode.Choice:
                if (!ValidateArgs(inst, 1)) break;
                if (!State.CompatAutoUi)
                {
                    // Core mode: choice is a state command only (no implicit drawing).
                    if (inst.Arguments.Count > 0 && inst.Arguments[0].StartsWith("%"))
                    {
                        State.ButtonResultRegister = inst.Arguments[0];
                    }
                    else
                    {
                        State.ButtonResultRegister = "0";
                    }

                    if (inst.Arguments.Count > 0 && !inst.Arguments[0].StartsWith("%"))
                    {
                        _reporter.Report(new AriaError("compat_mode off では choice の文字列引数は描画に使われません。lsp/spbtn/btnwait でUIを構築してください。", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Warning));
                    }

                    if (!HasAnyVisibleButton())
                    {
                        _reporter.Report(new AriaError("choice は待機先のボタンが存在しないため実行できません。spbtn/btn で先にボタンを作成してください。", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Error));
                        break;
                    }

                    State.State = VmState.WaitingForButton;
                    break;
                }

                ClearCompatUiSprites();

                int count = inst.Arguments.Count;
                int h = State.ChoiceHeight;
                int spacing = State.ChoiceSpacing;
                int totalH = (h + spacing) * count - spacing;
                int startY = (State.WindowHeight - totalH) / 2;
                int startX = (State.WindowWidth - State.ChoiceWidth) / 2;

                for (int i = 0; i < count; i++)
                {
                    int y = startY + (h + spacing) * i;
                    int rectId = AllocateCompatUiSpriteId();
                    int textId = AllocateCompatUiSpriteId();
                    
                    State.Sprites[rectId] = new Sprite
                    {
                        Id = rectId, Type = SpriteType.Rect, Z = 9500,
                        X = startX, Y = y, Width = State.ChoiceWidth, Height = h,
                        FillColor = State.ChoiceBgColor, FillAlpha = State.ChoiceBgAlpha, IsButton = true,
                        CornerRadius = State.ChoiceCornerRadius,
                        BorderColor = State.ChoiceBorderColor,
                        BorderWidth = State.ChoiceBorderWidth,
                        BorderOpacity = State.ChoiceBorderOpacity,
                        HoverFillColor = State.ChoiceHoverColor
                    };
                    State.SpriteButtonMap[rectId] = i;
                    TrackCompatUiSprite(rectId);

                    State.Sprites[textId] = new Sprite
                    {
                        Id = textId, Type = SpriteType.Text, Z = 9501,
                        Text = GetString(inst.Arguments[i]),
                        X = startX + State.ChoicePaddingX,
                        Y = y,
                        Width = State.ChoiceWidth - (State.ChoicePaddingX * 2),
                        Height = h,
                        FontSize = State.ChoiceFontSize,
                        Color = State.ChoiceTextColor,
                        TextAlign = "center",
                        TextVAlign = "center"
                    };
                    TrackCompatUiSprite(textId);
                }
                State.State = VmState.WaitingForButton;
                break;

            case OpCode.Let:
            case OpCode.Mov:
                if (!ValidateArgs(inst, 2)) break;
                if (inst.Arguments[0].StartsWith("$") || inst.Arguments[1].StartsWith("\""))
                    SetStr(inst.Arguments[0], GetString(inst.Arguments[1]));
                else
                    SetReg(inst.Arguments[0], GetVal(inst.Arguments[1].ToString()));
                break;
            case OpCode.Add:
                if (!ValidateArgs(inst, 2)) break;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + GetVal(inst.Arguments[1]));
                break;
            case OpCode.Sub:
                if (!ValidateArgs(inst, 2)) break;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - GetVal(inst.Arguments[1]));
                break;
            case OpCode.Mul:
                if (!ValidateArgs(inst, 2)) break;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) * GetVal(inst.Arguments[1]));
                break;
            case OpCode.Div:
                if (!ValidateArgs(inst, 2)) break;
                {
                    int div = GetVal(inst.Arguments[1]);
                    SetReg(inst.Arguments[0], div != 0 ? GetReg(inst.Arguments[0]) / div : 0);
                }
                break;
            case OpCode.Mod:
                if (!ValidateArgs(inst, 2)) break;
                {
                    int mod = GetVal(inst.Arguments[1]);
                    SetReg(inst.Arguments[0], mod != 0 ? GetReg(inst.Arguments[0]) % mod : 0);
                }
                break;
            case OpCode.Cmp:
                if (!ValidateArgs(inst, 2)) break;
                {
                    int lhs = GetReg(inst.Arguments[0]);
                    int rhs = GetVal(inst.Arguments[1]);
                    State.CompareFlag = lhs == rhs ? 0 : (lhs > rhs ? 1 : -1);
                }
                break;
            case OpCode.Beq:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CompareFlag == 0) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Bne:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CompareFlag != 0) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Bgt:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CompareFlag == 1) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Blt:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CompareFlag == -1) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Jmp:
                if (!ValidateArgs(inst, 1)) break;
                JumpTo(inst.Arguments[0]);
                break;
            case OpCode.JumpIfFalse:
                if (!ValidateArgs(inst, 1)) break;
                if (!EvaluateCondition(inst.Condition)) JumpTo(inst.Arguments[0]);
                break;
                
            case OpCode.Delay:
                if (!ValidateArgs(inst, 1)) break;
                State.DelayTimerMs = GetVal(inst.Arguments[0]);
                State.State = VmState.WaitingForDelay;
                break;
            case OpCode.Rnd:
                if (!ValidateArgs(inst, 3)) break;
                SetReg(inst.Arguments[0], Raylib_cs.Raylib.GetRandomValue(GetVal(inst.Arguments[1]), GetVal(inst.Arguments[2])));
                break;
            case OpCode.Inc:
                if (!ValidateArgs(inst, 1)) break;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + 1);
                break;
            case OpCode.Dec:
                if (!ValidateArgs(inst, 1)) break;
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - 1);
                break;
            case OpCode.For:
                if (!ValidateArgs(inst, 3)) break;
                SetReg(inst.Arguments[0], GetVal(inst.Arguments[1]));
                State.LoopStack.Push(new LoopState { 
                    PC = State.ProgramCounter, 
                    VarName = inst.Arguments[0], 
                    TargetValue = GetVal(inst.Arguments[2]) 
                });
                break;
            case OpCode.Next:
                if (State.LoopStack.Count > 0)
                {
                    var loop = State.LoopStack.Peek();
                    int currVal = GetReg(loop.VarName) + 1;
                    SetReg(loop.VarName, currVal);
                    if (currVal <= loop.TargetValue)
                    {
                        State.ProgramCounter = loop.PC;
                    }
                    else
                    {
                        State.LoopStack.Pop();
                    }
                }
                break;
            case OpCode.GetTimer:
                if (!ValidateArgs(inst, 1)) break;
                SetReg(inst.Arguments[0], (int)State.ScriptTimerMs);
                break;
            case OpCode.ResetTimer:
                State.ScriptTimerMs = 0;
                break;
            case OpCode.WaitTimer:
                if (!ValidateArgs(inst, 1)) break;
                // Can emulate via wait logic, actually let's just cheat and do a simple wait
                State.DelayTimerMs = Math.Max(0, GetVal(inst.Arguments[0]) - State.ScriptTimerMs);
                State.State = VmState.WaitingForDelay;
                break;
            case OpCode.RightMenu:
                if (inst.Arguments.Count == 1 && inst.Arguments[0].StartsWith("*", StringComparison.Ordinal))
                {
                    State.RightMenuLabel = inst.Arguments[0];
                    break;
                }
                State.RightMenuEntries.Clear();
                for (int i = 0; i + 1 < inst.Arguments.Count; i += 2)
                {
                    State.RightMenuEntries.Add(new RightMenuEntry
                    {
                        Label = GetString(inst.Arguments[i]),
                        Action = inst.Arguments[i + 1].TrimStart('*').ToLowerInvariant()
                    });
                }
                break;
            case OpCode.ClickCursor:
                if (inst.Arguments.Count > 0 && inst.Arguments[0].Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    State.ShowClickCursor = false;
                    break;
                }
                State.ShowClickCursor = true;
                if (inst.Arguments.Count > 0) State.ClickCursorPath = GetString(inst.Arguments[0]);
                if (inst.Arguments.Count > 1) State.ClickCursorOffsetX = GetVal(inst.Arguments[1]);
                if (inst.Arguments.Count > 2) State.ClickCursorOffsetY = GetVal(inst.Arguments[2]);
                break;
            case OpCode.Backlog:
                if (inst.Arguments.Count > 0) State.BacklogEnabled = IsOn(inst.Arguments[0]);
                break;
            case OpCode.KidokuMode:
                if (inst.Arguments.Count > 0) State.KidokuMode = GetVal(inst.Arguments[0]) != 0;
                break;
            case OpCode.SkipMode:
                if (inst.Arguments.Count > 0)
                {
                    string skipMode = GetString(inst.Arguments[0]).ToLowerInvariant();
                    State.SkipUnread = skipMode is "all" or "unread" or "1" or "on";
                    MarkPersistentDirty();
                }
                break;
            case OpCode.SystemButton:
                if (!ValidateArgs(inst, 2)) break;
                SetSystemButton(GetString(inst.Arguments[0]), IsOn(inst.Arguments[1]));
                break;
            case OpCode.Save:
                if (!ValidateArgs(inst, 1)) break;
                NormalizeRuntimeTextSprites();
                Saves.Save(GetVal(inst.Arguments[0]), State, _currentScriptFile);
                break;
            case OpCode.Load:
                if (!ValidateArgs(inst, 1)) break;
                var (dat, suc) = Saves.Load(GetVal(inst.Arguments[0]));
                if (suc && dat != null)
                {
                    ApplyLoadedState(dat);
                    if (_labels.ContainsKey("load_restore")) JumpTo("*load_restore"); // optional hook label in game script
                }
                break;

            case OpCode.PlayBgm:
                if (!ValidateArgs(inst, 1)) break;
                State.CurrentBgm = GetString(inst.Arguments[0]);
                break;
            case OpCode.StopBgm:
                State.CurrentBgm = "";
                break;
            case OpCode.PlaySe:
            case OpCode.Dwave:
                if (!ValidateArgs(inst, 1)) break;
                // dwave 0, "file"
                string seFile = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]);
                State.PendingSe.Add(seFile);
                break;
            case OpCode.BgmVol:
                if (!ValidateArgs(inst, 1)) break;
                State.BgmVolume = GetVal(inst.Arguments[0]);
                break;
            case OpCode.SeVol:
                if (!ValidateArgs(inst, 1)) break;
                State.SeVolume = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BgmFade:
                // implement bgm fade signal
                State.CurrentBgm = ""; // shortcut for now
                break;
            case OpCode.YesNoBox:
                if (!State.CompatAutoUi)
                {
                    _reporter.Report(new AriaError("yesnobox の自動UI生成は compat_mode off で無効です。描画命令と btnwait で実装してください。", inst.SourceLine, _currentScriptFile, AriaErrorLevel.Warning));
                    break;
                }

                // var, msg, title
                // We'll emulate it by creating choices on screen
                if (!ValidateArgs(inst, 3)) break;
                ClearCompatUiSprites();

                int yesNoW = Math.Max(160, State.ChoiceWidth / 3);
                int yesNoH = State.ChoiceHeight;
                int yesNoGap = 36;
                int centerX = State.WindowWidth / 2;
                int btnY = State.WindowHeight / 2 + 40;
                int leftX = centerX - yesNoW - (yesNoGap / 2);
                int rightX = centerX + (yesNoGap / 2);

                int yesRectId = AllocateCompatUiSpriteId();
                int yesTextId = AllocateCompatUiSpriteId();
                int noRectId = AllocateCompatUiSpriteId();
                int noTextId = AllocateCompatUiSpriteId();
                int msgRectId = AllocateCompatUiSpriteId();
                int msgTextId = AllocateCompatUiSpriteId();

                State.Sprites[yesRectId] = new Sprite
                {
                    Id = yesRectId, Type = SpriteType.Rect, Z = 9500, X = leftX, Y = btnY, Width = yesNoW, Height = yesNoH,
                    FillColor = State.ChoiceBgColor, FillAlpha = State.ChoiceBgAlpha, IsButton = true,
                    CornerRadius = State.ChoiceCornerRadius, BorderColor = State.ChoiceBorderColor, BorderWidth = State.ChoiceBorderWidth,
                    BorderOpacity = State.ChoiceBorderOpacity, HoverFillColor = State.ChoiceHoverColor
                };
                State.SpriteButtonMap[yesRectId] = 1;
                TrackCompatUiSprite(yesRectId);

                State.Sprites[yesTextId] = new Sprite
                {
                    Id = yesTextId, Type = SpriteType.Text, Z = 9501, Text = "Yes",
                    X = leftX + State.ChoicePaddingX, Y = btnY,
                    Width = yesNoW - (State.ChoicePaddingX * 2), Height = yesNoH,
                    FontSize = State.ChoiceFontSize, Color = State.ChoiceTextColor, TextAlign = "center", TextVAlign = "center"
                };
                TrackCompatUiSprite(yesTextId);

                State.Sprites[noRectId] = new Sprite
                {
                    Id = noRectId, Type = SpriteType.Rect, Z = 9500, X = rightX, Y = btnY, Width = yesNoW, Height = yesNoH,
                    FillColor = State.ChoiceBgColor, FillAlpha = State.ChoiceBgAlpha, IsButton = true,
                    CornerRadius = State.ChoiceCornerRadius, BorderColor = State.ChoiceBorderColor, BorderWidth = State.ChoiceBorderWidth,
                    BorderOpacity = State.ChoiceBorderOpacity, HoverFillColor = State.ChoiceHoverColor
                };
                State.SpriteButtonMap[noRectId] = 0;
                TrackCompatUiSprite(noRectId);

                State.Sprites[noTextId] = new Sprite
                {
                    Id = noTextId, Type = SpriteType.Text, Z = 9501, Text = "No",
                    X = rightX + State.ChoicePaddingX, Y = btnY,
                    Width = yesNoW - (State.ChoicePaddingX * 2), Height = yesNoH,
                    FontSize = State.ChoiceFontSize, Color = State.ChoiceTextColor, TextAlign = "center", TextVAlign = "center"
                };
                TrackCompatUiSprite(noTextId);

                int msgW = Math.Min(State.WindowWidth - 120, State.ChoiceWidth + 200);
                int msgX = (State.WindowWidth - msgW) / 2;
                int msgH = 110;
                int msgY = btnY - msgH - 30;
                State.Sprites[msgRectId] = new Sprite
                {
                    Id = msgRectId, Type = SpriteType.Rect, Z = 9500, X = msgX, Y = msgY, Width = msgW, Height = msgH,
                    FillColor = State.DefaultTextboxBgColor, FillAlpha = State.DefaultTextboxBgAlpha,
                    CornerRadius = State.DefaultTextboxCornerRadius, BorderColor = State.DefaultTextboxBorderColor,
                    BorderWidth = State.DefaultTextboxBorderWidth, BorderOpacity = State.DefaultTextboxBorderOpacity
                };
                TrackCompatUiSprite(msgRectId);

                State.Sprites[msgTextId] = new Sprite
                {
                    Id = msgTextId, Type = SpriteType.Text, Z = 9501, Text = GetString(inst.Arguments[1]),
                    X = msgX + State.DefaultTextboxPaddingX, Y = msgY + State.DefaultTextboxPaddingY,
                    Width = msgW - (State.DefaultTextboxPaddingX * 2), Height = msgH - (State.DefaultTextboxPaddingY * 2),
                    FontSize = State.DefaultFontSize, Color = State.DefaultTextColor
                };
                TrackCompatUiSprite(msgTextId);
                State.State = VmState.WaitingForButton;
                // Target reg is mapped internally, choices will set r0 to 0 or 1.
                // We should push the target var name for when it resumes, but keeping it simple for now: writes to r0.
                break;

            case OpCode.FadeIn:
                State.State = VmState.FadingIn;
                State.IsFading = true;
                State.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                break;
            case OpCode.FadeOut:
                State.State = VmState.FadingOut;
                State.IsFading = true;
                State.FadeDurationMs = inst.Arguments.Count > 0 ? GetVal(inst.Arguments[0]) : 1000;
                break;
            case OpCode.End:
                State.State = VmState.Ended;
                State.RequestClose = true;
                break;

            // チャプター選択システム
            case OpCode.ChapterSelect:
                {
                    var chapters = ChapterManager.GetAvailableChapters();
                    int chapterStartY = 200;

                    // 前のチャプターボタンをクリア
                    for (int i = 2000; i < 2100; i++)
                    {
                        State.Sprites.Remove(i);
                        State.SpriteButtonMap.Remove(i);
                    }

                    for (int i = 0; i < chapters.Count; i++)
                    {
                        int cardId = 2000 + i;
                        var ch = chapters[i];

                        // フラグからロック状態を取得
                        bool isUnlocked = ch.IsUnlocked;
                        string flagKey = $"chapter{ch.Id}_unlocked";
                        if (State.Flags.TryGetValue(flagKey, out bool flagValue))
                        {
                            isUnlocked = flagValue;
                        }

                        // 背景カード（スタイリッシュ化）
                        State.Sprites[cardId] = new Sprite
                        {
                            Id = cardId,
                            Type = SpriteType.Rect,
                            X = 340,
                            Y = chapterStartY + (i * 120),
                            Width = 600,
                            Height = 100,
                            FillColor = isUnlocked ? "#2a2a3e" : "#1a1a2e",
                            FillAlpha = 255,
                            IsButton = isUnlocked,
                            CornerRadius = 12,
                            BorderColor = isUnlocked ? "#4a4a6e" : "#2a2a4e",
                            BorderWidth = 2,
                            ShadowColor = "#000000",
                            ShadowOffsetX = 4,
                            ShadowOffsetY = 4,
                            HoverFillColor = "#3a3a5e",
                            HoverScale = 1.02f
                        };

                        // タイトルテキスト（スタイリッシュ化）
                        State.Sprites[cardId + 1] = new Sprite
                        {
                            Id = cardId + 1,
                            Type = SpriteType.Text,
                            Text = ch.Title,
                            X = 360,
                            Y = chapterStartY + (i * 120) + 25,
                            FontSize = 24,
                            Color = isUnlocked ? "#ffffff" : "#666688",
                            TextShadowColor = "#000000",
                            TextShadowX = 2,
                            TextShadowY = 2
                        };

                        // 説明テキスト（スタイリッシュ化）
                        State.Sprites[cardId + 2] = new Sprite
                        {
                            Id = cardId + 2,
                            Type = SpriteType.Text,
                            Text = ch.Description,
                            X = 360,
                            Y = chapterStartY + (i * 120) + 60,
                            FontSize = 16,
                            Color = isUnlocked ? "#aaaaee" : "#555577"
                        };

                        // ボタンマッピング（重要）
                        if (isUnlocked)
                        {
                            State.SpriteButtonMap[cardId] = ch.Id;
                        }
                    }
                }
                break;

            case OpCode.UnlockChapter:
                if (!ValidateArgs(inst, 1)) break;
                int chapterId = GetVal(inst.Arguments[0]);
                ChapterManager.UnlockChapter(chapterId);
                ChapterManager.SaveChapters();
                break;

            case OpCode.ChapterThumbnail:
                // チャプターサムネイルの設定
                if (!ValidateArgs(inst, 2)) break;
                {
                    int thumbChapterId = GetVal(inst.Arguments[0]);
                    string thumbPath = GetString(inst.Arguments[1]);
                    var chapter = ChapterManager.GetChapter(thumbChapterId);
                    if (chapter != null)
                    {
                        chapter.ThumbnailPath = thumbPath;
                        ChapterManager.SaveChapters();
                    }
                }
                break;

            case OpCode.ChapterCard:
                // カスタムチャプターカードの作成
                if (!ValidateArgs(inst, 5)) break;
                {
                    int cardId = GetVal(inst.Arguments[0]);
                    string title = GetString(inst.Arguments[1]);
                    string description = inst.Arguments.Count > 2 ? GetString(inst.Arguments[2]) : "";
                    int x = GetVal(inst.Arguments[3]);
                    int y = GetVal(inst.Arguments[4]);

                    State.Sprites[cardId] = new Sprite
                    {
                        Id = cardId,
                        Type = SpriteType.Rect,
                        X = x,
                        Y = y,
                        Width = 600,
                        Height = 100,
                        FillColor = "#333333",
                        FillAlpha = 255,
                        IsButton = true
                    };

                    State.Sprites[cardId + 1] = new Sprite
                    {
                        Id = cardId + 1,
                        Type = SpriteType.Text,
                        Text = title,
                        X = x + 20,
                        Y = y + 20,
                        FontSize = 24,
                        Color = "#ffffff"
                    };

                    if (!string.IsNullOrEmpty(description))
                    {
                        State.Sprites[cardId + 2] = new Sprite
                        {
                            Id = cardId + 2,
                            Type = SpriteType.Text,
                            Text = description,
                            X = x + 20,
                            Y = y + 55,
                            FontSize = 16,
                            Color = "#aaaaaa"
                        };
                    }
                }
                break;

            case OpCode.ChapterProgress:
                // チャプター進捗の更新
                if (!ValidateArgs(inst, 2)) break;
                {
                    int progressChapterId = GetVal(inst.Arguments[0]);
                    int progress = GetVal(inst.Arguments[1]);
                    ChapterManager.UpdateProgress(progressChapterId, progress);
                    ChapterManager.SaveChapters();
                }
                break;

            // キャラクター操作
            case OpCode.CharLoad:
                // キャラクターデータのロード
                if (!ValidateArgs(inst, 1)) break;
                {
                    string charDataFile = GetString(inst.Arguments[0]);
                    CharacterManager.LoadCharacterData(charDataFile);
                }
                break;

            case OpCode.CharShow:
                {
                    if (!ValidateArgs(inst, 1)) break;
                    string charId = inst.Arguments[0];
                    string expression = inst.Arguments.Count > 1 ? inst.Arguments[1] : "normal";
                    string pose = inst.Arguments.Count > 2 ? inst.Arguments[2] : "default";
                    CharacterManager.ShowCharacter(charId, expression, pose);
                    break;
                }

            case OpCode.CharHide:
                if (!ValidateArgs(inst, 1)) break;
                string hideCharId = inst.Arguments[0];
                int hideDuration = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 300;
                CharacterManager.HideCharacter(hideCharId, hideDuration);
                break;

            case OpCode.CharMove:
                if (!ValidateArgs(inst, 3)) break;
                string moveCharId = inst.Arguments[0];
                int moveX = GetVal(inst.Arguments[1]);
                int moveY = GetVal(inst.Arguments[2]);
                int moveDuration = inst.Arguments.Count > 3 ? GetVal(inst.Arguments[3]) : 500;
                CharacterManager.MoveCharacter(moveCharId, moveX, moveY, moveDuration);
                break;

            case OpCode.CharExpression:
                {
                    if (!ValidateArgs(inst, 2)) break;
                    string exprCharId = inst.Arguments[0];
                    string expression = inst.Arguments[1];
                    CharacterManager.ChangeExpression(exprCharId, expression);
                    break;
                }

            case OpCode.CharPose:
                {
                    if (!ValidateArgs(inst, 2)) break;
                    string poseCharId = inst.Arguments[0];
                    string pose = inst.Arguments[1];
                    CharacterManager.ChangePose(poseCharId, pose);
                    break;
                }

            case OpCode.CharZ:
                if (!ValidateArgs(inst, 2)) break;
                string zCharId = inst.Arguments[0];
                int z = GetVal(inst.Arguments[1]);
                CharacterManager.SetCharacterZ(zCharId, z);
                break;

            case OpCode.CharScale:
                if (!ValidateArgs(inst, 2)) break;
                string scaleCharId = inst.Arguments[0];
                float scale = GetFloat(inst.Arguments[1], inst, 1.0f);
                CharacterManager.SetCharacterScale(scaleCharId, scale);
                break;

            // ゲームフロー
            case OpCode.ChangeScene:
                if (!ValidateArgs(inst, 1)) break;
                string sceneStr = inst.Arguments[0].ToLowerInvariant();
                GameScene newScene = sceneStr switch
                {
                    "titlescreen" => GameScene.TitleScreen,
                    "chapterselect" => GameScene.ChapterSelect,
                    "gameplay" => GameScene.GamePlay,
                    "systemmenu" => GameScene.SystemMenu,
                    "saveloadmenu" => GameScene.SaveLoadMenu,
                    "settings" => GameScene.Settings,
                    "gallery" => GameScene.Gallery,
                    _ => GameScene.TitleScreen
                };
                GameFlow.TransitionTo(newScene, this);
                break;

            case OpCode.ReturnScene:
                GameFlow.GoBack(this);
                break;

            case OpCode.SetSceneData:
                {
                    if (!ValidateArgs(inst, 2)) break;
                    string dataKey = inst.Arguments[0];
                    string dataValue = GetString(inst.Arguments[1]);
                    State.SceneData[dataKey] = dataValue;
                    break;
                }

            case OpCode.GetSceneData:
                {
                    if (!ValidateArgs(inst, 1)) break;
                    string getDataKey = inst.Arguments[0];
                    if (State.SceneData.TryGetValue(getDataKey, out object? retrievedValue) && retrievedValue != null)
                    {
                        string strValue = retrievedValue.ToString() ?? "";
                        SetReg(inst.Arguments[0], int.TryParse(strValue, out int numVal) ? numVal : 0);
                    }
                    else
                    {
                        SetReg(inst.Arguments[0], 0);
                    }
                    break;
                }

            // フラグ管理システム
            case OpCode.SetFlag:
            case OpCode.GetFlag:
            case OpCode.ClearFlag:
            case OpCode.ToggleFlag:
            case OpCode.SetPFlag:
            case OpCode.GetPFlag:
            case OpCode.ClearPFlag:
            case OpCode.TogglePFlag:
            case OpCode.SetSFlag:
            case OpCode.GetSFlag:
            case OpCode.ClearSFlag:
            case OpCode.ToggleSFlag:
            case OpCode.SetVFlag:
            case OpCode.GetVFlag:
            case OpCode.ClearVFlag:
            case OpCode.ToggleVFlag:
            case OpCode.IncCounter:
            case OpCode.DecCounter:
            case OpCode.SetCounter:
            case OpCode.GetCounter:
                _flagCommands.Execute(inst);
                break;

            // チャプター定義（スクリプト主導）
            case OpCode.DefChapter:
                State.CurrentChapterDefinition = new ChapterInfo();
                break;

            case OpCode.ChapterId:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Id = GetVal(inst.Arguments[0]);
                break;

            case OpCode.ChapterTitle:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Title = GetString(inst.Arguments[0]);
                break;

            case OpCode.ChapterDesc:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Description = GetString(inst.Arguments[0]);
                break;

            case OpCode.ChapterScript:
                if (!ValidateArgs(inst, 1)) break;
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.ScriptPath = GetString(inst.Arguments[0]);
                break;

            case OpCode.EndChapter:
                if (State.CurrentChapterDefinition != null)
                {
                    ChapterManager.AddChapter(State.CurrentChapterDefinition);
                    State.CurrentChapterDefinition = null;
                }
                break;

            case OpCode.FontFilter:
                if (!ValidateArgs(inst, 1)) break;
                string filterType = GetString(inst.Arguments[0]).ToLowerInvariant();
                if (filterType == "bilinear")
                    State.FontFilter = TextureFilter.Bilinear;
                else if (filterType == "trilinear")
                    State.FontFilter = TextureFilter.Trilinear;
                else if (filterType == "point")
                    State.FontFilter = TextureFilter.Point;
                else if (filterType == "anisotropic")
                    State.FontFilter = TextureFilter.Trilinear; // このバージョンではAnisotropic未定義のためフォールバック
                else
                    State.FontFilter = TextureFilter.Bilinear;
                break;
        }
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
            Counters = new Dictionary<string, int>(State.Counters, StringComparer.OrdinalIgnoreCase),
            ReadKeys = State.ReadKeys.ToList()
        });
        Config.Save();
        _persistentDirty = false;
        _persistentSaveTimerMs = 0f;
    }

    private void AddBacklogEntry()
    {
        if (!State.BacklogEnabled) return;
        string text = State.CurrentTextBuffer.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (State.TextHistory.Count == 0 || State.TextHistory[^1] != text)
        {
            State.TextHistory.Add(text);
            if (State.TextHistory.Count > 300) State.TextHistory.RemoveAt(0);
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

    private int AllocateCompatUiSpriteId()
    {
        while (State.Sprites.ContainsKey(_nextCompatUiSpriteId))
        {
            _nextCompatUiSpriteId++;
        }
        return _nextCompatUiSpriteId++;
    }

    private void TrackCompatUiSprite(int spriteId)
    {
        _activeCompatUiSpriteIds.Add(spriteId);
    }

    private void ClearCompatUiSprites()
    {
        if (_activeCompatUiSpriteIds.Count == 0) return;

        foreach (int id in _activeCompatUiSpriteIds)
        {
            State.Sprites.Remove(id);
            State.SpriteButtonMap.Remove(id);
        }

        _activeCompatUiSpriteIds.Clear();
    }

    private bool HasAnyVisibleButton()
    {
        return State.Sprites.Values.Any(s => s.Visible && s.IsButton);
    }

    private void ApplyUiTheme(string themeNameRaw)
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
    private int GetReg(string reg) => State.Registers.TryGetValue(reg.TrimStart('%').ToLowerInvariant(), out int v) ? v : 0;
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
    
    private void SetStr(string reg, string val) => State.StringRegisters[reg.TrimStart('$').ToLowerInvariant()] = val;
    internal string GetString(string valStr)
    {
        if (valStr.StartsWith("$"))
        {
            string key = valStr.TrimStart('$').ToLowerInvariant();
            return State.StringRegisters.TryGetValue(key, out string? v) ? (v ?? "") : "";
        }
        return valStr;
    }

    private bool IsOn(string token)
    {
        string value = GetString(token).ToLowerInvariant();
        if (value is "on" or "true" or "yes") return true;
        if (value is "off" or "false" or "no") return false;
        return GetVal(token) != 0;
    }

    private void SetSystemButton(string name, bool visible)
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

    /// <summary>
    /// ゲームをセーブします。
    /// </summary>
    public void SaveGame(int slot)
    {
        NormalizeRuntimeTextSprites();
        Saves.Save(slot, State, _currentScriptFile);
        Console.WriteLine($"Game saved to slot {slot}");
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

            Console.WriteLine($"Game loaded from slot {slot}");
            
            // "load_restore"ラベルが存在すればそちらに飛ぶことで初期化をスクリプトから行う
            if (_labels.ContainsKey("load_restore")) JumpTo("*load_restore");
            else
            {
                // Fallback: If no custom setup is provided, attempt to update the renderer if needed
            }
        }
        else
        {
            Console.WriteLine($"Failed to load game from slot {slot}");
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
        State.TextTimerMs = 0f;
        State.IsWaitingPageClear = loaded.IsWaitingPageClear;
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
        _nextCompatUiSpriteId = Math.Max(50000, State.Sprites.Count == 0 ? 50000 : State.Sprites.Keys.Max() + 1);
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
            text.Visible = State.TextboxVisible;
            text.Z = 9001;
            int length = Math.Clamp(State.DisplayedTextLength, 0, State.CurrentTextBuffer.Length);
            text.Text = State.CurrentTextBuffer.Substring(0, length);
        }
    }
}





