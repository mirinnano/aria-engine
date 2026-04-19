using System;
using System.Collections.Generic;
using System.Linq;

namespace AriaEngine.Core;

public class Parser
{
    private readonly ErrorReporter _reporter;

    public Parser(ErrorReporter reporter)
    {
        _reporter = reporter;
    }

    private static readonly Dictionary<string, OpCode> KnownCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        { "load_bg", OpCode.LoadBg }, { "load_ch", OpCode.LoadCh },
        { "show_ch", OpCode.ShowCh }, { "hide_ch", OpCode.HideCh },
        { "clr", OpCode.Clr }, { "wait", OpCode.Wait },
        { "textbox", OpCode.Textbox },
        { "fontsize", OpCode.Fontsize },
        { "textcolor", OpCode.Textcolor },
        { "textbox_color", OpCode.TextboxColor },
        { "textbox_hide", OpCode.TextboxHide },
        { "textbox_show", OpCode.TextboxShow },
        { "setwindow", OpCode.SetWindow },
        { "erasetextwindow", OpCode.EraseTextWindow },
        { "textclear", OpCode.TextClear },
        { "textspeed", OpCode.TextSpeed },
        { "defaultspeed", OpCode.DefaultSpeed },
        { "textmode", OpCode.TextMode },
        { "br", OpCode.Br },
        { "\\", OpCode.WaitClickClear },
        { "@", OpCode.WaitClick },

        // システム
        { "yesnobox", OpCode.YesNoBox },
        { "mesbox", OpCode.MesBox },
        { "saveon", OpCode.SaveOn },
        { "saveoff", OpCode.SaveOff },
        { "save", OpCode.Save },
        { "load", OpCode.Load },
        { "lookback_on", OpCode.LookbackOn },
        { "lookback_off", OpCode.LookbackOff },
        { "automode_time", OpCode.AutoModeTime },
        { "rmenu", OpCode.RightMenu },

        // 演出・スクリプト
        { "delay", OpCode.Delay },
        { "rnd", OpCode.Rnd },
        { "inc", OpCode.Inc },
        { "dec", OpCode.Dec },
        { "for", OpCode.For },
        { "next", OpCode.Next },
        { "resettimer", OpCode.ResetTimer },
        { "gettimer", OpCode.GetTimer },
        { "waittimer", OpCode.WaitTimer },

        // オーディオ
        { "mov", OpCode.Mov }, { "numalias", OpCode.Alias },
        { "add", OpCode.Add }, { "sub", OpCode.Sub }, { "mul", OpCode.Mul }, { "div", OpCode.Div }, { "mod", OpCode.Mod },
        { "cmp", OpCode.Cmp }, { "beq", OpCode.Beq }, { "bne", OpCode.Bne }, { "bgt", OpCode.Bgt }, { "blt", OpCode.Blt },
        { "jmp", OpCode.Jmp }, { "goto", OpCode.Jmp }, { "choice", OpCode.Choice }, { "end", OpCode.End },
        { "gosub", OpCode.Gosub }, { "return", OpCode.Return }, { "defsub", OpCode.Defsub }, { "getparam", OpCode.Getparam },
        { "systemcall", OpCode.SystemCall },

        { "lsp", OpCode.Lsp }, { "lsp_text", OpCode.LspText }, { "lsp_rect", OpCode.LspRect },
        { "csp", OpCode.Csp },
        { "vsp", OpCode.Vsp },
        { "msp", OpCode.Msp },
        { "msp_rel", OpCode.MspRel },
        { "sp_z", OpCode.SpZ },
        { "sp_alpha", OpCode.SpAlpha },
        { "sp_scale", OpCode.SpScale },
        { "sp_fontsize", OpCode.SpFontsize },
        { "sp_color", OpCode.SpColor },
        { "sp_fill", OpCode.SpFill },

        // 装飾
        { "sp_round", OpCode.SpRound },
        { "sp_border", OpCode.SpBorder },
        { "sp_gradient", OpCode.SpGradient },
        { "sp_shadow", OpCode.SpShadow },
        { "sp_text_shadow", OpCode.SpTextShadow },
        { "sp_text_outline", OpCode.SpTextOutline },
        { "sp_text_align", OpCode.SpTextAlign },
        { "sp_rotation", OpCode.SpRotation },
        { "sp_hover_color", OpCode.SpHoverColor },
        { "sp_hover_scale", OpCode.SpHoverScale },
        { "sp_cursor", OpCode.SpCursor },

        // アニメーション
        { "amsp", OpCode.Amsp },
        { "afade", OpCode.Afade },
        { "ascale", OpCode.Ascale },
        { "acolor", OpCode.Acolor },
        { "await", OpCode.Await },
        { "ease", OpCode.Ease },

        // ボタン
        { "btn", OpCode.Btn }, { "btn_area", OpCode.BtnArea },
        { "btn_clear", OpCode.BtnClear }, { "btn_clear_all", OpCode.BtnClearAll }, { "btndef", OpCode.BtnClearAll },
        { "btnwait", OpCode.BtnWait }, { "textbtnwait", OpCode.BtnWait }, { "spbtn", OpCode.SpBtn }, { "btntime", OpCode.BtnTime },

