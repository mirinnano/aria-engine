using System;
using System.Collections.Generic;

namespace AriaEngine.Core;

public enum CommandCategory
{
    Core,
    Script,
    Render,
    Text,
    Input,
    Audio,
    Save,
    Flags,
    System,
    Compatibility
}

public sealed record CommandInfo(
    OpCode OpCode,
    string CanonicalName,
    CommandCategory Category,
    string[] Aliases,
    int MinArgs,
    string Description,
    bool IsCompatibility);

public static class CommandRegistry
{
    private static readonly Dictionary<string, CommandInfo> Commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<OpCode, CommandInfo> CommandsByOpCode = new();

    static CommandRegistry()
    {
        Register(CommandCategory.Compatibility, OpCode.LoadBg, "load_bg");
        Register(CommandCategory.Compatibility, OpCode.LoadCh, "load_ch");
        Register(CommandCategory.Compatibility, OpCode.ShowCh, "show_ch");
        Register(CommandCategory.Compatibility, OpCode.HideCh, "hide_ch");
        Register(CommandCategory.Compatibility, OpCode.Clr, "clr");
        Register(CommandCategory.Core, OpCode.Wait, "wait");

        Register(CommandCategory.Text, OpCode.Textbox, "textbox");
        Register(CommandCategory.Text, OpCode.Fontsize, "fontsize");
        Register(CommandCategory.Text, OpCode.Textcolor, "textcolor");
        Register(CommandCategory.Text, OpCode.TextboxColor, "textbox_color");
        Register(CommandCategory.Text, OpCode.TextboxHide, "textbox_hide");
        Register(CommandCategory.Text, OpCode.TextboxShow, "textbox_show");
        Register(CommandCategory.Text, OpCode.SetWindow, "setwindow");
        Register(CommandCategory.Text, OpCode.EraseTextWindow, "erasetextwindow");
        Register(CommandCategory.Text, OpCode.TextClear, "textclear");
        Register(CommandCategory.Text, OpCode.TextSpeed, "textspeed");
        Register(CommandCategory.Text, OpCode.DefaultSpeed, "defaultspeed");
        Register(CommandCategory.Text, OpCode.TextMode, "textmode");
        Register(CommandCategory.Text, OpCode.TextboxStyle, "textbox_style");
        Register(CommandCategory.Text, OpCode.ChoiceStyle, "choice_style");
        Register(CommandCategory.Text, OpCode.TextTarget, "text_target");
        Register(CommandCategory.Compatibility, OpCode.CompatMode, "compat_mode");
        Register(CommandCategory.Text, OpCode.UiTheme, "ui_theme");
        Register(CommandCategory.Text, OpCode.Br, "br");
        Register(CommandCategory.Text, OpCode.WaitClickClear, "\\");
        Register(CommandCategory.Text, OpCode.WaitClick, "@");

        Register(CommandCategory.System, OpCode.YesNoBox, "yesnobox");
        Register(CommandCategory.System, OpCode.MesBox, "mesbox");
        Register(CommandCategory.Save, OpCode.SaveOn, "saveon");
        Register(CommandCategory.Save, OpCode.SaveOff, "saveoff");
        Register(CommandCategory.Save, OpCode.Save, "save");
        Register(CommandCategory.Save, OpCode.Load, "load");
        Register(CommandCategory.Text, OpCode.LookbackOn, "lookback_on");
        Register(CommandCategory.Text, OpCode.LookbackOff, "lookback_off");
        Register(CommandCategory.System, OpCode.AutoModeTime, "automode_time");
        Register(CommandCategory.Input, OpCode.RightMenu, "rmenu");
        Register(CommandCategory.Text, OpCode.ClickCursor, "clickcursor");
        Register(CommandCategory.Text, OpCode.Backlog, "backlog");
        Register(CommandCategory.Text, OpCode.KidokuMode, "kidokumode");
        Register(CommandCategory.Text, OpCode.SkipMode, "skipmode");
        Register(CommandCategory.System, OpCode.WindowTitle, "window_title");
        Register(CommandCategory.System, OpCode.SystemButton, "system_button");

        Register(CommandCategory.Core, OpCode.Delay, "delay");
        Register(CommandCategory.Core, OpCode.Rnd, "rnd");
        Register(CommandCategory.Core, OpCode.Inc, "inc");
        Register(CommandCategory.Core, OpCode.Dec, "dec");
        Register(CommandCategory.Core, OpCode.For, "for");
        Register(CommandCategory.Core, OpCode.Next, "next");
        Register(CommandCategory.Core, OpCode.ResetTimer, "resettimer");
        Register(CommandCategory.Core, OpCode.GetTimer, "gettimer");
        Register(CommandCategory.Core, OpCode.WaitTimer, "waittimer");

        Register(CommandCategory.Core, OpCode.Mov, "mov", "let");
        Register(CommandCategory.Script, OpCode.Alias, "numalias", "alias");
        Register(CommandCategory.Core, OpCode.Add, "add");
        Register(CommandCategory.Core, OpCode.Sub, "sub");
        Register(CommandCategory.Core, OpCode.Mul, "mul");
        Register(CommandCategory.Core, OpCode.Div, "div");
        Register(CommandCategory.Core, OpCode.Mod, "mod");
        Register(CommandCategory.Core, OpCode.Cmp, "cmp");
        Register(CommandCategory.Core, OpCode.Beq, "beq");
        Register(CommandCategory.Core, OpCode.Bne, "bne");
        Register(CommandCategory.Core, OpCode.Bgt, "bgt");
        Register(CommandCategory.Core, OpCode.Blt, "blt");
        Register(CommandCategory.Core, OpCode.Jmp, "jmp", "goto");
        Register(CommandCategory.Text, OpCode.Choice, "choice");
        Register(CommandCategory.System, OpCode.End, "end");
        Register(CommandCategory.Script, OpCode.Gosub, "gosub", "call");
        Register(CommandCategory.Script, OpCode.Return, "return", "ret");
        Register(CommandCategory.Script, OpCode.Defsub, "defsub", "sub");
        Register(CommandCategory.Script, OpCode.Getparam, "getparam");
        Register(CommandCategory.System, OpCode.SystemCall, "systemcall");

        Register(CommandCategory.Render, OpCode.Lsp, "lsp");
        Register(CommandCategory.Render, OpCode.LspText, "lsp_text");
        Register(CommandCategory.Render, OpCode.LspRect, "lsp_rect");
        Register(CommandCategory.Render, OpCode.Csp, "csp");
        Register(CommandCategory.Render, OpCode.Vsp, "vsp");
        Register(CommandCategory.Render, OpCode.Msp, "msp");
        Register(CommandCategory.Render, OpCode.MspRel, "msp_rel");
        Register(CommandCategory.Render, OpCode.SpZ, "sp_z");
        Register(CommandCategory.Render, OpCode.SpAlpha, "sp_alpha");
        Register(CommandCategory.Render, OpCode.SpScale, "sp_scale");
        Register(CommandCategory.Render, OpCode.SpFontsize, "sp_fontsize");
        Register(CommandCategory.Render, OpCode.SpColor, "sp_color");
        Register(CommandCategory.Render, OpCode.SpFill, "sp_fill");
        Register(CommandCategory.Render, OpCode.SpRound, "sp_round");
        Register(CommandCategory.Render, OpCode.SpBorder, "sp_border");
        Register(CommandCategory.Render, OpCode.SpGradient, "sp_gradient");
        Register(CommandCategory.Render, OpCode.SpShadow, "sp_shadow");
        Register(CommandCategory.Render, OpCode.SpTextShadow, "sp_text_shadow");
        Register(CommandCategory.Render, OpCode.SpTextOutline, "sp_text_outline");
        Register(CommandCategory.Render, OpCode.SpTextAlign, "sp_text_align");
        Register(CommandCategory.Render, OpCode.SpTextVAlign, "sp_text_valign");
        Register(CommandCategory.Render, OpCode.SpRotation, "sp_rotation");
        Register(CommandCategory.Render, OpCode.SpHoverColor, "sp_hover_color");
        Register(CommandCategory.Render, OpCode.SpHoverScale, "sp_hover_scale");
        Register(CommandCategory.Render, OpCode.SpCursor, "sp_cursor");
        Register(CommandCategory.Render, OpCode.Amsp, "amsp");
        Register(CommandCategory.Render, OpCode.Afade, "afade");
        Register(CommandCategory.Render, OpCode.Ascale, "ascale");
        Register(CommandCategory.Render, OpCode.Acolor, "acolor");
        Register(CommandCategory.Render, OpCode.Await, "await");
        Register(CommandCategory.Render, OpCode.Ease, "ease");

        Register(CommandCategory.Input, OpCode.Btn, "btn");
        Register(CommandCategory.Input, OpCode.BtnArea, "btn_area");
        Register(CommandCategory.Input, OpCode.BtnClear, "btn_clear");
        Register(CommandCategory.Input, OpCode.BtnClearAll, "btn_clear_all", "btndef");
        Register(CommandCategory.Input, OpCode.BtnWait, "btnwait", "textbtnwait");
        Register(CommandCategory.Input, OpCode.SpBtn, "spbtn");
        Register(CommandCategory.Input, OpCode.BtnTime, "btntime");

        Register(CommandCategory.Compatibility, OpCode.Bg, "bg");
        Register(CommandCategory.Compatibility, OpCode.Print, "print");
        Register(CommandCategory.Compatibility, OpCode.Effect, "effect");
        Register(CommandCategory.Render, OpCode.Quake, "quake", "quakex");

        Register(CommandCategory.Audio, OpCode.PlayBgm, "play_bgm", "bgm");
        Register(CommandCategory.Audio, OpCode.StopBgm, "stop_bgm");
        Register(CommandCategory.Audio, OpCode.PlaySe, "play_se");
        Register(CommandCategory.Audio, OpCode.PlayMp3, "play_mp3", "mp3loop");
        Register(CommandCategory.Render, OpCode.FadeIn, "fade_in");
        Register(CommandCategory.Render, OpCode.FadeOut, "fade_out");
        Register(CommandCategory.Audio, OpCode.BgmVol, "bgmvol");
        Register(CommandCategory.Audio, OpCode.SeVol, "sevol");
        Register(CommandCategory.Audio, OpCode.Mp3Vol, "mp3vol");
        Register(CommandCategory.Audio, OpCode.BgmFade, "bgmfade");
        Register(CommandCategory.Audio, OpCode.Mp3FadeOut, "mp3fadeout");
        Register(CommandCategory.Audio, OpCode.Dwave, "dwave");
        Register(CommandCategory.Audio, OpCode.DwaveLoop, "dwaveloop");
        Register(CommandCategory.Audio, OpCode.DwaveStop, "dwavestop");

        Register(CommandCategory.System, OpCode.Window, "window");
        Register(CommandCategory.Text, OpCode.Font, "font");
        Register(CommandCategory.Text, OpCode.FontAtlasSize, "font_atlas_size");
        Register(CommandCategory.Script, OpCode.Script, "script", "include");
        Register(CommandCategory.System, OpCode.Debug, "debug");
        Register(CommandCategory.System, OpCode.Caption, "caption");

        Register(CommandCategory.Compatibility, OpCode.ChapterSelect, "chapter_select");
        Register(CommandCategory.Compatibility, OpCode.UnlockChapter, "unlock_chapter");
        Register(CommandCategory.Compatibility, OpCode.ChapterThumbnail, "chapter_thumbnail");
        Register(CommandCategory.Compatibility, OpCode.ChapterCard, "chapter_card");
        Register(CommandCategory.Compatibility, OpCode.ChapterScroll, "chapter_scroll");
        Register(CommandCategory.Compatibility, OpCode.ChapterProgress, "chapter_progress");
        Register(CommandCategory.Compatibility, OpCode.CharLoad, "char_load");
        Register(CommandCategory.Compatibility, OpCode.CharShow, "char_show");
        Register(CommandCategory.Compatibility, OpCode.CharHide, "char_hide");
        Register(CommandCategory.Compatibility, OpCode.CharMove, "char_move");
        Register(CommandCategory.Compatibility, OpCode.CharExpression, "char_expression");
        Register(CommandCategory.Compatibility, OpCode.CharPose, "char_pose");
        Register(CommandCategory.Compatibility, OpCode.CharZ, "char_z");
        Register(CommandCategory.Compatibility, OpCode.CharScale, "char_scale");
        Register(CommandCategory.Compatibility, OpCode.ChangeScene, "change_scene");
        Register(CommandCategory.Compatibility, OpCode.ReturnScene, "return_scene");
        Register(CommandCategory.Compatibility, OpCode.SetSceneData, "set_scene_data");
        Register(CommandCategory.Compatibility, OpCode.GetSceneData, "get_scene_data");

        Register(CommandCategory.Flags, OpCode.SetFlag, "set_flag");
        Register(CommandCategory.Flags, OpCode.GetFlag, "get_flag");
        Register(CommandCategory.Flags, OpCode.ClearFlag, "clear_flag");
        Register(CommandCategory.Flags, OpCode.ToggleFlag, "toggle_flag");
        Register(CommandCategory.Flags, OpCode.SetPFlag, "set_pflag");
        Register(CommandCategory.Flags, OpCode.GetPFlag, "get_pflag");
        Register(CommandCategory.Flags, OpCode.ClearPFlag, "clear_pflag");
        Register(CommandCategory.Flags, OpCode.TogglePFlag, "toggle_pflag");
        Register(CommandCategory.Flags, OpCode.SetSFlag, "set_sflag");
        Register(CommandCategory.Flags, OpCode.GetSFlag, "get_sflag");
        Register(CommandCategory.Flags, OpCode.ClearSFlag, "clear_sflag");
        Register(CommandCategory.Flags, OpCode.ToggleSFlag, "toggle_sflag");
        Register(CommandCategory.Flags, OpCode.SetVFlag, "set_vflag");
        Register(CommandCategory.Flags, OpCode.GetVFlag, "get_vflag");
        Register(CommandCategory.Flags, OpCode.ClearVFlag, "clear_vflag");
        Register(CommandCategory.Flags, OpCode.ToggleVFlag, "toggle_vflag");
        Register(CommandCategory.Flags, OpCode.IncCounter, "inc_counter");
        Register(CommandCategory.Flags, OpCode.DecCounter, "dec_counter");
        Register(CommandCategory.Flags, OpCode.SetCounter, "set_counter");
        Register(CommandCategory.Flags, OpCode.GetCounter, "get_counter");

        Register(CommandCategory.Compatibility, OpCode.DefChapter, "defchapter");
        Register(CommandCategory.Compatibility, OpCode.ChapterId, "chapter_id");
        Register(CommandCategory.Compatibility, OpCode.ChapterTitle, "chapter_title");
        Register(CommandCategory.Compatibility, OpCode.ChapterDesc, "chapter_desc");
        Register(CommandCategory.Compatibility, OpCode.ChapterScript, "chapter_script");
        Register(CommandCategory.Compatibility, OpCode.EndChapter, "endchapter");
        Register(CommandCategory.Text, OpCode.FontFilter, "font_filter");
    }

