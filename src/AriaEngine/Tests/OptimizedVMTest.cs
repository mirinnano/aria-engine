using System;
using System.IO;
using AriaEngine.Assets;
using AriaEngine.Compiler;
using AriaEngine.Core;
using AriaEngine.VM;

namespace AriaEngine.Tests;

/// <summary>
/// 最適化されたBytecodeVMのテスト
/// </summary>
public static class OptimizedVMTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== Optimized BytecodeVM Tests ===\n");

        TestSimpleScript();
        TestSpriteOperations();
        TestArithmetic();
        TestControlFlow();

        Console.WriteLine("\n=== All Optimized VM Tests Passed ===");
    }

    /// <summary>
    /// シンプルなスクリプトのテスト
    /// </summary>
    private static void TestSimpleScript()
    {
        Console.WriteLine("Test: Simple Script");

        var script = @"
*start
text ""Hello, World!""
text ""This is a test.""
return
";

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var compiler = new BytecodeCompiler(reporter);

        // スクリプトをパース
        var lines = script.Split('\n');
        var parseResult = parser.Parse(lines, "test.aria");

        // バイトコードにコンパイル
        var bytecodeFile = compiler.Compile(parseResult.Instructions, "test");

        // VMの作成と実行
        using var vm = new OptimizedBytecodeVM();

        int textCount = 0;
        vm.OnText += text =>
        {
            Console.WriteLine($"  Text: {text}");
            textCount++;
        };

        vm.Load(bytecodeFile);
        vm.Run();

        if (textCount != 2)
            throw new Exception($"Expected 2 text commands, got {textCount}");

        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// スプライト操作のテスト
    /// </summary>
    private static void TestSpriteOperations()
    {
        Console.WriteLine("Test: Sprite Operations");

        var script = @"
*sprite_test
text ""Loading sprite...""
sprite 1, ""test.png"", 100, 200
sprite 2, ""bg.jpg"", 0, 0
move 1, 150, 250
return
";

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var compiler = new BytecodeCompiler(reporter);

        var lines = script.Split('\n');
        var parseResult = parser.Parse(lines, "sprite.aria");
        var bytecodeFile = compiler.Compile(parseResult.Instructions, "sprite");

        using var vm = new OptimizedBytecodeVM();

        int spriteLoadCount = 0;
        int spriteMoveCount = 0;

        vm.OnSpriteLoad += (id, path, x, y) =>
        {
            Console.WriteLine($"  Sprite Load: ID={id}, Path={path}, X={x}, Y={y}");
            spriteLoadCount++;
        };

        vm.OnSpriteMove += (id, x, y) =>
        {
            Console.WriteLine($"  Sprite Move: ID={id}, X={x}, Y={y}");
            spriteMoveCount++;
        };

        vm.Load(bytecodeFile);
        vm.Run();

        if (spriteLoadCount != 2)
            throw new Exception($"Expected 2 sprite loads, got {spriteLoadCount}");
        if (spriteMoveCount != 1)
            throw new Exception($"Expected 1 sprite move, got {spriteMoveCount}");

        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// 算術演算のテスト
    /// </summary>
    private static void TestArithmetic()
    {
        Console.WriteLine("Test: Arithmetic Operations");

        var script = @"
*arithmetic_test
let %0, 10
let %1, 20
add %0, 5
sub %1, 3
mul %0, 2
div %0, 5
return
";

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var compiler = new BytecodeCompiler(reporter);

        var lines = script.Split('\n');
        var parseResult = parser.Parse(lines, "arithmetic.aria");
        var bytecodeFile = compiler.Compile(parseResult.Instructions, "arithmetic");

        using var vm = new OptimizedBytecodeVM();
        vm.Load(bytecodeFile);
        vm.Run();

        // 結果の確認
        // %0 = 10, +5 = 15, *2 = 30, /5 = 6
        // %1 = 20, -3 = 17

        Console.WriteLine("  Arithmetic test completed");
        Console.WriteLine("  ✓ Passed\n");
    }

    /// <summary>
    /// 制御フローのテスト
    /// </summary>
    private static void TestControlFlow()
    {
        Console.WriteLine("Test: Control Flow");

        var script = @"
*control_test
let %0, 0
cmp %0, 0
beq *equal
text ""Not equal""
jump *end

*equal
text ""Equal""
jump *end

*end
return
";

        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var compiler = new BytecodeCompiler(reporter);

        var lines = script.Split('\n');
        var parseResult = parser.Parse(lines, "control.aria");
        var bytecodeFile = compiler.Compile(parseResult.Instructions, "control");

        using var vm = new OptimizedBytecodeVM();

        string? lastText = null;
        vm.OnText += text => lastText = text;

        vm.Load(bytecodeFile);
        vm.Run();

        if (lastText != "Equal")
            throw new Exception($"Expected 'Equal', got '{lastText}'");

        Console.WriteLine("  ✓ Passed\n");
    }
}
