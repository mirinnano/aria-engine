using System;
using System.Collections.Generic;
using System.Linq;
using AriaEngine.Core;

namespace AriaEngine.Compiler;

/// <summary>
/// AriaスクリプトのInstructionからバイトコードを生成するコンパイラ
/// </summary>
public class BytecodeCompiler
{
    private readonly ErrorReporter _reporter;
    private readonly BytecodeGenerator _generator;
    private readonly Dictionary<string, int> _stringTable;
    private readonly Dictionary<int, int> _localIndices;
    private readonly Dictionary<string, int> _globalIndices;

    public BytecodeCompiler(ErrorReporter reporter)
    {
        _reporter = reporter;
        _generator = new BytecodeGenerator();
        _stringTable = new Dictionary<string, int>(StringComparer.Ordinal);
        _localIndices = new Dictionary<int, int>();
        _globalIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Instructionリストをバイトコードファイルにコンパイル
    /// </summary>
    public BytecodeFile Compile(IReadOnlyList<Instruction> instructions, string scriptName)
    {
        var file = new BytecodeFile();

        // メイン関数を追加
        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 0,
            EntryPoint = 0,
            LocalCount = 0,
            ParamCount = 0,
            ReturnType = (byte)ReturnType.Void
        });

        // スクリプト名を文字列テーブルに追加
        AddString(scriptName);

        // 命令をバイトコードに変換
        foreach (var inst in instructions)
        {
            CompileInstruction(inst);
        }

        // ラベルを解決
        _generator.ResolveLabels();

        // バイトコードを設定
        file.Code = _generator.ToByteArray();

        // 文字列テーブルを設定
        file.Strings.AddRange(_stringTable.Keys.OrderBy(k => _stringTable[k]));

        // 関数テーブルのオフセットを修正
        for (int i = 0; i < file.Functions.Count; i++)
        {
            var func = file.Functions[i];
            func.NameOffset = (uint)_stringTable[scriptName];
            file.Functions[i] = func;
        }

