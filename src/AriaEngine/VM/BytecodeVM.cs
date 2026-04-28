using System;
using System.Collections.Generic;
using AriaEngine.Compiler;

namespace AriaEngine.VM;

/// <summary>
/// バイトコード仮想マシン
/// .aribバイトコードファイルを実行します
/// </summary>
public class BytecodeVM
{
    // ========================================
    // 状態
    // ========================================

    private byte[] _code = Array.Empty<byte>();
    private int _pc;  // Program Counter

    // スタック
    private readonly Stack<object> _stack = new();
    private const int MaxStackSize = 1024;

    // メモリ
    private object[] _locals = Array.Empty<object>();
    private readonly Dictionary<string, object> _globals = new();
    private readonly int[] _registers = new int[256];  // レジスタファイル

    // コールスタック
    private readonly Stack<CallFrame> _callStack = new();
    private const int MaxCallDepth = 256;

    // バイトコードファイル情報
    private BytecodeFile _bytecodeFile = null!;
    private readonly Dictionary<int, FunctionEntry> _functionIndexMap = new();
    private readonly Dictionary<uint, string> _offsetToLabelMap = new();

    // 実行状態
    private bool _isRunning;
    private bool _isPaused;

    // デバッグ情報
    private readonly Dictionary<uint, LineInfo> _codeOffsetToLineInfo = new();

    // イベント/コールバック
    public event Action<string>? OnDebugLog;
    public event Action? OnBreakpoint;
    public event Action<string, Exception>? OnError;

    // ========================================
    // プロパティ
    // ========================================

    /// <summary>
    /// 現在のプログラムカウンタ
    /// </summary>
    public int ProgramCounter => _pc;

    /// <summary>
    /// 現在のスタックサイズ
    /// </summary>
    public int StackSize => _stack.Count;

    /// <summary>
    /// 現在のコールスタックの深さ
    /// </summary>
    public int CallDepth => _callStack.Count;

    /// <summary>
    /// 実行中かどうか
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 一時停止中かどうか
    /// </summary>
    public bool IsPaused => _isPaused;

    // ========================================
    // 内部テスト用プロパティ
    // ========================================

#if ARIA_TEST
    /// <summary>
    /// ローカル変数配列（テスト用）
    /// </summary>
    internal object[] TestLocals => _locals;

    /// <summary>
    /// スタック（テスト用）
    /// </summary>
    internal Stack<object> TestStack => _stack;

    /// <summary>
    /// CallFrameを内部構造体として公開（テスト用）
    /// </summary>
    internal struct TestCallFrame
    {
        public int ReturnAddress;
        public object[] Locals;
        public int FunctionIndex;
    }
#endif

    // ========================================
    // 初期化とロード
    // ========================================

    /// <summary>
    /// バイトコードファイルをロード
    /// </summary>
    public void Load(BytecodeFile bytecodeFile)
    {
        _bytecodeFile = bytecodeFile;
        _code = bytecodeFile.Code;
        _pc = 0;

        // 関数インデックスマップを構築
        _functionIndexMap.Clear();
        for (int i = 0; i < bytecodeFile.Functions.Count; i++)
        {
            _functionIndexMap[i] = bytecodeFile.Functions[i];
        }

        // デバッグ情報を構築
        _codeOffsetToLineInfo.Clear();
        foreach (var lineInfo in bytecodeFile.DebugInfo)
        {
            _codeOffsetToLineInfo[lineInfo.CodeOffset] = lineInfo;
        }

        // グローバル変数を初期化
        _globals.Clear();

        // スタックとコールスタックをクリア
        _stack.Clear();
        _callStack.Clear();

        _isRunning = false;
        _isPaused = false;
    }

    /// <summary>
    /// バイト列からバイトコードをロード
    /// </summary>
    public void Load(byte[] bytecodeBytes)
    {
        var file = BytecodeFile.FromBytes(bytecodeBytes);
        Load(file);
    }

    // ========================================
    // 実行制御
    // ========================================

    /// <summary>
    /// 実行を開始
    /// </summary>
    public void Run()
    {
        if (_code.Length == 0)
            throw new InvalidOperationException("No bytecode loaded");

        _isRunning = true;
        _isPaused = false;

        try
        {
            ExecuteLoop();
        }
        catch (Exception ex)
        {
            _isRunning = false;
            OnError?.Invoke(GetCurrentLocation(), ex);
            throw;
        }
    }

