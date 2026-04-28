using System;

namespace AriaEngine.Tests;

/// <summary>
/// バイトコードVMテストプログラム
/// </summary>
class TestProgram
{
    static int Main(string[] args)
    {
        try
        {
            BytecodeVMTest.RunTests();
            Console.WriteLine();
            OptimizedVMTest.RunTests();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
