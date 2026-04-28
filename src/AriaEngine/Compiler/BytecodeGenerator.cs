using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AriaEngine.Compiler;

/// <summary>
/// バイトコード生成器
/// 高レベルな中間表現からバイトコードを生成します
/// </summary>
public class BytecodeGenerator
{
    private readonly List<byte> _code = new();
    private readonly Dictionary<string, int> _labels = new();
    private readonly List<LabelFixup> _labelFixups = new();
    private int _currentPosition = 0;

    /// <summary>
    /// 生成されたバイトコードを取得
    /// </summary>
    public ReadOnlySpan<byte> Code => _code.ToArray();

    /// <summary>
    /// 現在のコード位置
    /// </summary>
    public int Position => _currentPosition;

    /// <summary>
    /// オペコードを発行
    /// </summary>
    public void EmitOp(BytecodeOpCode op)
    {
        _code.Add((byte)op);
        _currentPosition++;
    }

    /// <summary>
    /// 整数を発行
    /// </summary>
    public void EmitInt(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _code.AddRange(bytes);
        _currentPosition += 4;
    }

    /// <summary>
    /// 浮動小数点数を発行
    /// </summary>
    public void EmitFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _code.AddRange(bytes);
        _currentPosition += 4;
    }

    /// <summary>
    /// 文字列インデックスを発行
    /// </summary>
    public void EmitStringIndex(int index)
    {
        EmitInt(index);
    }

    /// <summary>
    /// 関数インデックスを発行
    /// </summary>
    public void EmitFunctionIndex(ushort index)
    {
        byte[] bytes = BitConverter.GetBytes(index);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _code.AddRange(bytes);
        _currentPosition += 2;
    }

    /// <summary>
    /// ローカル変数インデックスを発行
    /// </summary>
    public void EmitLocalIndex(ushort index)
    {
        EmitFunctionIndex(index);
    }

    /// <summary>
    /// レジスタ番号を発行
    /// </summary>
    public void EmitRegister(byte reg)
    {
        _code.Add(reg);
        _currentPosition++;
    }

    /// <summary>
    /// ラベルを定義（現在の位置をラベルとして記録）
    /// </summary>
    public void EmitLabel(string name)
    {
        if (_labels.ContainsKey(name))
        {
            throw new InvalidOperationException($"Label '{name}' is already defined");
        }
        _labels[name] = _currentPosition;
    }

    /// <summary>
    /// 文字列参照を発行（未解決のラベルや関数名）
    /// </summary>
    public void EmitStringRef(string reference)
    {
        // プレースホルダーとして0を発行
        EmitInt(0);

        // 修正情報を記録
        _labelFixups.Add(new LabelFixup
        {
            LabelName = reference,
            FixupPosition = _currentPosition - 4
        });
    }

    /// <summary>
    /// ジャンプターゲットを発行（未解決のラベル）
    /// </summary>
    public void EmitJumpTarget(string label)
    {
        EmitStringRef(label);
    }

    /// <summary>
    /// 相対ジャンプを発行
    /// </summary>
    public void EmitRelativeJump(int offset)
    {
        EmitInt(offset);
    }

    /// <summary>
    /// バイト列を発行
    /// </summary>
    public void EmitBytes(byte[] bytes)
    {
        _code.AddRange(bytes);
        _currentPosition += bytes.Length;
    }

    /// <summary>
    /// N個のNOPを発行（アライメント用）
    /// </summary>
    public void EmitNop(int count)
    {
        for (int i = 0; i < count; i++)
        {
            EmitOp(BytecodeOpCode.Nop);
        }
    }

    /// <summary>
    /// すべてのラベル参照を解決
    /// </summary>
    public void ResolveLabels()
    {
        foreach (var fixup in _labelFixups)
        {
            if (!_labels.TryGetValue(fixup.LabelName, out int targetPosition))
            {
                throw new InvalidOperationException($"Undefined label: {fixup.LabelName}");
            }

            // ジャンプターゲットを計算
            int jumpTarget = targetPosition - (fixup.FixupPosition + 4);

            // 修正位置にジャンプターゲットを書き込み
            byte[] bytes = BitConverter.GetBytes(jumpTarget);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            for (int i = 0; i < 4; i++)
            {
                _code[fixup.FixupPosition + i] = bytes[i];
            }
        }

        _labelFixups.Clear();
    }

    /// <summary>
    /// 未解決のラベルがあるかチェック
    /// </summary>
    public bool HasUnresolvedLabels()
    {
        return _labelFixups.Count > 0;
    }

    /// <summary>
    /// 未解決のラベルリストを取得
    /// </summary>
    public IReadOnlyList<string> GetUnresolvedLabels()
    {
        var labels = new HashSet<string>();
        foreach (var fixup in _labelFixups)
        {
            labels.Add(fixup.LabelName);
        }
        return labels.ToList();
    }

    /// <summary>
    /// 現在の位置にブレークポイントを挿入
    /// </summary>
    public void EmitBreakpoint()
    {
        EmitOp(BytecodeOpCode.DebugBreak);
    }

    /// <summary>
    /// デバッグログを挿入
    /// </summary>
    public void EmitDebugLog(int stringIndex)
    {
        EmitOp(BytecodeOpCode.DebugLog);
        EmitStringIndex(stringIndex);
    }

    /// <summary>
    /// バイトコードをクリア
    /// </summary>
    public void Clear()
    {
        _code.Clear();
        _labels.Clear();
        _labelFixups.Clear();
        _currentPosition = 0;
    }

    /// <summary>
    /// バイトコードをバイト配列に変換
    /// </summary>
    public byte[] ToByteArray()
    {
        if (HasUnresolvedLabels())
        {
            throw new InvalidOperationException($"Cannot convert to byte array: unresolved labels exist: {string.Join(", ", GetUnresolvedLabels())}");
        }
        return _code.ToArray();
    }

    /// <summary>
    /// バイトコードを16進数ダンプで取得（デバッグ用）
    /// </summary>
    public string DumpHex()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _code.Count; i += 16)
        {
            sb.AppendFormat("{0:X4}: ", i);
            for (int j = 0; j < 16; j++)
            {
                if (i + j < _code.Count)
                {
                    sb.AppendFormat("{0:X2} ", _code[i + j]);
                }
                else
                {
                    sb.Append("   ");
                }
                if (j == 7) sb.Append(" ");
            }
            sb.Append(" |");
            for (int j = 0; j < 16 && i + j < _code.Count; j++)
            {
                byte b = _code[i + j];
                if (b >= 32 && b <= 126)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('.');
                }
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }

    /// <summary>
    /// ラベル修正情報
    /// </summary>
    private record struct LabelFixup
    {
        public string LabelName { get; init; }
        public int FixupPosition { get; init; }
    }

    /// <summary>
    /// コードセグメントを作成（一時的な位置記録用）
    /// </summary>
    public class CodeSegment : IDisposable
    {
        private readonly BytecodeGenerator _generator;
        private readonly int _startPosition;

        public CodeSegment(BytecodeGenerator generator)
        {
            _generator = generator;
            _startPosition = generator.Position;
        }

        public int StartPosition => _startPosition;

        public int EndPosition => _generator.Position;

        public int Length => EndPosition - StartPosition;

        public void Dispose()
        {
            // 必要に応じて後処理
        }
    }

    /// <summary>
    /// コードセグメントを作成
    /// </summary>
    public CodeSegment CreateSegment()
    {
        return new CodeSegment(this);
    }
}

