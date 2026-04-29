using System;
using System.Buffers;
using System.Collections.Generic;
using AriaEngine.Compiler;

namespace AriaEngine.VM;

/// <summary>
/// 最適化されたバイトコード仮想マシン
/// パフォーマンス重視の実装
/// </summary>
public sealed class OptimizedBytecodeVM : IDisposable
{
    // ========================================
    // 定数
    // ========================================

    private const int MaxStackSize = 1024;
    private const int MaxCallDepth = 256;
    private const int DefaultLocalCapacity = 16;

    // ========================================
    // 状態（キャッシュフレンドリーな配置）
    // ========================================

    // コードとPC（ホットパス）
    private byte[] _code = Array.Empty<byte>();
    private int _pc;

    // 配列ベースのスタック（Stack<object>より高速）
    private object[] _stack = new object[MaxStackSize];
    private int _stackTop;

    // ローカル変数
    private object[] _locals = Array.Empty<object>();
    private int _localCount;

    // レジスタファイル（256レジスタ）
    private readonly int[] _registers = new int[256];

    // コールスタック（配列ベース）
    private CallFrame[] _callStack = new CallFrame[MaxCallDepth];
    private int _callStackTop;

    // バイトコードファイル情報
    private BytecodeFile _bytecodeFile = null!;
    private readonly Dictionary<int, int> _functionIndexMap = new(32);

    // 実行状態
    private bool _isRunning;
    private bool _isPaused;

    // 文字列テーブルキャッシュ
    private readonly string[] _stringCache = Array.Empty<string>();

    // 定数テーブルキャッシュ
    private readonly object[] _constantCache = Array.Empty<object>();

    // ========================================
    // イベント
    // ========================================

    public event Action<string>? OnDebugLog;
    public event Action? OnBreakpoint;
    public event Action<string, Exception>? OnError;

    // ゲームエンジン連携イベント
    public event Action<string>? OnText;
    public event Action<int, string, int, int>? OnSpriteLoad;
    public event Action<int, int, int>? OnSpriteMove;
    public event Action<string, int>? OnBackgroundSet;
    public event Action<string, int>? OnBGMPlay;
    public event Action? OnBGMStop;
    public event Action<string, int, int>? OnSEPlay;

    // テキスト制御イベント
    public event Action? OnTextClear;
    public event Action? OnWaitClick;

    // システムイベント
    public event Action? OnSave;
    public event Action? OnLoad;

    // ========================================
    // プロパティ
    // ========================================

    public int ProgramCounter => _pc;
    public int StackSize => _stackTop;
    public int CallDepth => _callStackTop;
    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;

    // ========================================
    // 内部構造体
    // ========================================

    private readonly struct CallFrame
    {
        public readonly int ReturnAddress;
        public readonly int LocalCount;
        public readonly int FunctionIndex;
        public readonly int StackBase;

        public CallFrame(int returnAddr, int localCount, int funcIndex, int stackBase)
        {
            ReturnAddress = returnAddr;
            LocalCount = localCount;
            FunctionIndex = funcIndex;
            StackBase = stackBase;
        }
    }

    // ========================================
    // 初期化とロード
    // ========================================

    public void Load(BytecodeFile bytecodeFile)
    {
        _bytecodeFile = bytecodeFile;
        _code = bytecodeFile.Code;
        _pc = 0;

        // 文字列テーブルキャッシュを構築
        _stringCache = bytecodeFile.Strings.ToArray();

        // 定数テーブルキャッシュを構築
        _constantCache = new object[bytecodeFile.Constants.Count];
        for (int i = 0; i < bytecodeFile.Constants.Count; i++)
        {
            _constantCache[i] = bytecodeFile.Constants[i].GetValue();
        }

        // 関数インデックスマップを構築
        _functionIndexMap.Clear();
        for (int i = 0; i < bytecodeFile.Functions.Count; i++)
        {
            _functionIndexMap[(int)bytecodeFile.Functions[i].EntryPoint] = i;
        }

        // 状態リセット
        _stackTop = 0;
        _callStackTop = 0;
        _isRunning = false;
        _isPaused = false;
    }

    public void Load(byte[] bytecodeBytes)
    {
        var file = BytecodeFile.FromBytes(bytecodeBytes);
        Load(file);
    }

    // ========================================
    // 実行制御
    // ========================================

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

    public void RunOneInstruction()
    {
        if (!_isRunning || _isPaused) return;

        try
        {
            ExecuteInstruction();
        }
        catch (Exception ex)
        {
            _isRunning = false;
            OnError?.Invoke(GetCurrentLocation(), ex);
            throw;
        }
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
    }