    /// <summary>
    /// 関数を呼び出して実行
    /// </summary>
    public void CallFunction(string name, object[] args)
    {
        // 関数を検索
        FunctionEntry? funcEntry = null;
        int funcIndex = -1;

        for (int i = 0; i < _bytecodeFile.Functions.Count; i++)
        {
            string funcName = _bytecodeFile.Strings[(int)_bytecodeFile.Functions[i].NameOffset];
            if (funcName == name)
            {
                funcEntry = _bytecodeFile.Functions[i];
                funcIndex = i;
                break;
            }
        }

        if (funcEntry == null)
            throw new InvalidOperationException($"Function not found: {name}");

        // エントリーポイントが0の場合は初期ローカル変数を確保
        if (funcEntry.Value.EntryPoint == 0 && _locals.Length == 0)
        {
            _locals = new object[funcEntry.Value.LocalCount];
        }

        // 引数をスタックにプッシュ
        foreach (var arg in args)
        {
            _stack.Push(arg);
        }

        // 呼び出し
        CallFunction(funcIndex);
        Run();
    }

    /// <summary>
    /// プライベートな関数呼び出し
    /// </summary>
    private void CallFunction(int functionIndex)
    {
        if (functionIndex >= _bytecodeFile.Functions.Count)
            throw new IndexOutOfRangeException($"Function index out of range: {functionIndex}");

        FunctionEntry funcEntry = _bytecodeFile.Functions[functionIndex];

        // コールスタックの深さチェック
        if (_callStack.Count >= MaxCallDepth)
            throw new StackOverflowException($"Maximum call depth exceeded: {MaxCallDepth}");

        // 現在のフレームを保存
        var frame = new CallFrame
        {
            ReturnAddress = _pc,
            Locals = _locals,
            FunctionIndex = functionIndex
        };
        _callStack.Push(frame);

        // 新しいローカル変数領域を確保
        _locals = new object[funcEntry.LocalCount];

        // 関数のエントリポイントにジャンプ
        _pc = (int)funcEntry.EntryPoint;
    }

    /// <summary>
    /// 実行を一時停止
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>
    /// 実行を再開
    /// </summary>
    public void Resume()
    {
        if (_isRunning && _isPaused)
        {
            _isPaused = false;
            ExecuteLoop();
        }
    }