        return file;
    }

    private void CompileInstruction(Instruction inst)
    {
        switch (inst.Op)
        {
            // テキスト操作
            case OpCode.Text:
                CompileText(inst);
                break;
            case OpCode.TextClear:
                _generator.EmitOp(BytecodeOpCode.TextClear);
                break;
            case OpCode.WaitClick:
                _generator.EmitOp(BytecodeOpCode.WaitClick);
                break;
            case OpCode.WaitClickClear:
                _generator.EmitOp(BytecodeOpCode.WaitClickClear);
                break;

            // スプライト操作
            case OpCode.Lsp:
                CompileLsp(inst);
                break;
            case OpCode.LspText:
                CompileLspText(inst);
                break;
            case OpCode.LspRect:
                CompileLspRect(inst);
                break;
            case OpCode.Vsp:
                CompileVsp(inst);
                break;
            case OpCode.Msp:
                CompileMsp(inst);
                break;
            case OpCode.MspRel:
                CompileMspRel(inst);
                break;
            case OpCode.SpAlpha:
                CompileSpAlpha(inst);
                break;
            case OpCode.SpScale:
                CompileSpScale(inst);
                break;
            case OpCode.SpRotation:
                CompileSpRotation(inst);
                break;
            case OpCode.SpColor:
                CompileSpColor(inst);
                break;
            case OpCode.SpZ:
                CompileSpZ(inst);
                break;
            case OpCode.Csp:
                CompileCsp(inst);
                break;

            // 背景操作
            case OpCode.Bg:
                CompileBg(inst);
                break;
            case OpCode.LoadBg:
                CompileLoadBg(inst);
                break;

            // 音声操作
            case OpCode.PlayBgm:
                CompilePlayBgm(inst);
                break;
            case OpCode.StopBgm:
                CompileStopBgm(inst);
                break;
            case OpCode.PlaySe:
                CompilePlaySe(inst);
                break;

            // 制御フロー
            case OpCode.Jmp:
                CompileJmp(inst);
                break;
            case OpCode.Gosub:
                CompileGosub(inst);
                break;
            case OpCode.Return:
                _generator.EmitOp(BytecodeOpCode.Return);
                break;

            // 待機
            case OpCode.Wait:
                CompileWait(inst);
                break;

            // レジスタ操作
            case OpCode.Let:
                CompileLet(inst);
                break;
            case OpCode.Add:
                CompileAdd(inst);
                break;
            case OpCode.Sub:
                CompileSub(inst);
                break;
            case OpCode.Mul:
                CompileMul(inst);
                break;
            case OpCode.Div:
                CompileDiv(inst);
                break;

            // 比較操作
            case OpCode.Cmp:
                CompileCmp(inst);
                break;
            case OpCode.Beq:
                CompileBeq(inst);
                break;
            case OpCode.Bne:
                CompileBne(inst);
                break;
            case OpCode.Blt:
                CompileBlt(inst);
                break;
            case OpCode.Bgt:
                CompileBgt(inst);
                break;

            // システム
            case OpCode.Save:
                CompileSave(inst);
                break;
            case OpCode.Load:
                CompileLoad(inst);
                break;

            // UI
            case OpCode.Textbox:
                CompileTextbox(inst);
                break;

            // 未実装の命令
            default:
                _reporter.Report(new AriaError(
                    $"Bytecode compiler: Instruction not implemented: {inst.Op}",
                    inst.SourceLine,
                    "unknown.aria",
                    AriaErrorLevel.Warning,
                    "COMPILER_NOT_IMPLEMENTED"));
                break;
        }
    }

    private void CompileText(Instruction inst)
    {
        if (inst.Arguments.Count == 0) return;

        string text = inst.Arguments[0];
        int stringIndex = AddString(text);

        _generator.EmitOp(BytecodeOpCode.Text);
        _generator.EmitInt(stringIndex);
    }

    private void CompileLsp(Instruction inst)
    {
        if (inst.Arguments.Count < 3) return;

        int id = ParseInt(inst.Arguments[0]);
        string path = inst.Arguments[1];
        int x = ParseInt(inst.Arguments[2]);
        int y = inst.Arguments.Count > 3 ? ParseInt(inst.Arguments[3]) : 0;

        int pathIndex = AddString(path);

        _generator.EmitOp(BytecodeOpCode.SpriteLoad);
        _generator.EmitInt(id);
        _generator.EmitInt(pathIndex);
        _generator.EmitInt(x);
        _generator.EmitInt(y);
    }

    private void CompileLspText(Instruction inst)
    {
        if (inst.Arguments.Count < 3) return;

        int id = ParseInt(inst.Arguments[0]);
        string text = inst.Arguments[1];
        int x = ParseInt(inst.Arguments[2]);
        int y = inst.Arguments.Count > 3 ? ParseInt(inst.Arguments[3]) : 0;

        int textIndex = AddString(text);

        _generator.EmitOp(BytecodeOpCode.SpriteTextLoad);
        _generator.EmitInt(id);
        _generator.EmitInt(textIndex);
        _generator.EmitInt(x);
        _generator.EmitInt(y);
    }

    private void CompileLspRect(Instruction inst)
    {
        if (inst.Arguments.Count < 5) return;

        int id = ParseInt(inst.Arguments[0]);
        int x = ParseInt(inst.Arguments[1]);
        int y = ParseInt(inst.Arguments[2]);
        int width = ParseInt(inst.Arguments[3]);
        int height = ParseInt(inst.Arguments[4]);

        _generator.EmitOp(BytecodeOpCode.SpriteRectLoad);
        _generator.EmitInt(id);
        _generator.EmitInt(x);
        _generator.EmitInt(y);
        _generator.EmitInt(width);
        _generator.EmitInt(height);
    }

    private void CompileVsp(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int id = ParseInt(inst.Arguments[0]);
        string visible = inst.Arguments[1];
        bool isVisible = visible.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                         visible.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         visible == "1";

        _generator.EmitOp(BytecodeOpCode.SpriteVisible);
        _generator.EmitInt(id);
        _generator.EmitInt(isVisible ? 1 : 0);
    }

    private void CompileMsp(Instruction inst)
    {
        if (inst.Arguments.Count < 3) return;

        int id = ParseInt(inst.Arguments[0]);
        int x = ParseInt(inst.Arguments[1]);
        int y = ParseInt(inst.Arguments[2]);

        _generator.EmitOp(BytecodeOpCode.SpriteMove);
        _generator.EmitInt(id);
        _generator.EmitInt(x);
        _generator.EmitInt(y);
    }

    private void CompileMspRel(Instruction inst)
    {
        if (inst.Arguments.Count < 3) return;

        int id = ParseInt(inst.Arguments[0]);
        int dx = ParseInt(inst.Arguments[1]);
        int dy = ParseInt(inst.Arguments[2]);

        _generator.EmitOp(BytecodeOpCode.SpriteMoveRel);
        _generator.EmitInt(id);
        _generator.EmitInt(dx);
        _generator.EmitInt(dy);
    }

    private void CompileSpAlpha(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int id = ParseInt(inst.Arguments[0]);
        int alpha = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.SpriteAlpha);
        _generator.EmitInt(id);
        _generator.EmitInt(alpha);
    }

    private void CompileSpScale(Instruction inst)
    {
        if (inst.Arguments.Count < 3) return;

        int id = ParseInt(inst.Arguments[0]);
        float scaleX = ParseFloat(inst.Arguments[1]);
        float scaleY = ParseFloat(inst.Arguments[2]);

        _generator.EmitOp(BytecodeOpCode.SpriteScale);
        _generator.EmitInt(id);
        _generator.EmitFloat(scaleX);
        _generator.EmitFloat(scaleY);
    }

    private void CompileSpRotation(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int id = ParseInt(inst.Arguments[0]);
        float rotation = ParseFloat(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.SpriteRotation);
        _generator.EmitInt(id);
        _generator.EmitFloat(rotation);
    }

    private void CompileSpColor(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int id = ParseInt(inst.Arguments[0]);
        string color = inst.Arguments[1];
        int colorIndex = AddString(color);

        _generator.EmitOp(BytecodeOpCode.SpriteColor);
        _generator.EmitInt(id);
        _generator.EmitInt(colorIndex);
    }

    private void CompileSpZ(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int id = ParseInt(inst.Arguments[0]);
        int z = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.SpriteZ);
        _generator.EmitInt(id);
        _generator.EmitInt(z);
    }

    private void CompileCsp(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        int id = ParseInt(inst.Arguments[0]);

        _generator.EmitOp(BytecodeOpCode.SpriteDelete);
        _generator.EmitInt(id);
    }

    private void CompileBg(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string path = inst.Arguments[0];
        int duration = inst.Arguments.Count > 1 ? ParseInt(inst.Arguments[1]) : 0;

        int pathIndex = AddString(path);

        _generator.EmitOp(BytecodeOpCode.BackgroundSet);
        _generator.EmitInt(pathIndex);
        _generator.EmitInt(duration);
    }

    private void CompileLoadBg(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string path = inst.Arguments[0];
        int pathIndex = AddString(path);

        _generator.EmitOp(BytecodeOpCode.BackgroundLoad);
        _generator.EmitInt(pathIndex);
    }

    private void CompilePlayBgm(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string path = inst.Arguments[0];
        int volume = inst.Arguments.Count > 1 ? ParseInt(inst.Arguments[1]) : 100;

        int pathIndex = AddString(path);

        _generator.EmitOp(BytecodeOpCode.BGMPlay);
        _generator.EmitInt(pathIndex);
        _generator.EmitInt(volume);
    }

    private void CompileStopBgm(Instruction inst)
    {
        int duration = inst.Arguments.Count > 0 ? ParseInt(inst.Arguments[0]) : 0;

        _generator.EmitOp(BytecodeOpCode.BGMStop);
        _generator.EmitInt(duration);
    }

    private void CompilePlaySe(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string path = inst.Arguments[0];
        int channel = inst.Arguments.Count > 1 ? ParseInt(inst.Arguments[1]) : 0;
        int volume = inst.Arguments.Count > 2 ? ParseInt(inst.Arguments[2]) : 100;

        int pathIndex = AddString(path);

        _generator.EmitOp(BytecodeOpCode.SEPlay);
        _generator.EmitInt(pathIndex);
        _generator.EmitInt(channel);
        _generator.EmitInt(volume);
    }

    private void CompileJmp(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.Jump);
        _generator.EmitStringRef(label);
    }

    private void CompileGosub(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.Call);
        _generator.EmitStringRef(label);
    }

    private void CompileWait(Instruction inst)
    {
        int duration = inst.Arguments.Count > 0 ? ParseInt(inst.Arguments[0]) : 0;

        _generator.EmitOp(BytecodeOpCode.Wait);
        _generator.EmitInt(duration);
    }

    private void CompileLet(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.StoreRegister);
        _generator.EmitInt(regIndex);
    }

    private void CompileAdd(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.LoadRegister);
        _generator.EmitInt(regIndex);
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.Add);
        _generator.EmitOp(BytecodeOpCode.StoreRegister);
        _generator.EmitInt(regIndex);
    }

    private void CompileSub(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.LoadRegister);
        _generator.EmitInt(regIndex);
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.Sub);
        _generator.EmitOp(BytecodeOpCode.StoreRegister);
        _generator.EmitInt(regIndex);
    }

    private void CompileMul(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.LoadRegister);
        _generator.EmitInt(regIndex);
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.Mul);
        _generator.EmitOp(BytecodeOpCode.StoreRegister);
        _generator.EmitInt(regIndex);
    }

    private void CompileDiv(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.LoadRegister);
        _generator.EmitInt(regIndex);
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.Div);
        _generator.EmitOp(BytecodeOpCode.StoreRegister);
        _generator.EmitInt(regIndex);
    }

    private void CompileCmp(Instruction inst)
    {
        if (inst.Arguments.Count < 2) return;

        int regIndex = ParseRegIndex(inst.Arguments[0]);
        int value = ParseInt(inst.Arguments[1]);

        _generator.EmitOp(BytecodeOpCode.LoadRegister);
        _generator.EmitInt(regIndex);
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        _generator.EmitOp(BytecodeOpCode.Cmp);
    }

    private void CompileBeq(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.JumpIfTrue);
        _generator.EmitStringRef(label);
    }

    private void CompileBne(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.JumpIfFalse);
        _generator.EmitStringRef(label);
    }

    private void CompileBlt(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.JumpIfLess);
        _generator.EmitStringRef(label);
    }

    private void CompileBgt(Instruction inst)
    {
        if (inst.Arguments.Count < 1) return;

        string label = inst.Arguments[0];
        _generator.EmitOp(BytecodeOpCode.JumpIfGreater);
        _generator.EmitStringRef(label);
    }

    private void CompileSave(Instruction inst)
    {
        int slot = inst.Arguments.Count > 0 ? ParseInt(inst.Arguments[0]) : 0;

        _generator.EmitOp(BytecodeOpCode.Save);
        _generator.EmitInt(slot);
    }

    private void CompileLoad(Instruction inst)
    {
        int slot = inst.Arguments.Count > 0 ? ParseInt(inst.Arguments[0]) : 0;

        _generator.EmitOp(BytecodeOpCode.Load);
        _generator.EmitInt(slot);
    }

    private void CompileTextbox(Instruction inst)
    {
        if (inst.Arguments.Count < 4) return;

        int x = ParseInt(inst.Arguments[0]);
        int y = ParseInt(inst.Arguments[1]);
        int width = ParseInt(inst.Arguments[2]);
        int height = ParseInt(inst.Arguments[3]);

        _generator.EmitOp(BytecodeOpCode.TextboxSet);
        _generator.EmitInt(x);
        _generator.EmitInt(y);
        _generator.EmitInt(width);
        _generator.EmitInt(height);
    }

    private int AddString(string value)
    {
        if (!_stringTable.TryGetValue(value, out int index))
        {
            index = _stringTable.Count;
            _stringTable[value] = index;
        }
        return index;
    }

    private int ParseInt(string value)
    {
        if (int.TryParse(value, out int result))
            return result;

        _reporter.Report(new AriaError(
            $"Failed to parse integer: {value}",
            -1,
            "bytecode.aria",
            AriaErrorLevel.Error,
            "COMPILER_PARSE_INT"));
        return 0;
    }

    private float ParseFloat(string value)
    {
        if (float.TryParse(value, out float result))
            return result;

        _reporter.Report(new AriaError(
            $"Failed to parse float: {value}",
            -1,
            "bytecode.aria",
            AriaErrorLevel.Error,
            "COMPILER_PARSE_FLOAT"));
        return 0f;
    }

    private int ParseRegIndex(string value)
    {
        if (value.StartsWith("%") && int.TryParse(value.Substring(1), out int regIndex))
        {
            return regIndex;
        }

        _reporter.Report(new AriaError(
            $"Invalid register format: {value}",
            -1,
            "bytecode.aria",
            AriaErrorLevel.Error,
            "COMPILER_PARSE_REG"));
        return 0;
    }
}