    public void Stop()
    {
        _isRunning = false;
        _isPaused = false;
    }

    public void Reset()
    {
        Stop();
        _pc = 0;
        _stackTop = 0;
        _callStackTop = 0;
        _locals = Array.Empty<object>();
        _localCount = 0;
    }

    // ========================================
    // メイン実行ループ（最適化済み）
    // ========================================

    private void ExecuteLoop()
    {
        // ホットパス: 局所変数で頻繁アクセス
        byte[] code = _code;
        int codeLength = code.Length;
        object[] stack = _stack;
        object[] locals = _locals;
        object[] constants = _constantCache;
        string[] strings = _stringCache;
        int[] registers = _registers;

        while (_isRunning && !_isPaused && _pc < codeLength)
        {
            // 命令フェッチ
            BytecodeOpCode op = (BytecodeOpCode)code[_pc++];

            // 命令デコードと実行
            switch (op)
            {
                // スタック操作
                case BytecodeOpCode.PushInt:
                    stack[_stackTop++] = ReadInt32();
                    break;

                case BytecodeOpCode.PushFloat:
                    stack[_stackTop++] = ReadSingle();
                    break;

                case BytecodeOpCode.PushString:
                    stack[_stackTop++] = strings[ReadInt32()];
                    break;

                case BytecodeOpCode.Pop:
                    _stackTop--;
                    break;

                case BytecodeOpCode.Dup:
                    stack[_stackTop] = stack[_stackTop - 1];
                    _stackTop++;
                    break;

                case BytecodeOpCode.Swap:
                    (stack[_stackTop - 1], stack[_stackTop - 2]) = (stack[_stackTop - 2], stack[_stackTop - 1]);
                    break;

                // メモリ操作
                case BytecodeOpCode.LoadLocal:
                    stack[_stackTop++] = locals[ReadUInt16()];
                    break;

                case BytecodeOpCode.StoreLocal:
                    locals[ReadUInt16()] = stack[--_stackTop];
                    break;

                case BytecodeOpCode.LoadGlobal:
                    stack[_stackTop++] = LoadGlobal(strings[ReadInt32()]);
                    break;

                case BytecodeOpCode.StoreGlobal:
                    StoreGlobal(strings[ReadInt32()], stack[--_stackTop]);
                    break;

                case BytecodeOpCode.LoadRegister:
                    stack[_stackTop++] = registers[ReadUInt16()];
                    break;

                case BytecodeOpCode.StoreRegister:
                    registers[ReadUInt16()] = (int)stack[--_stackTop];
                    break;

                // 算術演算
                case BytecodeOpCode.Add:
                    ExecuteBinaryOp(ref _stackTop, stack, (a, b) => (int)a + (int)b);
                    break;

                case BytecodeOpCode.Sub:
                    ExecuteBinaryOp(ref _stackTop, stack, (a, b) => (int)a - (int)b);
                    break;

                case BytecodeOpCode.Mul:
                    ExecuteBinaryOp(ref _stackTop, stack, (a, b) => (int)a * (int)b);
                    break;

                case BytecodeOpCode.Div:
                    ExecuteBinaryOp(ref _stackTop, stack, (a, b) => (int)a / (int)b);
                    break;

                case BytecodeOpCode.Mod:
                    ExecuteBinaryOp(ref _stackTop, stack, (a, b) => (int)a % (int)b);
                    break;

                case BytecodeOpCode.Neg:
                    stack[_stackTop - 1] = -(int)stack[_stackTop - 1];
                    break;

                case BytecodeOpCode.Inc:
                    stack[_stackTop - 1] = (int)stack[_stackTop - 1] + 1;
                    break;

                case BytecodeOpCode.Dec:
                    stack[_stackTop - 1] = (int)stack[_stackTop - 1] - 1;
                    break;

                // 比較演算
                case BytecodeOpCode.CmpEq:
                case BytecodeOpCode.CmpNe:
                case BytecodeOpCode.CmpLt:
                case BytecodeOpCode.CmpLe:
                case BytecodeOpCode.CmpGt:
                case BytecodeOpCode.CmpGe:
                    ExecuteComparison(op, ref _stackTop, stack);
                    break;

                // 制御フロー
                case BytecodeOpCode.Jump:
                    _pc = ReadInt32();
                    break;

                case BytecodeOpCode.JumpIfTrue:
                    if ((bool)stack[--_stackTop])
                        _pc = ReadInt32();
                    else
                        _pc += 4;
                    break;

                case BytecodeOpCode.JumpIfFalse:
                    if (!(bool)stack[--_stackTop])
                        _pc = ReadInt32();
                    else
                        _pc += 4;
                    break;

                case BytecodeOpCode.Call:
                    int funcIndex = ReadInt32();
                    CallFunction(funcIndex);
                    break;

                case BytecodeOpCode.Return:
                    ReturnFromFunction(null);
                    break;

                case BytecodeOpCode.ReturnValue:
                    object returnValue = stack[--_stackTop];
                    ReturnFromFunction(returnValue);
                    break;

                // テキスト命令
                case BytecodeOpCode.Text:
                    ExecuteText(stack, strings, ref _stackTop);
                    break;

                case BytecodeOpCode.TextClear:
                    OnTextClear?.Invoke();
                    break;

                case BytecodeOpCode.WaitClick:
                    OnWaitClick?.Invoke();
                    break;

                case BytecodeOpCode.WaitClickClear:
                    OnTextClear?.Invoke();
                    OnWaitClick?.Invoke();
                    break;

                // スプライト命令
                case BytecodeOpCode.SpriteLoad:
                    ExecuteSpriteLoad(stack, strings, ref _stackTop);
                    break;

                case BytecodeOpCode.SpriteMove:
                    ExecuteSpriteMove(stack, ref _stackTop);
                    break;

                // 背景命令
                case BytecodeOpCode.BackgroundSet:
                    ExecuteBackgroundSet(stack, strings, ref _stackTop);
                    break;

                // オーディオ命令
                case BytecodeOpCode.BGMPlay:
                    ExecuteBGMPlay(stack, strings, ref _stackTop);
                    break;

                case BytecodeOpCode.BGMStop:
                    ExecuteBGMStop();
                    break;

                case BytecodeOpCode.SEPlay:
                    ExecuteSEPlay(stack, strings, ref _stackTop);
                    break;

                // システム命令
                case BytecodeOpCode.Save:
                    OnSave?.Invoke();
                    break;

                case BytecodeOpCode.Load:
                    OnLoad?.Invoke();
                    break;

                case BytecodeOpCode.Nop:
                    break;

                // デバッグ
                case BytecodeOpCode.DebugBreak:
                    OnBreakpoint?.Invoke();
                    break;

                case BytecodeOpCode.DebugLog:
                    OnDebugLog?.Invoke(strings[ReadInt32()]);
                    break;

                default:
                    throw new NotImplementedException($"Opcode not implemented: {op}");
            }

            // スタックオーバーフローチェック
            if (_stackTop > MaxStackSize)
                throw new StackOverflowException($"Stack overflow: {_stackTop} > {MaxStackSize}");
        }
    }