    /// <summary>
    /// 実行を停止
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _isPaused = false;
    }

    /// <summary>
    /// リセット
    /// </summary>
    public void Reset()
    {
        Stop();
        _pc = 0;
        _stack.Clear();
        _callStack.Clear();
        _locals = Array.Empty<object>();
    }

    // ========================================
    // メイン実行ループ
    // ========================================

    private void ExecuteLoop()
    {
        while (_isRunning && !_isPaused && _pc < _code.Length)
        {
            ExecuteInstruction();
        }

        // コードの終わりに達したら停止
        if (_pc >= _code.Length)
        {
            _isRunning = false;
        }
    }

    private void ExecuteInstruction()
    {
        if (_pc >= _code.Length)
            throw new InvalidOperationException("Program counter out of bounds");

        BytecodeOpCode op = (BytecodeOpCode)_code[_pc];
        _pc++;

        try
        {
            switch (op)
            {
                // スタック操作
                case BytecodeOpCode.PushInt:
                    ExecutePushInt();
                    break;
                case BytecodeOpCode.PushFloat:
                    ExecutePushFloat();
                    break;
                case BytecodeOpCode.PushString:
                    ExecutePushString();
                    break;
                case BytecodeOpCode.Pop:
                    ExecutePop();
                    break;
                case BytecodeOpCode.Dup:
                    ExecuteDup();
                    break;
                case BytecodeOpCode.Swap:
                    ExecuteSwap();
                    break;

                // メモリ操作
                case BytecodeOpCode.LoadLocal:
                    ExecuteLoadLocal();
                    break;
                case BytecodeOpCode.StoreLocal:
                    ExecuteStoreLocal();
                    break;
                case BytecodeOpCode.LoadGlobal:
                    ExecuteLoadGlobal();
                    break;
                case BytecodeOpCode.StoreGlobal:
                    ExecuteStoreGlobal();
                    break;
                case BytecodeOpCode.LoadRegister:
                    ExecuteLoadRegister();
                    break;
                case BytecodeOpCode.StoreRegister:
                    ExecuteStoreRegister();
                    break;

                // 算術演算
                case BytecodeOpCode.Add:
                    ExecuteAdd();
                    break;
                case BytecodeOpCode.Sub:
                    ExecuteSub();
                    break;
                case BytecodeOpCode.Mul:
                    ExecuteMul();
                    break;
                case BytecodeOpCode.Div:
                    ExecuteDiv();
                    break;
                case BytecodeOpCode.Mod:
                    ExecuteMod();
                    break;
                case BytecodeOpCode.Neg:
                    ExecuteNeg();
                    break;
                case BytecodeOpCode.Inc:
                    ExecuteInc();
                    break;
                case BytecodeOpCode.Dec:
                    ExecuteDec();
                    break;

                // 比較演算
                case BytecodeOpCode.CmpEq:
                    ExecuteCmpEq();
                    break;
                case BytecodeOpCode.CmpNe:
                    ExecuteCmpNe();
                    break;
                case BytecodeOpCode.CmpLt:
                    ExecuteCmpLt();
                    break;
                case BytecodeOpCode.CmpLe:
                    ExecuteCmpLe();
                    break;
                case BytecodeOpCode.CmpGt:
                    ExecuteCmpGt();
                    break;
                case BytecodeOpCode.CmpGe:
                    ExecuteCmpGe();
                    break;

                // 論理演算
                case BytecodeOpCode.LogicalAnd:
                    ExecuteLogicalAnd();
                    break;
                case BytecodeOpCode.LogicalOr:
                    ExecuteLogicalOr();
                    break;
                case BytecodeOpCode.LogicalNot:
                    ExecuteLogicalNot();
                    break;

                // 制御フロー
                case BytecodeOpCode.Jump:
                    ExecuteJump();
                    break;
                case BytecodeOpCode.JumpIfTrue:
                    ExecuteJumpIfTrue();
                    break;
                case BytecodeOpCode.JumpIfFalse:
                    ExecuteJumpIfFalse();
                    break;
                case BytecodeOpCode.Call:
                    ExecuteCall();
                    break;
                case BytecodeOpCode.Return:
                    ExecuteReturn();
                    break;
                case BytecodeOpCode.ReturnValue:
                    ExecuteReturnValue();
                    break;

                // デバッグ
                case BytecodeOpCode.Nop:
                    // 何もしない
                    break;
                case BytecodeOpCode.DebugBreak:
                    OnBreakpoint?.Invoke();
                    break;
                case BytecodeOpCode.DebugLog:
                    ExecuteDebugLog();
                    break;

                default:
                    throw new NotImplementedException($"Opcode not implemented: {op}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error executing opcode {op} at PC={_pc - 1}", ex);
        }

        // スタックオーバーフローチェック
        if (_stack.Count > MaxStackSize)
        {
            throw new StackOverflowException($"Stack overflow: {_stack.Count} > {MaxStackSize}");
        }
    }

    // ========================================
    // 命令実装（スタック操作）
    // ========================================

    private void ExecutePushInt()
    {
        int value = ReadInt32();
        _stack.Push(value);
    }

    private void ExecutePushFloat()
    {
        float value = ReadSingle();
        _stack.Push(value);
    }

    private void ExecutePushString()
    {
        int stringIndex = ReadInt32();
        string value = _bytecodeFile.Strings[stringIndex];
        _stack.Push(value);
    }

    private void ExecutePop()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");
        _stack.Pop();
    }

    private void ExecuteDup()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");
        object value = _stack.Peek();
        _stack.Push(value);
    }

    private void ExecuteSwap()
    {
        if (_stack.Count < 2)
            throw new InvalidOperationException("Stack underflow");

        object top = _stack.Pop();
        object second = _stack.Pop();
        _stack.Push(top);
        _stack.Push(second);
    }

    // ========================================
    // 命令実装（メモリ操作）
    // ========================================

    private void ExecuteLoadLocal()
    {
        ushort index = ReadUInt16();
        if (index >= _locals.Length)
            throw new IndexOutOfRangeException($"Local variable index out of range: {index}");

        _stack.Push(_locals[index]);
    }

    private void ExecuteStoreLocal()
    {
        ushort index = ReadUInt16();
        if (index >= _locals.Length)
            throw new IndexOutOfRangeException($"Local variable index out of range: {index}");

        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        _locals[index] = _stack.Pop();
    }

    private void ExecuteLoadGlobal()
    {
        int nameOffset = ReadInt32();
        string name = _bytecodeFile.Strings[nameOffset];

        if (!_globals.TryGetValue(name, out object? value))
            throw new InvalidOperationException($"Global variable not found: {name}");

        _stack.Push(value);
    }

    private void ExecuteStoreGlobal()
    {
        int nameOffset = ReadInt32();
        string name = _bytecodeFile.Strings[nameOffset];

        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        _globals[name] = _stack.Pop();
    }

    private void ExecuteLoadRegister()
    {
        byte reg = ReadByte();
        _stack.Push(_registers[reg]);
    }

    private void ExecuteStoreRegister()
    {
        byte reg = ReadByte();
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        _registers[reg] = Convert.ToInt32(_stack.Pop());
    }

    // ========================================
    // 命令実装（算術演算）
    // ========================================

    private void ExecuteAdd()
    {
        var (right, left) = PopTwo();
        if (left is int lInt && right is int rInt)
        {
            _stack.Push(lInt + rInt);
        }
        else if (left is float lFloat && right is float rFloat)
        {
            _stack.Push(lFloat + rFloat);
        }
        else
        {
            float l = Convert.ToSingle(left);
            float r = Convert.ToSingle(right);
            _stack.Push(l + r);
        }
    }

    private void ExecuteSub()
    {
        var (right, left) = PopTwo();
        if (left is int lInt && right is int rInt)
        {
            _stack.Push(lInt - rInt);
        }
        else
        {
            float l = Convert.ToSingle(left);
            float r = Convert.ToSingle(right);
            _stack.Push(l - r);
        }
    }

    private void ExecuteMul()
    {
        var (right, left) = PopTwo();
        if (left is int lInt && right is int rInt)
        {
            _stack.Push(lInt * rInt);
        }
        else
        {
            float l = Convert.ToSingle(left);
            float r = Convert.ToSingle(right);
            _stack.Push(l * r);
        }
    }

    private void ExecuteDiv()
    {
        var (right, left) = PopTwo();
        if (left is int lInt && right is int rInt)
        {
            _stack.Push(rInt != 0 ? lInt / rInt : 0);
        }
        else
        {
            float l = Convert.ToSingle(left);
            float r = Convert.ToSingle(right);
            _stack.Push(r != 0 ? l / r : 0f);
        }
    }

    private void ExecuteMod()
    {
        var (right, left) = PopTwo();
        if (left is int lInt && right is int rInt)
        {
            _stack.Push(rInt != 0 ? lInt % rInt : 0);
        }
        else
        {
            throw new InvalidOperationException("Modulo operation only supported for integers");
        }
    }

    private void ExecuteNeg()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        object value = _stack.Pop();
        if (value is int i)
        {
            _stack.Push(-i);
        }
        else if (value is float f)
        {
            _stack.Push(-f);
        }
        else
        {
            throw new InvalidOperationException("Negation only supported for numeric types");
        }
    }

    private void ExecuteInc()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        object value = _stack.Pop();
        if (value is int i)
        {
            _stack.Push(i + 1);
        }
        else
        {
            throw new InvalidOperationException("Increment only supported for integers");
        }
    }

    private void ExecuteDec()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        object value = _stack.Pop();
        if (value is int i)
        {
            _stack.Push(i - 1);
        }
        else
        {
            throw new InvalidOperationException("Decrement only supported for integers");
        }
    }

    // ========================================
    // 命令実装（比較演算）
    // ========================================

    private void ExecuteCmpEq()
    {
        var (right, left) = PopTwo();
        _stack.Push(Equals(left, right));
    }

    private void ExecuteCmpNe()
    {
        var (right, left) = PopTwo();
        _stack.Push(!Equals(left, right));
    }

    private void ExecuteCmpLt()
    {
        var (right, left) = PopTwo();
        _stack.Push(CompareTo(left, right) < 0);
    }

    private void ExecuteCmpLe()
    {
        var (right, left) = PopTwo();
        _stack.Push(CompareTo(left, right) <= 0);
    }

    private void ExecuteCmpGt()
    {
        var (right, left) = PopTwo();
        _stack.Push(CompareTo(left, right) > 0);
    }

    private void ExecuteCmpGe()
    {
        var (right, left) = PopTwo();
        _stack.Push(CompareTo(left, right) >= 0);
    }

    // ========================================
    // 命令実装（論理演算）
    // ========================================

    private void ExecuteLogicalAnd()
    {
        var (right, left) = PopTwo();
        bool leftBool = Convert.ToBoolean(left);
        bool rightBool = Convert.ToBoolean(right);
        _stack.Push(leftBool && rightBool);
    }

    private void ExecuteLogicalOr()
    {
        var (right, left) = PopTwo();
        bool leftBool = Convert.ToBoolean(left);
        bool rightBool = Convert.ToBoolean(right);
        _stack.Push(leftBool || rightBool);
    }

    private void ExecuteLogicalNot()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        object value = _stack.Pop();
        _stack.Push(!Convert.ToBoolean(value));
    }

    // ========================================
    // 命令実装（制御フロー）
    // ========================================

    private void ExecuteJump()
    {
        int offset = ReadInt32();
        _pc += offset;
    }

    private void ExecuteJumpIfTrue()
    {
        int offset = ReadInt32();

        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        bool condition = Convert.ToBoolean(_stack.Pop());
        if (condition)
        {
            _pc += offset;
        }
    }

    private void ExecuteJumpIfFalse()
    {
        int offset = ReadInt32();

        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        bool condition = Convert.ToBoolean(_stack.Pop());
        if (!condition)
        {
            _pc += offset;
        }
    }

    private void ExecuteCall()
    {
        ushort functionIndex = (ushort)ReadInt32();
        CallFunction(functionIndex);
    }

    private void ExecuteReturn()
    {
        ReturnFromFunction(null);
    }

    private void ExecuteReturnValue()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("Stack underflow");

        object returnValue = _stack.Pop();
        ReturnFromFunction(returnValue);
    }

    // ========================================
    // 命令実装（デバッグ）
    // ========================================

    private void ExecuteDebugLog()
    {
        int stringIndex = ReadInt32();
        string message = _bytecodeFile.Strings[stringIndex];
        OnDebugLog?.Invoke(message);
    }

    // ========================================
    // ヘルパーメソード
    // ========================================

    private void ReturnFromFunction(object? returnValue)
    {
        if (_callStack.Count == 0)
        {
            // トップレベル関数からのリターン（実行終了）
            _isRunning = false;
            return;
        }

        CallFrame frame = _callStack.Pop();

        // 戻り値をプッシュ
        if (returnValue != null)
        {
            _stack.Push(returnValue);
        }

        // フレームを復元
        _pc = frame.ReturnAddress;
        _locals = frame.Locals;
    }

    private (object right, object left) PopTwo()
    {
        if (_stack.Count < 2)
            throw new InvalidOperationException("Stack underflow");

        object right = _stack.Pop();
        object left = _stack.Pop();
        return (right, left);
    }

    private int CompareTo(object left, object right)
    {
        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        // 数値として比較
        if (left is int lInt && right is int rInt)
        {
            return lInt.CompareTo(rInt);
        }
        else if (left is float lFloat && right is float rFloat)
        {
            return lFloat.CompareTo(rFloat);
        }
        else
        {
            float l = Convert.ToSingle(left);
            float r = Convert.ToSingle(right);
            return l.CompareTo(r);
        }
    }

    private string GetCurrentLocation()
    {
        if (_codeOffsetToLineInfo.TryGetValue((uint)_pc, out LineInfo lineInfo))
        {
            string file = _bytecodeFile.Strings.FirstOrDefault() ?? "unknown";
            return $"{file}:{lineInfo.SourceLine}:{lineInfo.SourceColumn}";
        }
        return $"PC={_pc}";
    }

    // ========================================
    // バイト列読み込みヘルパー
    // ========================================

    private byte ReadByte()
    {
        if (_pc >= _code.Length)
            throw new InvalidOperationException("Unexpected end of bytecode");

        return _code[_pc++];
    }

    private ushort ReadUInt16()
    {
        if (_pc + 2 > _code.Length)
            throw new InvalidOperationException("Unexpected end of bytecode");

        ushort value = BitConverter.ToUInt16(_code, _pc);
        if (!BitConverter.IsLittleEndian)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToUInt16(bytes);
        }

        _pc += 2;
        return value;
    }

    private int ReadInt32()
    {
        if (_pc + 4 > _code.Length)
            throw new InvalidOperationException("Unexpected end of bytecode");

        int value = BitConverter.ToInt32(_code, _pc);
        if (!BitConverter.IsLittleEndian)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToInt32(bytes);
        }

        _pc += 4;
        return value;
    }

    private float ReadSingle()
    {
        if (_pc + 4 > _code.Length)
            throw new InvalidOperationException("Unexpected end of bytecode");

        float value = BitConverter.ToSingle(_code, _pc);
        if (!BitConverter.IsLittleEndian)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            value = BitConverter.ToSingle(bytes);
        }

        _pc += 4;
        return value;
    }
}

/// <summary>
/// コールフレーム（関数呼び出しのコンテキスト）
/// </summary>
internal struct CallFrame
{
    public int ReturnAddress;
    public object[] Locals;
    public int FunctionIndex;

    public CallFrame()
    {
        ReturnAddress = 0;
        Locals = Array.Empty<object>();
        FunctionIndex = 0;
    }
}