    public static IReadOnlyDictionary<string, CommandInfo> All => Commands;

    public static bool Contains(string name) => Commands.ContainsKey(name);

    public static bool TryGet(string name, out OpCode opCode)
    {
        if (Commands.TryGetValue(name, out var info))
        {
            opCode = info.OpCode;
            return true;
        }

        opCode = default;
        return false;
    }

    public static CommandInfo? GetInfo(string name)
    {
        return Commands.TryGetValue(name, out var info) ? info : null;
    }

    public static CommandInfo? GetInfo(OpCode opCode)
    {
        return CommandsByOpCode.TryGetValue(opCode, out var info) ? info : null;
    }

    private static void Register(CommandCategory category, OpCode opCode, string canonicalName, params string[] aliases)
    {
        var info = new CommandInfo(
            opCode,
            canonicalName,
            category,
            aliases,
            GetDefaultMinArgs(opCode),
            canonicalName,
            category == CommandCategory.Compatibility);
        Commands[canonicalName] = info;
        CommandsByOpCode.TryAdd(opCode, info);
        foreach (var alias in aliases)
        {
            Commands[alias] = info;
        }
    }

    private static int GetDefaultMinArgs(OpCode opCode)
    {
        return opCode switch
        {
            OpCode.SetFlag or OpCode.SetPFlag or OpCode.SetSFlag or OpCode.SetVFlag => 2,
            OpCode.SetCounter => 2,
            OpCode.GetFlag or OpCode.GetPFlag or OpCode.ClearFlag or OpCode.ClearPFlag or OpCode.ToggleFlag or OpCode.TogglePFlag => 1,
            OpCode.GetSFlag or OpCode.ClearSFlag or OpCode.ToggleSFlag => 1,
            OpCode.GetVFlag or OpCode.ClearVFlag or OpCode.ToggleVFlag => 1,
            OpCode.IncCounter or OpCode.DecCounter or OpCode.GetCounter => 1,
            OpCode.Mov or OpCode.Add or OpCode.Sub or OpCode.Mul or OpCode.Div or OpCode.Mod => 2,
            OpCode.Cmp => 2,
            OpCode.Beq or OpCode.Bne or OpCode.Bgt or OpCode.Blt or OpCode.Jmp or OpCode.Gosub => 1,
            OpCode.Lsp => 4,
            OpCode.LspText => 4,
            OpCode.LspRect => 5,
            OpCode.Csp => 1,
            OpCode.Vsp => 2,
            OpCode.Msp or OpCode.MspRel => 3,
            OpCode.SpZ or OpCode.SpAlpha or OpCode.SpFontsize or OpCode.SpColor => 2,
            OpCode.SpScale => 3,
            OpCode.SpFill => 3,
            OpCode.Btn or OpCode.BtnClear => 1,
            OpCode.BtnArea => 5,
            OpCode.SpBtn => 2,
            OpCode.BtnTime => 1,
            OpCode.Window => 3,
            OpCode.Caption or OpCode.WindowTitle or OpCode.Font or OpCode.FontAtlasSize or OpCode.Script => 1,
            _ => 0
        };
    }
}