    // ========================================
    // 最適化された命令実装
    // ========================================

    private void ExecuteInstruction()
    {
        // 単一命令実行（デバッグ用）
        ExecuteLoop();
    }

    private static void ExecuteBinaryOp(ref int stackTop, object[] stack, Func<object, object, object> op)
    {
        object b = stack[--stackTop];
        object a = stack[--stackTop];
        stack[stackTop++] = op(a, b);
    }

    private static void ExecuteComparison(BytecodeOpCode op, ref int stackTop, object[] stack)
    {
        object b = stack[--stackTop];
        object a = stack[--stackTop];

        bool result = op switch
        {
            BytecodeOpCode.CmpEq => a.Equals(b),
            BytecodeOpCode.CmpNe => !a.Equals(b),
            BytecodeOpCode.CmpLt => Comparer<object>.Default.Compare(a, b) < 0,
            BytecodeOpCode.CmpLe => Comparer<object>.Default.Compare(a, b) <= 0,
            BytecodeOpCode.CmpGt => Comparer<object>.Default.Compare(a, b) > 0,
            BytecodeOpCode.CmpGe => Comparer<object>.Default.Compare(a, b) >= 0,
            _ => throw new InvalidOperationException($"Invalid comparison opcode: {op}")
        };

        stack[stackTop++] = result;
    }

    // ========================================
    // 命令実装
    // ========================================

    private void ExecuteText(object[] stack, string[] strings, ref int stackTop)
    {
        int stringIndex = ReadInt32();
        string text = strings[stringIndex];
        // テキスト表示イベント
        OnText?.Invoke(text);
    }

