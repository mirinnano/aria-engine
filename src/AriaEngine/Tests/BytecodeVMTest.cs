using System;
using AriaEngine.Compiler;
using AriaEngine.VM;

namespace AriaEngine.Tests;

/// <summary>
/// バイトコードVMのテスト
/// </summary>
public class BytecodeVMTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== BytecodeVM Tests ===\n");

        TestSimpleAdd();
        TestFunctionCall();
        TestConditionals();
        TestLoop();

        Console.WriteLine("\n=== All Tests Passed ===");
    }

    /// <summary>
    /// 簡単な加算テスト
    /// </summary>
    private static void TestSimpleAdd()
    {
        Console.WriteLine("Test: Simple Addition");

        var file = new BytecodeFile();
        var generator = new BytecodeGenerator();

        // 関数: main() -> void
        // 10 + 20 を計算して結果をローカル変数に格納

        // 関数テーブル
        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 0,
            EntryPoint = 0,
            LocalCount = 1,
            ParamCount = 0,
            ReturnType = (byte)ReturnType.Void
        });

        // 文字列テーブル
        file.Strings.Add("main");
        file.Strings.Add("add");

        // 定数テーブル
        file.Constants.Add(ConstantValue.FromInt(10));
        file.Constants.Add(ConstantValue.FromInt(20));

        // バイトコード生成
        // PushInt 10
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(10);

        // PushInt 20
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(20);

        // Add
        generator.EmitOp(BytecodeOpCode.Add);

        // StoreLocal 0
        generator.EmitOp(BytecodeOpCode.StoreLocal);
        generator.EmitLocalIndex(0);

        // Return
        generator.EmitOp(BytecodeOpCode.Return);

        // バイトコードをファイルに設定
        file.Code = generator.ToByteArray();

        // VMの作成と実行
        var vm = new BytecodeVM();
        vm.Load(file);

        // デバッグログの設定
        vm.OnDebugLog += (msg) => Console.WriteLine($"  [LOG] {msg}");

        // main関数を呼び出し
        vm.CallFunction("main", Array.Empty<object>());

        // 結果の確認
        int result = (int)vm.TestLocals[0];
        Console.WriteLine($"  Result: {result}");
        if (result != 30)
            throw new Exception($"Expected 30, got {result}");

        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// 関数呼び出しテスト
    /// </summary>
    private static void TestFunctionCall()
    {
        Console.WriteLine("Test: Function Call");

        var file = new BytecodeFile();
        var generator = new BytecodeGenerator();

        // 関数: add(a: int, b: int) -> int
        // 関数: main() -> void

        // 関数テーブル
        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 0,
            EntryPoint = 0,
            LocalCount = 0,
            ParamCount = 2,
            ReturnType = (byte)ReturnType.Int
        });

        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 4,
            EntryPoint = 6,
            LocalCount = 0,
            ParamCount = 0,
            ReturnType = (byte)ReturnType.Void
        });

        // 文字列テーブル
        file.Strings.Add("add");
        file.Strings.Add("main");

        // バイトコード生成: add関数
        // LoadLocal 0 (a)
        generator.EmitOp(BytecodeOpCode.LoadLocal);
        generator.EmitLocalIndex(0);

        // LoadLocal 1 (b)
        generator.EmitOp(BytecodeOpCode.LoadLocal);
        generator.EmitLocalIndex(1);

        // Add
        generator.EmitOp(BytecodeOpCode.Add);

        // ReturnValue
        generator.EmitOp(BytecodeOpCode.ReturnValue);

        // バイトコード生成: main関数
        // PushInt 15
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(15);

        // PushInt 25
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(25);

        // Call 0 (add)
        generator.EmitOp(BytecodeOpCode.Call);
        generator.EmitFunctionIndex(0);

        // Pop (戻り値を破棄)
        generator.EmitOp(BytecodeOpCode.Pop);

        // Return
        generator.EmitOp(BytecodeOpCode.Return);

        // バイトコードをファイルに設定
        file.Code = generator.ToByteArray();

        // VMの作成と実行
        var vm = new BytecodeVM();
        vm.Load(file);
        vm.CallFunction("main", Array.Empty<object>());

        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// 条件分岐テスト
    /// </summary>
    private static void TestConditionals()
    {
        Console.WriteLine("Test: Conditionals");

        var file = new BytecodeFile();
        var generator = new BytecodeGenerator();

        // 関数テーブル
        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 0,
            EntryPoint = 0,
            LocalCount = 0,
            ParamCount = 0,
            ReturnType = (byte)ReturnType.Void
        });

        // 文字列テーブル
        file.Strings.Add("main");

        // バイトコード生成
        // PushInt 10
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(10);

        // PushInt 5
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(5);

        // CmpLt
        generator.EmitOp(BytecodeOpCode.CmpLt);

        // JumpIfTrue +5 (skip)
        generator.EmitOp(BytecodeOpCode.JumpIfTrue);
        generator.EmitInt(5);

        // PushInt 0 (false path)
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(0);

        // Jump +2 (skip true path)
        generator.EmitOp(BytecodeOpCode.Jump);
        generator.EmitInt(2);

        // PushInt 1 (true path)
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(1);

        // Return
        generator.EmitOp(BytecodeOpCode.Return);

        // バイトコードをファイルに設定
        file.Code = generator.ToByteArray();

        // VMの作成と実行
        var vm = new BytecodeVM();
        vm.Load(file);
        vm.CallFunction("main", Array.Empty<object>());

        // スタックの結果を確認
        int result = (int)vm.TestStack.Pop();
        Console.WriteLine($"  Result: {result}");
        if (result != 1)
            throw new Exception($"Expected 1, got {result}");

        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// ループテスト
    /// </summary>
    private static void TestLoop()
    {
        Console.WriteLine("Test: Loop");

        var file = new BytecodeFile();
        var generator = new BytecodeGenerator();

        // 関数テーブル
        file.Functions.Add(new FunctionEntry
        {
            NameOffset = 0,
            EntryPoint = 0,
            LocalCount = 1,
            ParamCount = 0,
            ReturnType = (byte)ReturnType.Void
        });

        // 文字列テーブル
        file.Strings.Add("main");

        // バイトコード生成
        // PushInt 0 (counter)
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(0);

        // StoreLocal 0
        generator.EmitOp(BytecodeOpCode.StoreLocal);
        generator.EmitLocalIndex(0);

        // ラベル: loop_start
        generator.EmitLabel("loop_start");

        // LoadLocal 0
        generator.EmitOp(BytecodeOpCode.LoadLocal);
        generator.EmitLocalIndex(0);

        // PushInt 5
        generator.EmitOp(BytecodeOpCode.PushInt);
        generator.EmitInt(5);

        // CmpLt
        generator.EmitOp(BytecodeOpCode.CmpLt);

        // JumpIfFalse +12 (exit loop)
        generator.EmitOp(BytecodeOpCode.JumpIfFalse);
        generator.EmitInt(12);

        // LoadLocal 0
        generator.EmitOp(BytecodeOpCode.LoadLocal);
        generator.EmitLocalIndex(0);

        // Inc
        generator.EmitOp(BytecodeOpCode.Inc);

        // StoreLocal 0
        generator.EmitOp(BytecodeOpCode.StoreLocal);
        generator.EmitLocalIndex(0);

        // Jump -16 (loop_start)
        generator.EmitOp(BytecodeOpCode.Jump);
        generator.EmitInt(-16);

        // ラベル: loop_end (implicit)

        // Return
        generator.EmitOp(BytecodeOpCode.Return);

        // ラベルを解決
        generator.ResolveLabels();

        // バイトコードをファイルに設定
        file.Code = generator.ToByteArray();

        // VMの作成と実行
        var vm = new BytecodeVM();
        vm.Load(file);
        vm.CallFunction("main", Array.Empty<object>());

        // 結果の確認
        int result = (int)vm.TestLocals[0];
        Console.WriteLine($"  Final counter: {result}");
        if (result != 5)
            throw new Exception($"Expected 5, got {result}");

        Console.WriteLine("  ✓ Passed\n");
    }
}