        { "bg", OpCode.Bg }, { "print", OpCode.Print }, { "effect", OpCode.Effect }, { "quakex", OpCode.Quake }, { "quake", OpCode.Quake },

        { "play_bgm", OpCode.PlayBgm }, { "stop_bgm", OpCode.StopBgm }, { "bgm", OpCode.PlayBgm },
        { "play_se", OpCode.PlaySe }, { "play_mp3", OpCode.PlayMp3 }, { "mp3loop", OpCode.PlayMp3 },
        { "fade_in", OpCode.FadeIn }, { "fade_out", OpCode.FadeOut },
        { "bgmvol", OpCode.BgmVol }, { "sevol", OpCode.SeVol }, { "mp3vol", OpCode.Mp3Vol },
        { "bgmfade", OpCode.BgmFade }, { "mp3fadeout", OpCode.Mp3FadeOut },
        { "dwave", OpCode.Dwave }, { "dwaveloop", OpCode.DwaveLoop }, { "dwavestop", OpCode.DwaveStop },

        { "window", OpCode.Window }, { "font", OpCode.Font },
        { "font_atlas_size", OpCode.FontAtlasSize }, { "script", OpCode.Script },
        { "debug", OpCode.Debug }, { "caption", OpCode.Caption },

        // チャプター選択システム
        { "chapter_select", OpCode.ChapterSelect },
        { "unlock_chapter", OpCode.UnlockChapter },
        { "chapter_thumbnail", OpCode.ChapterThumbnail },
        { "chapter_card", OpCode.ChapterCard },
        { "chapter_scroll", OpCode.ChapterScroll },
        { "chapter_progress", OpCode.ChapterProgress },

        // キャラクター操作
        { "char_load", OpCode.CharLoad },
        { "char_show", OpCode.CharShow },
        { "char_hide", OpCode.CharHide },
        { "char_move", OpCode.CharMove },
        { "char_expression", OpCode.CharExpression },
        { "char_pose", OpCode.CharPose },
        { "char_z", OpCode.CharZ },
        { "char_scale", OpCode.CharScale },

        // ゲームフロー
        { "change_scene", OpCode.ChangeScene },
        { "return_scene", OpCode.ReturnScene },
        { "set_scene_data", OpCode.SetSceneData },
        { "get_scene_data", OpCode.GetSceneData },

        // フラグ管理システム
        { "set_flag", OpCode.SetFlag },
        { "get_flag", OpCode.GetFlag },
        { "clear_flag", OpCode.ClearFlag },
        { "toggle_flag", OpCode.ToggleFlag },
        { "inc_counter", OpCode.IncCounter },
        { "dec_counter", OpCode.DecCounter },
        { "set_counter", OpCode.SetCounter },
        { "get_counter", OpCode.GetCounter },

        // チャプターデータ定義（スクリプト主導）
        { "defchapter", OpCode.DefChapter },
        { "chapter_id", OpCode.ChapterId },
        { "chapter_title", OpCode.ChapterTitle },
        { "chapter_desc", OpCode.ChapterDesc },
        { "chapter_script", OpCode.ChapterScript },
        { "endchapter", OpCode.EndChapter },