/// <summary>
/// バイトコード生成のビルダー（Fluent Interface）
/// </summary>
public class BytecodeBuilder
{
    private readonly BytecodeGenerator _generator;

    public BytecodeBuilder(BytecodeGenerator generator)
    {
        _generator = generator;
    }

    /// <summary>
    /// 整数をプッシュ
    /// </summary>
    public BytecodeBuilder PushInt(int value)
    {
        _generator.EmitOp(BytecodeOpCode.PushInt);
        _generator.EmitInt(value);
        return this;
    }

    /// <summary>
    /// 浮動小数点数をプッシュ
    /// </summary>
    public BytecodeBuilder PushFloat(float value)
    {
        _generator.EmitOp(BytecodeOpCode.PushFloat);
        _generator.EmitFloat(value);
        return this;
    }

    /// <summary>
    /// 文字列をプッシュ
    /// </summary>
    public BytecodeBuilder PushString(int stringIndex)
    {
        _generator.EmitOp(BytecodeOpCode.PushString);
        _generator.EmitStringIndex(stringIndex);
        return this;
    }

    /// <summary>
    /// ローカル変数をロード
    /// </summary>
    public BytecodeBuilder LoadLocal(int index)
    {
        _generator.EmitOp(BytecodeOpCode.LoadLocal);
        _generator.EmitLocalIndex((ushort)index);
        return this;
    }