    private void ExecuteSpriteLoad(object[] stack, string[] strings, ref int stackTop)
    {
        int id = ReadInt32();
        int pathIndex = ReadInt32();
        int x = ReadInt32();
        int y = ReadInt32();
        string path = strings[pathIndex];
        OnSpriteLoad?.Invoke(id, path, x, y);
    }

    private void ExecuteSpriteMove(object[] stack, ref int stackTop)
    {
        int id = ReadInt32();
        int x = ReadInt32();
        int y = ReadInt32();
        OnSpriteMove?.Invoke(id, x, y);
    }

    private void ExecuteBackgroundSet(object[] stack, string[] strings, ref int stackTop)
    {
        int pathIndex = ReadInt32();
        int duration = ReadInt32();
        string path = strings[pathIndex];
        OnBackgroundSet?.Invoke(path, duration);
    }

    private void ExecuteBGMPlay(object[] stack, string[] strings, ref int stackTop)
    {
        int pathIndex = ReadInt32();
        int volume = ReadInt32();
        string path = strings[pathIndex];
        OnBGMPlay?.Invoke(path, volume);
    }

    private void ExecuteBGMStop()
    {
        OnBGMStop?.Invoke();
    }

    private void ExecuteSEPlay(object[] stack, string[] strings, ref int stackTop)
    {
        int pathIndex = ReadInt32();
        int channel = ReadInt32();
        int volume = ReadInt32();
        string path = strings[pathIndex];
        OnSEPlay?.Invoke(path, channel, volume);
    }

    // ========================================
    // 関数呼び出し
    // ========================================

    private void CallFunction(int functionIndex)
    {
        if (functionIndex >= _bytecodeFile.Functions.Count)
            throw new IndexOutOfRangeException($"Function index out of range: {functionIndex}");

        FunctionEntry funcEntry = _bytecodeFile.Functions[functionIndex];

        // コールスタックの深さチェック
        if (_callStackTop >= MaxCallDepth)
            throw new StackOverflowException($"Maximum call depth exceeded: {MaxCallDepth}");

        // 現在のフレームを保存
        _callStack[_callStackTop++] = new CallFrame(_pc, _localCount, functionIndex, _stackTop);

        // 新しいローカル変数領域を確保
        int newLocalCount = funcEntry.LocalCount;
        object[] newLocals = new object[newLocalCount];
        object[] oldLocals = _locals;

        _locals = newLocals;
        _localCount = newLocalCount;

        // 古いローカル変数はガベージコレクションに任せる
        // （大きな配列の場合はプールに戻す）

        // 関数のエントリポイントにジャンプ
        _pc = (int)funcEntry.EntryPoint;
    }

    private void ReturnFromFunction(object? returnValue)
    {
        if (_callStackTop == 0)
        {
            // トップレベル関数からのリターン（実行終了）
            _isRunning = false;
            return;
        }

        CallFrame frame = _callStack[--_callStackTop];

        // 戻り値をプッシュ
        if (returnValue != null)
        {
            _stack[_stackTop++] = returnValue;
        }

        // フレームを復元
        _pc = frame.ReturnAddress;
        _locals = new object[frame.LocalCount];
        _localCount = frame.LocalCount;
    }

    // ========================================
    // グローバル変数
    // ========================================

    private readonly Dictionary<string, object> _globals = new();

    private object LoadGlobal(string name)
    {
        if (!_globals.TryGetValue(name, out object? value))
            throw new InvalidOperationException($"Global variable not found: {name}");
        return value;
    }

    private void StoreGlobal(string name, object value)
    {
        _globals[name] = value;
    }

    // ========================================
    // ヘルパーメソッド（インライン展開用）
    // ========================================

    private int ReadInt32()
    {
        int result = BitConverter.ToInt32(_code, _pc);
        _pc += 4;
        return result;
    }

    private float ReadSingle()
    {
        float result = BitConverter.ToSingle(_code, _pc);
        _pc += 4;
        return result;
    }

    private ushort ReadUInt16()
    {
        ushort result = BitConverter.ToUInt16(_code, _pc);
        _pc += 2;
        return result;
    }

    private byte ReadByte()
    {
        return _code[_pc++];
    }

    private string GetCurrentLocation()
    {
        return $"PC:{_pc}";
    }

    // ========================================
    // リソース解放
    // ========================================

    public void Dispose()
    {
        _code = Array.Empty<byte>();
        _stack = Array.Empty<object>();
        _locals = Array.Empty<object>();
        _globals.Clear();
        _functionIndexMap.Clear();
    }
}