        // フォント設定
        { "font_filter", OpCode.FontFilter }
    };

    public (List<Instruction> Instructions, Dictionary<string, int> Labels) Parse(string[] lines, string scriptFile = "")
    {
        var instructions = new List<Instruction>();
        var labels = new Dictionary<string, int>();
        var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pre-pass for Defsubs and Labels
        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = StripComments(rawLine).TrimStart();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("*"))
            {
                var labelName = line.Substring(1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                labels[labelName] = -1; // Temp placeholder
            }
            else if (line.StartsWith("defsub ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = Tokenize(line);
                if (parts.Count > 1) defsubs.Add(parts[1]);
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = StripComments(rawLine).TrimStart();

            if (string.IsNullOrEmpty(line)) continue;

            // Handle labels (multi-commands on same line via ":" is supported by NScripter, but we parse strictly. For ARIA we split by ":" if not in quotes)
            var statements = SplitStatements(line);
            foreach (var stmt in statements)
            {
                if (string.IsNullOrEmpty(stmt)) continue;

                if (stmt.StartsWith("*"))
                {
                    var labelName = stmt.Substring(1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    labels[labelName] = instructions.Count;
                    continue;
                }

                var parts = Tokenize(stmt);
                if (parts.Count == 0) continue;

                string firstToken = parts[0];

                if (firstToken.Equals("if", StringComparison.OrdinalIgnoreCase))
                {
                    int cmdIndex = -1;
                    for (int j = 1; j < parts.Count; j++)
                    {
                        if (KnownCommands.ContainsKey(parts[j]) || defsubs.Contains(parts[j]))
                        {
                            cmdIndex = j;
                            break;
                        }
                    }

                    if (cmdIndex > 1)
                    {
                        var condTokens = parts.GetRange(1, cmdIndex - 1);
                        var cmdToken = parts[cmdIndex];
                        var opArgs = parts.Skip(cmdIndex + 1).ToList();

                        if (KnownCommands.TryGetValue(cmdToken, out OpCode op))
                            instructions.Add(new Instruction(op, opArgs, i + 1, condTokens));
                        else if (defsubs.Contains(cmdToken))
                            instructions.Add(new Instruction(OpCode.Gosub, new List<string> { cmdToken }.Concat(opArgs).ToList(), i + 1, condTokens));
                    }
                    continue;
                }

                if (KnownCommands.TryGetValue(firstToken, out OpCode statementOp))
                {
                    var args = parts.Skip(1).ToList();
                    instructions.Add(new Instruction(statementOp, args, i + 1));
                }
                else if (defsubs.Contains(firstToken))
                {
                    instructions.Add(new Instruction(OpCode.Gosub, new List<string> { firstToken }.Concat(parts.Skip(1)).ToList(), i + 1));
                }
                else
                {
                    string textData = stmt.TrimEnd().Replace("\\n", "\n");
                    var match = System.Text.RegularExpressions.Regex.Match(textData, @"^([^「]+?)「(.*?)」(\\?|@?)$");
                    
                    if (match.Success)
                    {
                        // 構文シュガー: Name「Text」 の場合、自動で textclear を挿入
                        instructions.Add(new Instruction(OpCode.TextClear, new List<string>(), i + 1));
                        
                        // 末尾に \ や @ がなければ自動で \（クリック待ち＆改ページ）を付与
                        if (match.Groups[3].Value == "") textData += "\\";
                    }

                    string buf = "";
                    for (int c = 0; c < textData.Length; c++)
                    {
                        if (textData[c] == '\\') 
                        {
                            if (buf.Length > 0) { instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1)); buf = ""; }
                            instructions.Add(new Instruction(OpCode.WaitClickClear, new List<string>(), i + 1));
                        }
                        else if (textData[c] == '@')
                        {
                            if (buf.Length > 0) { instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1)); buf = ""; }
                            instructions.Add(new Instruction(OpCode.WaitClick, new List<string>(), i + 1));
                        }
                        else
                        {
                            buf += textData[c];
                        }
                    }
                    if (buf.Length > 0)
                    {
                        instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1));
                    }
                }
            }
        }

        foreach (var inst in instructions)
        {
            if (inst.Op == OpCode.Jmp || inst.Op == OpCode.Beq || inst.Op == OpCode.Bne || 
                inst.Op == OpCode.Bgt || inst.Op == OpCode.Blt || inst.Op == OpCode.Gosub)
            {
                if (inst.Arguments.Count > 0 && inst.Op != OpCode.Gosub)
                {
                    string target = inst.Arguments[0].TrimStart('*');
                    if (!labels.ContainsKey(target))
                        _reporter.Report(new AriaError($"未定義のラベル '*{target}' へのジャンプです。", inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                }
                else if (inst.Op == OpCode.Gosub)
                {
                    string target = inst.Arguments[0].TrimStart('*');
                    if (!labels.ContainsKey(target))
                        _reporter.Report(new AriaError($"未定義のサブルーチン/ラベル '{target}' の呼び出しです。", inst.SourceLine, scriptFile, AriaErrorLevel.Error));
                }
            }
        }

        return (instructions, labels);
    }

    private string StripComments(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes)
            {
                if (line[i] == ';') return line.Substring(0, i);
                if (i < line.Length - 1 && line[i] == '/' && line[i + 1] == '/') return line.Substring(0, i);
            }
        }
        return line;
    }

    private List<string> SplitStatements(string line)
    {
        var result = new List<string>();
        int start = 0;
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            if (!inQuotes && line[i] == ':')
            {
                result.Add(line.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        result.Add(line.Substring(start).Trim());
        return result;
    }

    private List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        int i = 0;
        
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }
            if (line[i] == ',') { i++; continue; }

            if (line[i] == '"')
            {
                i++;
                int start = i;
                while (i < line.Length && line[i] != '"') i++;
                tokens.Add(line.Substring(start, i - start));
                if (i < line.Length) i++;
            }
            else
            {
                int start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != ',' && line[i] != '"')
                {
                    // Handle comparison operators
                    if (i + 1 < line.Length && line.Substring(i, 2) == "==")
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("==");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line.Substring(i, 2) == "!=")
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("!=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line.Substring(i, 2) == ">=")
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add(">=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (i + 1 < line.Length && line.Substring(i, 2) == "<=")
                    {
                        if (i > start) break;
                        else
                        {
                            tokens.Add("<=");
                            i += 2;
                            start = i;
                            continue;
                        }
                    }
                    else if (line[i] == '=')
                    {
                        if (i > start) break;
                        else { i++; break; }
                    }
                    i++;
                }

                string t = line.Substring(start, i - start);
                // Replace \n inside strings as well to support command arguments
                if (!string.IsNullOrEmpty(t))
                {
                    tokens.Add(t.Replace("\\n", "\n"));
                }
            }
        }
        return tokens;
    }
}
