using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using AriaEngine.Core;
using AriaEngine.Core.Commands;
using AriaEngine.Rendering;

namespace AriaEngine.Tests;

public class CommandTests
{
    [Fact]
    public void TryGet_RegisteredCommands_ReturnsExpectedOpcodes()
    {
        CommandRegistry.TryGet("mov", out var mov).Should().BeTrue();
        mov.Should().Be(OpCode.Mov);

        CommandRegistry.TryGet("lsp", out var lsp).Should().BeTrue();
        lsp.Should().Be(OpCode.Lsp);
    }

    [Fact]
    public void TryGet_AliasesResolveToCanonicalOpcode()
    {
        CommandRegistry.TryGet("goto", out var gotoOp).Should().BeTrue();
        CommandRegistry.TryGet("jmp", out var jmpOp).Should().BeTrue();
        gotoOp.Should().Be(OpCode.Jmp);
        jmpOp.Should().Be(OpCode.Jmp);

        CommandRegistry.TryGet("let", out var letOp).Should().BeTrue();
        letOp.Should().Be(OpCode.Mov);
    }

    [Fact]
    public void GetInfo_ReturnsCategoryAndMinArgs()
    {
        var mov = CommandRegistry.GetInfo("mov");
        mov.Should().NotBeNull();
        mov!.Category.Should().Be(CommandCategory.Core);
        mov.MinArgs.Should().Be(2);

        var setPFlag = CommandRegistry.GetInfo("set_pflag");
        setPFlag.Should().NotBeNull();
        setPFlag!.Category.Should().Be(CommandCategory.Flags);
        setPFlag.MinArgs.Should().Be(2);
    }

    [Fact]
    public void SubAlias_CurrentlyResolvesToDefsubCompatibilityBehavior()
    {
        CommandRegistry.TryGet("sub", out var op).Should().BeTrue();
        op.Should().Be(OpCode.Defsub);
    }

    [Fact]
    public void UnknownCommand_ReturnsFalse()
    {
        CommandRegistry.TryGet("not_a_command", out _).Should().BeFalse();
    }

    [Fact]
    public void FlagCommandHandler_HandlesPersistentFlagRoundTrip()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new FlagCommandHandler(vm);

        handler.HandledCodes.Should().Contain(OpCode.SetPFlag);
        handler.HandledCodes.Should().Contain(OpCode.GetPFlag);

        handler.Execute(new Instruction
        {
            Op = OpCode.SetPFlag,
            Arguments = new List<string> { "chapter_day1", "1" }
        }).Should().BeTrue();
        handler.Execute(new Instruction
        {
            Op = OpCode.GetPFlag,
            Arguments = new List<string> { "chapter_day1", "%10" }
        }).Should().BeTrue();

        vm.State.Flags["chapter_day1"].Should().BeTrue();
        vm.State.Registers["10"].Should().Be(1);
    }

    [Fact]
    public void CoreCommandHandler_HandlesStartupCommands()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new CoreCommandHandler(vm);

        handler.HandledCodes.Should().Contain(OpCode.Window);
        handler.Execute(new Instruction
        {
            Op = OpCode.Window,
            Arguments = new List<string> { "800", "600", "Test Window" }
        }).Should().BeTrue();
        handler.Execute(new Instruction
        {
            Op = OpCode.FontAtlasSize,
            Arguments = new List<string> { "64" }
        }).Should().BeTrue();
        handler.Execute(new Instruction
        {
            Op = OpCode.Debug,
            Arguments = new List<string> { "on" }
        }).Should().BeTrue();

        vm.State.WindowWidth.Should().Be(800);
        vm.State.WindowHeight.Should().Be(600);
        vm.State.Title.Should().Be("Test Window");
        vm.State.FontAtlasSize.Should().Be(64);
        vm.State.DebugMode.Should().BeTrue();
    }
}