    /// <summary>
    /// ローカル変数にストア
    /// </summary>
    public BytecodeBuilder StoreLocal(int index)
    {
        _generator.EmitOp(BytecodeOpCode.StoreLocal);
        _generator.EmitLocalIndex((ushort)index);
        return this;
    }

    /// <summary>
    /// 加算
    /// </summary>
    public BytecodeBuilder Add()
    {
        _generator.EmitOp(BytecodeOpCode.Add);
        return this;
    }

    /// <summary>
    /// 減算
    /// </summary>
    public BytecodeBuilder Sub()
    {
        _generator.EmitOp(BytecodeOpCode.Sub);
        return this;
    }

    /// <summary>
    /// 乗算
    /// </summary>
    public BytecodeBuilder Mul()
    {
        _generator.EmitOp(BytecodeOpCode.Mul);
        return this;
    }

    /// <summary>
    /// 除算
    /// </summary>
    public BytecodeBuilder Div()
    {
        _generator.EmitOp(BytecodeOpCode.Div);
        return this;
    }

    /// <summary>
    /// 関数呼び出し
    /// </summary>
    public BytecodeBuilder Call(int functionIndex)
    {
        _generator.EmitOp(BytecodeOpCode.Call);
        _generator.EmitFunctionIndex((ushort)functionIndex);
        return this;
    }

    /// <summary>
    /// リターン
    /// </summary>
    public BytecodeBuilder Return()
    {
        _generator.EmitOp(BytecodeOpCode.Return);
        return this;
    }

    /// <summary>
    /// 値を返してリターン
    /// </summary>
    public BytecodeBuilder ReturnValue()
    {
        _generator.EmitOp(BytecodeOpCode.ReturnValue);
        return this;
    }

    /// <summary>
    /// ジャンプ
    /// </summary>
    public BytecodeBuilder Jump(string label)
    {
        _generator.EmitOp(BytecodeOpCode.Jump);
        _generator.EmitJumpTarget(label);
        return this;
    }

    /// <summary>
    /// 条件付きジャンプ（真ならジャンプ）
    /// </summary>
    public BytecodeBuilder JumpIfTrue(string label)
    {
        _generator.EmitOp(BytecodeOpCode.JumpIfTrue);
        _generator.EmitJumpTarget(label);
        return this;
    }

    /// <summary>
    /// 条件付きジャンプ（偽ならジャンプ）
    /// </summary>
    public BytecodeBuilder JumpIfFalse(string label)
    {
        _generator.EmitOp(BytecodeOpCode.JumpIfFalse);
        _generator.EmitJumpTarget(label);
        return this;
    }

    /// <summary>
    /// ラベルを定義
    /// </summary>
    public BytecodeBuilder Label(string name)
    {
        _generator.EmitLabel(name);
        return this;
    }

    /// <summary>
    /// カスタムオペコードを発行
    /// </summary>
    public BytecodeBuilder Emit(BytecodeOpCode op)
    {
        _generator.EmitOp(op);
        return this;
    }

    /// <summary>
    /// カスタムオペコードと整数を発行
    /// </summary>
    public BytecodeBuilder EmitInt(BytecodeOpCode op, int value)
    {
        _generator.EmitOp(op);
        _generator.EmitInt(value);
        return this;
    }
}
