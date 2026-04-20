using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Rendering;
using AriaEngine.UI;
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

    private Dictionary<string, string> _characterPaths = new();

    public VirtualMachine(ErrorReporter reporter, TweenManager tweens, SaveManager saves, ConfigManager config)
    {
        _reporter = reporter;
        Tweens = tweens;
        Saves = saves;
        Config = config;
        State = new GameState();
        Menu = new MenuSystem(this);

        // Load initial config into GameState
        State.TextSpeedMs = Config.Config.GlobalTextSpeedMs;
        State.BgmVolume = Config.Config.BgmVolume;
        State.SeVolume = Config.Config.SeVolume;

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
            int resultValue = 0;

            if (buttonId >= 9100 && buttonId < 9200)
            {
                // Choice buttons (rect IDs)
                resultValue = buttonId - 9100;
                for (int i = 9100; i < 9300; i++) State.Sprites.Remove(i);
            }
            else if (buttonId >= 9200 && buttonId < 9300)
            {
                // Choice buttons (text IDs) - map to corresponding choice index
                resultValue = buttonId - 9200;
                for (int i = 9100; i < 9300; i++) State.Sprites.Remove(i);
            }
            else
            {
                // Check if this sprite is mapped via spbtn
                if (State.SpriteButtonMap.TryGetValue(buttonId, out int mappedValue))
                {
                    resultValue = mappedValue;
                }
                else
                {
                    resultValue = buttonId;
                }
            }

            // Set both registers for compatibility
            State.Registers["0"] = resultValue;
            State.Registers["r0"] = resultValue;

            State.State = VmState.Running;
            State.ButtonTimeoutMs = 0;
            State.ButtonTimer = 0f;
        }
    }

    public void SignalTimeout()
    {
        if (State.State == VmState.WaitingForButton)
        {
            State.Registers["0"] = -1;
            State.Registers["r0"] = -1;
            State.State = VmState.Running;
            State.ButtonTimeoutMs = 0;
            State.ButtonTimer = 0f;
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

            if (State.Sprites.TryGetValue(9001, out var txtSprite))
            {
                txtSprite.Text = State.CurrentTextBuffer.Substring(0, State.DisplayedTextLength);
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
                ValidateArgs(inst, 3);
                State.WindowWidth = int.Parse(inst.Arguments[0]);
                State.WindowHeight = int.Parse(inst.Arguments[1]);
                State.Title = inst.Arguments[2];
                break;
            case OpCode.Caption:
                ValidateArgs(inst, 1);
                State.Title = inst.Arguments[0];
                break;
            case OpCode.Font:
                ValidateArgs(inst, 1);
                State.FontPath = inst.Arguments[0];
                break;
            case OpCode.FontAtlasSize:
                ValidateArgs(inst, 1);
                State.FontAtlasSize = int.Parse(inst.Arguments[0]);
                break;
            case OpCode.Script:
                ValidateArgs(inst, 1);
                State.MainScript = inst.Arguments[0];
                break;
            case OpCode.Debug:
                if (inst.Arguments.Count > 0 && inst.Arguments[0] == "on") State.DebugMode = true;
                else State.DebugMode = false;
                break;
            
            // Subroutines and Control Flow
            case OpCode.Gosub:
                ValidateArgs(inst, 1);
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
                ValidateArgs(inst, 2);
                // We use dynamic mapping via SetReg to preserve values, or let the parser rewrite.
                // In this basic VM, we will just use variables natively. Alias sets initial mapping or just ignores.
                break;
            case OpCode.SystemCall:
                ValidateArgs(inst, 1);
                switch (inst.Arguments[0].ToLowerInvariant())
                {
                    case "rmenu":
                    case "lookback":
                    case "load":
                        // Minimal mock support
                        break;
                }
                break;

            // Sprites
            case OpCode.Lsp:
                ValidateArgs(inst, 4);
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
                ValidateArgs(inst, 4);
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
                ValidateArgs(inst, 5);
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
                ValidateArgs(inst, 1);
                {
                    int id = GetVal(inst.Arguments[0]);
                    if (id == -1) State.Sprites.Clear();
                    else State.Sprites.Remove(id);
                }
                break;
            case OpCode.Vsp:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var vsp))
                {
                    vsp.Visible = int.TryParse(inst.Arguments[1], out int v) ? v != 0 : inst.Arguments[1] == "on";
                }
                break;
            case OpCode.Msp:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var msp))
                {
                    msp.X = GetVal(inst.Arguments[1]); msp.Y = GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.MspRel:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var mspr))
                {
                    mspr.X += GetVal(inst.Arguments[1]); mspr.Y += GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.SpZ:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spz)) spz.Z = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpAlpha:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spa)) spa.Opacity = GetVal(inst.Arguments[1]) / 255.0f;
                break;
            case OpCode.SpScale:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsc))
                {
                    spsc.ScaleX = float.Parse(inst.Arguments[1]); spsc.ScaleY = float.Parse(inst.Arguments[2]);
                }
                break;
            case OpCode.SpFontsize:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spf)) spf.FontSize = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpColor:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spc)) spc.Color = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpFill:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spfl))
                {
                    spfl.FillColor = GetString(inst.Arguments[1]); spfl.FillAlpha = GetVal(inst.Arguments[2]);
                }
                break;

            case OpCode.Btn:
                ValidateArgs(inst, 1);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var btn)) btn.IsButton = true;
                // Also add mapping for compatibility with btnwait
                if (!State.SpriteButtonMap.ContainsKey(GetVal(inst.Arguments[0])))
                    State.SpriteButtonMap[GetVal(inst.Arguments[0])] = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BtnArea:
                ValidateArgs(inst, 5);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var ba))
                {
                    ba.IsButton = true;
                    ba.ClickAreaX = GetVal(inst.Arguments[1]); ba.ClickAreaY = GetVal(inst.Arguments[2]);
                    ba.ClickAreaW = GetVal(inst.Arguments[3]); ba.ClickAreaH = GetVal(inst.Arguments[4]);
                }
                break;
            case OpCode.BtnClear:
                ValidateArgs(inst, 1);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var bcl)) bcl.IsButton = false;
                break;
            case OpCode.BtnClearAll:
                foreach (var s in State.Sprites.Values) s.IsButton = false;
                State.SpriteButtonMap.Clear();
                break;
            case OpCode.SpBtn:
                ValidateArgs(inst, 2);
                {
                    int sId = GetVal(inst.Arguments[0]);
                    int bId = GetVal(inst.Arguments[1]);
                    if (State.Sprites.TryGetValue(sId, out var spb)) spb.IsButton = true;
                    State.SpriteButtonMap[sId] = bId;
                }
                break;
                
            // ----- UI Decorators -----
            case OpCode.SpRound:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spr)) spr.CornerRadius = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpBorder:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spbr)) 
                {
                    spbr.BorderColor = GetString(inst.Arguments[1]);
                    spbr.BorderWidth = GetVal(inst.Arguments[2]);
                }
                break;
            case OpCode.SpGradient:
                ValidateArgs(inst, 4);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spg)) 
                {
                    spg.GradientTo = GetString(inst.Arguments[2]);
                    spg.GradientDirection = GetString(inst.Arguments[3]);
                }
                break;
            case OpCode.SpShadow:
                ValidateArgs(inst, 5);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spsh)) 
                {
                    spsh.ShadowOffsetX = GetVal(inst.Arguments[1]);
                    spsh.ShadowOffsetY = GetVal(inst.Arguments[2]);
                    spsh.ShadowColor = GetString(inst.Arguments[3]);
                    spsh.ShadowAlpha = GetVal(inst.Arguments[4]);
                }
                break;
            case OpCode.SpTextShadow:
                ValidateArgs(inst, 4);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spts)) 
                {
                    spts.TextShadowX = GetVal(inst.Arguments[1]);
                    spts.TextShadowY = GetVal(inst.Arguments[2]);
                    spts.TextShadowColor = GetString(inst.Arguments[3]);
                }
                break;
            case OpCode.SpTextOutline:
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spto)) 
                {
                    spto.TextOutlineSize = GetVal(inst.Arguments[1]);
                    spto.TextOutlineColor = GetString(inst.Arguments[2]);
                }
                break;
            case OpCode.SpTextAlign:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var spta)) spta.TextAlign = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpRotation:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sprot)) sprot.Rotation = GetVal(inst.Arguments[1]);
                break;
            case OpCode.SpHoverColor:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphc)) sphc.HoverFillColor = GetString(inst.Arguments[1]);
                break;
            case OpCode.SpHoverScale:
                ValidateArgs(inst, 2);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sphs)) sphs.HoverScale = float.Parse(inst.Arguments[1]);
                break;

            // ----- Animations (Tween) -----
            case OpCode.Amsp:
                ValidateArgs(inst, 4);
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
                ValidateArgs(inst, 3);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sp_afade))
                {
                    Tweens.Add(new Tween { SpriteId = sp_afade.Id, Property = "opacity", From = sp_afade.Opacity, To = GetVal(inst.Arguments[1])/255f, DurationMs = GetVal(inst.Arguments[2]), Ease = Tweens.CurrentEaseType });
                }
                break;
            case OpCode.Ascale:
                ValidateArgs(inst, 4);
                if (State.Sprites.TryGetValue(GetVal(inst.Arguments[0]), out var sp_ascale))
                {
                    Tweens.Add(new Tween { SpriteId = sp_ascale.Id, Property = "scaleX", From = sp_ascale.ScaleX, To = float.Parse(inst.Arguments[1]), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                    Tweens.Add(new Tween { SpriteId = sp_ascale.Id, Property = "scaleY", From = sp_ascale.ScaleY, To = float.Parse(inst.Arguments[2]), DurationMs = GetVal(inst.Arguments[3]), Ease = Tweens.CurrentEaseType });
                }
                break;
            case OpCode.Await:
                State.State = VmState.WaitingForAnimation;
                break;
            case OpCode.Ease:
                ValidateArgs(inst, 1);
                string easeName = GetString(inst.Arguments[0]).ToLowerInvariant();
                if (easeName == "easein") Tweens.CurrentEaseType = EaseType.EaseIn;
                else if (easeName == "easeout") Tweens.CurrentEaseType = EaseType.EaseOut;
                else if (easeName == "easeinout") Tweens.CurrentEaseType = EaseType.EaseInOut;
                else Tweens.CurrentEaseType = EaseType.Linear;
                break;

            case OpCode.BtnTime:
                ValidateArgs(inst, 1);
                State.ButtonTimeoutMs = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BtnWait:
                State.State = VmState.WaitingForButton;
                break;

            case OpCode.LoadBg:
            case OpCode.Bg:
                ValidateArgs(inst, 1);
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
                break;
            case OpCode.TextClear:
                State.CurrentTextBuffer = "";
                State.DisplayedTextLength = 0;
                State.IsWaitingPageClear = false;
                if (State.Sprites.ContainsKey(9000)) State.Sprites.Remove(9000);
                if (State.Sprites.ContainsKey(9001)) State.Sprites.Remove(9001);
                break;

            case OpCode.Textbox:
            case OpCode.SetWindow:
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
                ValidateArgs(inst, 1);
                State.DefaultFontSize = GetVal(inst.Arguments[0]);
                break;
            case OpCode.Textcolor:
                ValidateArgs(inst, 1);
                State.DefaultTextColor = GetString(inst.Arguments[0]);
                break;
            case OpCode.TextboxColor:
                ValidateArgs(inst, 2);
                State.DefaultTextboxBgColor = GetString(inst.Arguments[0]);
                State.DefaultTextboxBgAlpha = GetVal(inst.Arguments[1]);
                break;
            case OpCode.TextboxHide:
            case OpCode.EraseTextWindow:
                State.TextboxVisible = false;
                if (State.Sprites.ContainsKey(9000)) State.Sprites[9000].Visible = false;
                if (State.Sprites.ContainsKey(9001)) State.Sprites[9001].Visible = false;
                break;
            case OpCode.TextboxShow:
                State.TextboxVisible = true;
                if (State.Sprites.ContainsKey(9000)) State.Sprites[9000].Visible = true;
                if (State.Sprites.ContainsKey(9001)) State.Sprites[9001].Visible = true;
                break;
                
            case OpCode.TextMode:
                ValidateArgs(inst, 1);
                string mode = GetString(inst.Arguments[0]).ToLowerInvariant();
                Config.Config.TextMode = mode;
                if (mode == "nvl")
                {
                    State.DefaultTextboxX = 20;
                    State.DefaultTextboxY = 20;
                    State.DefaultTextboxW = State.WindowWidth - 40;
                    State.DefaultTextboxH = State.WindowHeight - 40;
                    State.DefaultTextboxBgAlpha = 150;
                }
                else
                {
                    State.DefaultTextboxX = 50;
                    State.DefaultTextboxY = 500;
                    State.DefaultTextboxW = 1180;
                    State.DefaultTextboxH = 200;
                    State.DefaultTextboxBgAlpha = 180;
                }
                break;
            case OpCode.TextSpeed:
                ValidateArgs(inst, 1);
                State.TextSpeedMs = GetVal(inst.Arguments[0]);
                break;
            case OpCode.DefaultSpeed:
                ValidateArgs(inst, 1);
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
                State.State = VmState.WaitingForClick;
                break;
            case OpCode.WaitClickClear:
                State.IsWaitingPageClear = true;
                State.State = VmState.WaitingForClick;
                break;
            case OpCode.Text:
                if (State.TextboxVisible)
                {
                    State.Sprites[9000] = new Sprite
                    {
                        Id = 9000, Type = SpriteType.Rect, Z = 9000,
                        X = State.DefaultTextboxX, Y = State.DefaultTextboxY,
                        Width = State.DefaultTextboxW, Height = State.DefaultTextboxH,
                        FillColor = State.DefaultTextboxBgColor, FillAlpha = State.DefaultTextboxBgAlpha,
                        IsHovered = false // ensure initialization
                    };
                    string fullText = string.Join(" ", inst.Arguments);
                    // Process variable interpolation in text: ${name}
                    foreach (var kvp in State.StringRegisters)
                        fullText = fullText.Replace($"${{{kvp.Key}}}", kvp.Value);
                        
                    State.CurrentTextBuffer += fullText;
                    
                    State.Sprites[9001] = new Sprite
                    {
                        Id = 9001, Type = SpriteType.Text, Z = 9001,
                        Text = State.CurrentTextBuffer,
                        X = State.DefaultTextboxX + 20, Y = State.DefaultTextboxY + 20,
                        Width = State.DefaultTextboxW - 40, Height = State.DefaultTextboxH - 40,
                        FontSize = State.DefaultFontSize, Color = State.DefaultTextColor
                    };
                    
                    if (State.TextSpeedMs > 0 && State.DisplayedTextLength < State.CurrentTextBuffer.Length)
                    {
                        State.State = VmState.WaitingForAnimation; 
                    }
                    else
                    {
                        State.DisplayedTextLength = State.CurrentTextBuffer.Length;
                    }
                }
                break;

            case OpCode.Wait:
                State.State = VmState.WaitingForClick;
                break;

            case OpCode.Choice:
                int count = inst.Arguments.Count;
                int h = 60; int spacing = 20;
                int totalH = (h + spacing) * count - spacing;
                int startY = (State.WindowHeight - totalH) / 2;
                int startX = (State.WindowWidth - 600) / 2;

                for (int i = 0; i < count; i++)
                {
                    int y = startY + (h + spacing) * i;
                    int rectId = 9100 + i;
                    int textId = 9200 + i;
                    
                    State.Sprites[rectId] = new Sprite
                    {
                        Id = rectId, Type = SpriteType.Rect, Z = 9500,
                        X = startX, Y = y, Width = 600, Height = h,
                        FillColor = "#000000", FillAlpha = 200, IsButton = true
                    };
                    State.Sprites[textId] = new Sprite
                    {
                        Id = textId, Type = SpriteType.Text, Z = 9501,
                        Text = GetString(inst.Arguments[i]), X = startX + 20 + 200, Y = y + 10,
                        FontSize = 32, Color = "#ffffff"
                    };
                }
                State.State = VmState.WaitingForButton;
                break;

            case OpCode.Let:
            case OpCode.Mov:
                ValidateArgs(inst, 2);
                if (inst.Arguments[0].StartsWith("$") || inst.Arguments[1].StartsWith("\""))
                    SetStr(inst.Arguments[0], GetString(inst.Arguments[1]));
                else
                    SetReg(inst.Arguments[0], GetVal(inst.Arguments[1].ToString()));
                break;
            case OpCode.Add:
                ValidateArgs(inst, 2);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + GetVal(inst.Arguments[1]));
                break;
            case OpCode.Sub:
                ValidateArgs(inst, 2);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - GetVal(inst.Arguments[1]));
                break;
            case OpCode.Mul:
                ValidateArgs(inst, 2);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) * GetVal(inst.Arguments[1]));
                break;
            case OpCode.Div:
                ValidateArgs(inst, 2);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) / GetVal(inst.Arguments[1]));
                break;
            case OpCode.Mod:
                ValidateArgs(inst, 2);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) % GetVal(inst.Arguments[1]));
                break;
            case OpCode.Cmp:
                ValidateArgs(inst, 2);
                {
                    int lhs = GetReg(inst.Arguments[0]);
                    int rhs = GetVal(inst.Arguments[1]);
                    State.CompareFlag = lhs == rhs ? 0 : (lhs > rhs ? 1 : -1);
                }
                break;
            case OpCode.Beq:
                ValidateArgs(inst, 1);
                if (State.CompareFlag == 0) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Bne:
                ValidateArgs(inst, 1);
                if (State.CompareFlag != 0) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Bgt:
                ValidateArgs(inst, 1);
                if (State.CompareFlag == 1) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Blt:
                ValidateArgs(inst, 1);
                if (State.CompareFlag == -1) JumpTo(inst.Arguments[0]);
                break;
            case OpCode.Jmp:
                ValidateArgs(inst, 1);
                JumpTo(inst.Arguments[0]);
                break;
                
            case OpCode.Delay:
                ValidateArgs(inst, 1);
                State.DelayTimerMs = GetVal(inst.Arguments[0]);
                State.State = VmState.WaitingForDelay;
                break;
            case OpCode.Rnd:
                ValidateArgs(inst, 3);
                SetReg(inst.Arguments[0], Raylib_cs.Raylib.GetRandomValue(GetVal(inst.Arguments[1]), GetVal(inst.Arguments[2])));
                break;
            case OpCode.Inc:
                ValidateArgs(inst, 1);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) + 1);
                break;
            case OpCode.Dec:
                ValidateArgs(inst, 1);
                SetReg(inst.Arguments[0], GetReg(inst.Arguments[0]) - 1);
                break;
            case OpCode.For:
                // Emulate for loop 
                ValidateArgs(inst, 3);
                SetReg(inst.Arguments[0], GetVal(inst.Arguments[1]));
                // Push return address for `next` (primitive)
                State.CallStack.Push(State.ProgramCounter);
                State.ParamStack.Push(inst.Arguments[0]); // store variable name
                State.ParamStack.Push(inst.Arguments[2]); // store target value
                break;
            case OpCode.Next:
                if (State.ParamStack.Count >= 2 && State.CallStack.Count > 0)
                {
                    int endVal = GetVal(State.ParamStack.Pop());
                    string varName = State.ParamStack.Pop();
                    int pc = State.CallStack.Pop();
                    int currVal = GetReg(varName) + 1;
                    SetReg(varName, currVal);
                    if (currVal <= endVal)
                    {
                        State.CallStack.Push(pc);
                        State.ParamStack.Push(varName);
                        State.ParamStack.Push(endVal.ToString());
                        State.ProgramCounter = pc;
                    }
                }
                break;
            case OpCode.GetTimer:
                ValidateArgs(inst, 1);
                SetReg(inst.Arguments[0], (int)State.ScriptTimerMs);
                break;
            case OpCode.ResetTimer:
                State.ScriptTimerMs = 0;
                break;
            case OpCode.WaitTimer:
                ValidateArgs(inst, 1);
                // Can emulate via wait logic, actually let's just cheat and do a simple wait
                State.DelayTimerMs = Math.Max(0, GetVal(inst.Arguments[0]) - State.ScriptTimerMs);
                State.State = VmState.WaitingForDelay;
                break;
            case OpCode.RightMenu:
                ValidateArgs(inst, 1);
                State.RightMenuLabel = inst.Arguments[0];
                break;
            case OpCode.Save:
                ValidateArgs(inst, 1);
                Saves.Save(GetVal(inst.Arguments[0]), State, _currentScriptFile);
                break;
            case OpCode.Load:
                ValidateArgs(inst, 1);
                var (dat, suc) = Saves.Load(GetVal(inst.Arguments[0]));
                if (suc && dat != null)
                {
                    // Copy state over
                    State.Registers = dat.State.Registers;
                    State.StringRegisters = dat.State.StringRegisters;
                    State.Sprites = dat.State.Sprites;
                    State.CallStack = dat.State.CallStack;
                    State.CurrentBgm = dat.State.CurrentBgm;
                    _currentScriptFile = dat.ScriptFile;
                    // Note: Need a full deep copy normally, but overriding reference is fine if careful
                    JumpTo("*load_restore"); // optional hook label in game script
                }
                break;

            case OpCode.PlayBgm:
                ValidateArgs(inst, 1);
                State.CurrentBgm = GetString(inst.Arguments[0]);
                break;
            case OpCode.StopBgm:
                State.CurrentBgm = "";
                break;
            case OpCode.PlaySe:
            case OpCode.Dwave:
                // dwave 0, "file"
                string seFile = inst.Arguments.Count > 1 ? GetString(inst.Arguments[1]) : GetString(inst.Arguments[0]);
                State.PendingSe.Add(seFile);
                break;
            case OpCode.BgmVol:
                ValidateArgs(inst, 1);
                State.BgmVolume = GetVal(inst.Arguments[0]);
                break;
            case OpCode.SeVol:
                ValidateArgs(inst, 1);
                State.SeVolume = GetVal(inst.Arguments[0]);
                break;
            case OpCode.BgmFade:
                // implement bgm fade signal
                State.CurrentBgm = ""; // shortcut for now
                break;
            case OpCode.YesNoBox:
                // var, msg, title
                // We'll emulate it by creating choices on screen
                ValidateArgs(inst, 3);
                State.Sprites[9100] = new Sprite { Id = 9100, Type = SpriteType.Rect, Z = 9500, X = 400, Y = 300, Width = 200, Height = 60, FillColor = "#000000", FillAlpha = 200, IsButton = true };
                State.Sprites[9200] = new Sprite { Id = 9200, Type = SpriteType.Text, Z = 9501, Text = "Yes", X = 480, Y = 315, FontSize = 32 };
                State.Sprites[9101] = new Sprite { Id = 9101, Type = SpriteType.Rect, Z = 9500, X = 680, Y = 300, Width = 200, Height = 60, FillColor = "#000000", FillAlpha = 200, IsButton = true };
                State.Sprites[9201] = new Sprite { Id = 9201, Type = SpriteType.Text, Z = 9501, Text = "No", X = 760, Y = 315, FontSize = 32 };
                // Also show msg
                State.Sprites[9300] = new Sprite { Id = 9300, Type = SpriteType.Rect, Z = 9500, X = 200, Y = 200, Width = 880, Height = 80, FillColor = "#444444", FillAlpha = 255 };
                State.Sprites[9301] = new Sprite { Id = 9301, Type = SpriteType.Text, Z = 9501, Text = GetString(inst.Arguments[1]), X = 220, Y = 220, FontSize = 32 };
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
                int chapterId = GetVal(inst.Arguments[0]);
                ChapterManager.UnlockChapter(chapterId);
                ChapterManager.SaveChapters();
                break;

            case OpCode.ChapterThumbnail:
                // チャプターサムネイルの設定
                if (inst.Arguments.Count >= 2)
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
                if (inst.Arguments.Count >= 5)
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
                if (inst.Arguments.Count >= 2)
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
                if (inst.Arguments.Count > 0)
                {
                    string charDataFile = GetString(inst.Arguments[0]);
                    CharacterManager.LoadCharacterData(charDataFile);
                }
                break;

            case OpCode.CharShow:
                {
                    ValidateArgs(inst, 1);
                    string charId = inst.Arguments[0];
                    string expression = inst.Arguments.Count > 1 ? inst.Arguments[1] : "normal";
                    string pose = inst.Arguments.Count > 2 ? inst.Arguments[2] : "default";
                    CharacterManager.ShowCharacter(charId, expression, pose);
                    break;
                }

            case OpCode.CharHide:
                ValidateArgs(inst, 1);
                string hideCharId = inst.Arguments[0];
                int hideDuration = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 300;
                CharacterManager.HideCharacter(hideCharId, hideDuration);
                break;

            case OpCode.CharMove:
                ValidateArgs(inst, 3);
                string moveCharId = inst.Arguments[0];
                int moveX = GetVal(inst.Arguments[1]);
                int moveY = GetVal(inst.Arguments[2]);
                int moveDuration = inst.Arguments.Count > 3 ? GetVal(inst.Arguments[3]) : 500;
                CharacterManager.MoveCharacter(moveCharId, moveX, moveY, moveDuration);
                break;

            case OpCode.CharExpression:
                {
                    ValidateArgs(inst, 2);
                    string exprCharId = inst.Arguments[0];
                    string expression = inst.Arguments[1];
                    CharacterManager.ChangeExpression(exprCharId, expression);
                    break;
                }

            case OpCode.CharPose:
                {
                    ValidateArgs(inst, 2);
                    string poseCharId = inst.Arguments[0];
                    string pose = inst.Arguments[1];
                    CharacterManager.ChangePose(poseCharId, pose);
                    break;
                }

            case OpCode.CharZ:
                ValidateArgs(inst, 2);
                string zCharId = inst.Arguments[0];
                int z = GetVal(inst.Arguments[1]);
                CharacterManager.SetCharacterZ(zCharId, z);
                break;

            case OpCode.CharScale:
                ValidateArgs(inst, 2);
                string scaleCharId = inst.Arguments[0];
                float scale = float.Parse(inst.Arguments[1]);
                CharacterManager.SetCharacterScale(scaleCharId, scale);
                break;

            // ゲームフロー
            case OpCode.ChangeScene:
                ValidateArgs(inst, 1);
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
                    ValidateArgs(inst, 2);
                    string dataKey = inst.Arguments[0];
                    string dataValue = GetString(inst.Arguments[1]);
                    State.SceneData[dataKey] = dataValue;
                    break;
                }

            case OpCode.GetSceneData:
                {
                    ValidateArgs(inst, 1);
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
                ValidateArgs(inst, 2);
                {
                    string flagName = inst.Arguments[0];
                    bool value = GetVal(inst.Arguments[1]) != 0;
                    State.Flags[flagName] = value;
                }
                break;

            case OpCode.GetFlag:
                ValidateArgs(inst, 1);
                {
                    string flagName = inst.Arguments[0];
                    bool value = State.Flags.TryGetValue(flagName, out bool flagValue) ? flagValue : false;
                    SetReg(inst.Arguments[0], value ? 1 : 0);
                }
                break;

            case OpCode.ClearFlag:
                ValidateArgs(inst, 1);
                {
                    string flagName = inst.Arguments[0];
                    State.Flags[flagName] = false;
                }
                break;

            case OpCode.ToggleFlag:
                ValidateArgs(inst, 1);
                {
                    string flagName = inst.Arguments[0];
                    bool currentValue = State.Flags.TryGetValue(flagName, out bool flagValue) ? flagValue : false;
                    State.Flags[flagName] = !currentValue;
                }
                break;

            case OpCode.IncCounter:
                ValidateArgs(inst, 1);
                {
                    string counterName = inst.Arguments[0];
                    int increment = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 1;
                    int currentValue = State.Counters.TryGetValue(counterName, out int counterValue) ? counterValue : 0;
                    State.Counters[counterName] = currentValue + increment;
                }
                break;

            case OpCode.DecCounter:
                ValidateArgs(inst, 1);
                {
                    string counterName = inst.Arguments[0];
                    int decrement = inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 1;
                    int currentValue = State.Counters.TryGetValue(counterName, out int counterValue) ? counterValue : 0;
                    State.Counters[counterName] = currentValue - decrement;
                }
                break;

            case OpCode.SetCounter:
                ValidateArgs(inst, 2);
                {
                    string counterName = inst.Arguments[0];
                    int value = GetVal(inst.Arguments[1]);
                    State.Counters[counterName] = value;
                }
                break;

            case OpCode.GetCounter:
                ValidateArgs(inst, 1);
                {
                    string counterName = inst.Arguments[0];
                    int value = State.Counters.TryGetValue(counterName, out int counterValue) ? counterValue : 0;
                    SetReg(inst.Arguments[0], value);
                }
                break;

            // チャプター定義（スクリプト主導）
            case OpCode.DefChapter:
                State.CurrentChapterDefinition = new ChapterInfo();
                break;

            case OpCode.ChapterId:
                ValidateArgs(inst, 1);
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Id = GetVal(inst.Arguments[0]);
                break;

            case OpCode.ChapterTitle:
                ValidateArgs(inst, 1);
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Title = GetString(inst.Arguments[0]);
                break;

            case OpCode.ChapterDesc:
                ValidateArgs(inst, 1);
                if (State.CurrentChapterDefinition != null)
                    State.CurrentChapterDefinition.Description = GetString(inst.Arguments[0]);
                break;

            case OpCode.ChapterScript:
                ValidateArgs(inst, 1);
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

            // フォントフィルター設定
            case OpCode.FontFilter:
                ValidateArgs(inst, 1);
                string filterType = GetString(inst.Arguments[0]).ToLowerInvariant();
                if (filterType == "bilinear")
                    State.FontFilter = TextureFilter.Bilinear;
                else if (filterType == "trilinear")
                    State.FontFilter = TextureFilter.Trilinear;
                else if (filterType == "point")
                    State.FontFilter = TextureFilter.Point;
                else
                    State.FontFilter = TextureFilter.Bilinear;
                break;
        }
    }

    private void ValidateArgs(Instruction inst, int min)
    {
        if (inst.Arguments.Count < min)
            throw new Exception($"'{inst.Op}' には最低 {min} 個の引数が必要です（渡された引数: {inst.Arguments.Count}）");
    }

    private void SetReg(string reg, int val) => State.Registers[reg.TrimStart('%').ToLowerInvariant()] = val;
    private int GetReg(string reg) => State.Registers.TryGetValue(reg.TrimStart('%').ToLowerInvariant(), out int v) ? v : 0;
    private int GetVal(string valStr)
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
    
    private void SetStr(string reg, string val) => State.StringRegisters[reg.TrimStart('$').ToLowerInvariant()] = val;
    private string GetString(string valStr)
    {
        if (valStr.StartsWith("$"))
        {
            string key = valStr.TrimStart('$').ToLowerInvariant();
            return State.StringRegisters.TryGetValue(key, out string? v) ? (v ?? "") : "";
        }
        return valStr;
    }

    public void JumpTo(string labelNameArg)
    {
        string label = labelNameArg.TrimStart('*');
        if (_labels.TryGetValue(label, out int pc)) State.ProgramCounter = pc;
        else throw new Exception($"未定義のラベル '*{label}' にジャンプしようとしました。");
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
            throw new Exception($"未定義のサブルーチン '*{label}' を呼び出そうとしました。");
        }
    }

    /// <summary>
    /// ゲームを終了します。
    /// </summary>
    public void QuitGame()
    {
        State.State = VmState.Ended;
    }

    /// <summary>
    /// ゲームをセーブします。
    /// </summary>
    public void SaveGame(int slot)
    {
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
            State = data.State;
            State.ProgramCounter = 0;
            Console.WriteLine($"Game loaded from slot {slot}");
        }
        else
        {
            Console.WriteLine($"Failed to load game from slot {slot}");
        }
    }
}
