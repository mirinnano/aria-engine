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
    public void ScopeCommands_AreRegistered()
    {
        CommandRegistry.TryGet("scope", out var scopeOp).Should().BeTrue();
        scopeOp.Should().Be(OpCode.ScopeEnter);
        CommandRegistry.TryGet("end_scope", out var endScopeOp).Should().BeTrue();
        endScopeOp.Should().Be(OpCode.ScopeExit);
    }

    [Fact]
    public void ScopeEnterAndExit_DefersSpriteLifetimeCleanup()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var flow = new FlowCommandHandler(vm);

        // enter a scope
        flow.Execute(new Instruction { Op = OpCode.ScopeEnter, Arguments = new List<string>(), SourceLine = 0 });
        // defer a sprite creation inside the scope
        flow.Execute(new Instruction { Op = OpCode.Defer, Arguments = new List<string> { "lsp", "999", "sprite.png", "0", "0" }, SourceLine = 0 });
        // exit scope
        flow.Execute(new Instruction { Op = OpCode.ScopeExit, Arguments = new List<string>(), SourceLine = 0 });

        // sprite 999 should be cleaned up
        vm.State.Render.Sprites.ContainsKey(999).Should().BeFalse();
        // no scope should remain
        vm.State.Execution.ScopeStack.Count.Should().Be(0);
    }

    [Fact]
    public void ScopeEnterAndExit_MultipleDefers_SpritesCleanedUpInLifo()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var flow = new FlowCommandHandler(vm);

        // Enter scope
        flow.Execute(new Instruction { Op = OpCode.ScopeEnter, Arguments = new List<string>(), SourceLine = 0 });

        // Defer two LSP calls to create two sprites
        flow.Execute(new Instruction { Op = OpCode.Defer, Arguments = new List<string> { "lsp", "1001", "sprite1.png", "0", "0" }, SourceLine = 0 });
        flow.Execute(new Instruction { Op = OpCode.Defer, Arguments = new List<string> { "lsp", "1002", "sprite2.png", "0", "0" }, SourceLine = 0 });

        // Exit scope should cleanup both defers in LIFO order
        flow.Execute(new Instruction { Op = OpCode.ScopeExit, Arguments = new List<string>(), SourceLine = 0 });

        vm.State.Render.Sprites.ContainsKey(1001).Should().BeFalse();
        vm.State.Render.Sprites.ContainsKey(1002).Should().BeFalse();
        vm.State.Execution.ScopeStack.Count.Should().Be(0);
    }

    [Fact]
    public void NscrEffectAndPrint_DefineAndRunTransitionEffect()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var render = new RenderCommandHandler(vm);

        render.Execute(new Instruction { Op = OpCode.Effect, Arguments = new List<string> { "12", "480", "wipe" }, SourceLine = 0 });
        render.Execute(new Instruction { Op = OpCode.Print, Arguments = new List<string> { "12" }, SourceLine = 0 });

        vm.State.Render.NscrEffects[12].DurationMs.Should().Be(480);
        vm.State.Render.NscrEffects[12].Transition.Should().Be(TransitionType.WipeCircle);
        vm.State.Render.IsFading.Should().BeTrue();
        vm.State.Render.FadeProgress.Should().Be(0f);
        vm.State.Render.FadeDurationMs.Should().Be(480);
        vm.State.Render.TransitionStyle.Should().Be(TransitionType.WipeCircle);
        vm.State.Execution.State.Should().Be(VmState.FadingIn);
        vm.State.Render.ActiveEffects.Should().Contain("nscr:print:12");
    }

    [Fact]
    public void ChapterScroll_AdjustsRenderedChapterCardOffset()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var compat = new CompatibilityCommandHandler(vm);
        vm.ChapterManager.AddChapter(new ChapterInfo { Id = 1, Title = "One", Description = "First", IsUnlocked = true });

        compat.Execute(new Instruction { Op = OpCode.ChapterSelect, Arguments = new List<string>(), SourceLine = 0 });
        float initialY = vm.State.Render.Sprites[2000].Y;

        compat.Execute(new Instruction { Op = OpCode.ChapterScroll, Arguments = new List<string> { "-48" }, SourceLine = 0 });

        vm.State.SceneRuntime.SceneData["chapter_scroll_offset"].Should().Be(-48);
        vm.State.Render.Sprites[2000].Y.Should().Be(initialY - 48);
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
    public void SubCommand_ResolvesToArithmeticSub()
    {
        CommandRegistry.TryGet("sub", out var op).Should().BeTrue();
        op.Should().Be(OpCode.Sub);
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

        vm.State.FlagRuntime.SaveFlags["chapter_day1"].Should().BeTrue();
        vm.State.RegisterState.Registers["10"].Should().Be(1);
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

        vm.State.EngineSettings.WindowWidth.Should().Be(800);
        vm.State.EngineSettings.WindowHeight.Should().Be(600);
        vm.State.EngineSettings.Title.Should().Be("Test Window");
        vm.State.EngineSettings.FontAtlasSize.Should().Be(64);
        vm.State.EngineSettings.DebugMode.Should().BeTrue();
    }

    [Fact]
    public void FlowCommandHandler_SetArray_IgnoresNegativeIndexWithoutThrowing()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new FlowCommandHandler(vm);

        var action = () => handler.Execute(new Instruction
        {
            Op = OpCode.SetArray,
            Arguments = new List<string> { "%arr", "-2", "1" }
        });

        action.Should().NotThrow();
        vm.State.RegisterState.Arrays.Should().NotContainKey("arr");
    }

    [Fact]
    public void FlowCommandHandler_LetArray_IgnoresNegativeIndexWithoutThrowing()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new FlowCommandHandler(vm);

        var action = () => handler.Execute(new Instruction
        {
            Op = OpCode.Let,
            Arguments = new List<string> { "%arr[-2]", "1" }
        });

        action.Should().NotThrow();
        vm.State.RegisterState.Arrays.Should().NotContainKey("arr");
    }

    [Fact]
    public void GalleryPageText_UsesEmptyStateWhenGalleryHasNoEntries()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var result = parser.Parse(new[]
        {
            "gallery_count %count",
            "let %page = 0",
            "let $pagestr = \"NO ENTRIES\"",
            "let %totalpages = 0",
            "if %count > 0",
            "    let %totalpages = %count + 5",
            "    div %totalpages, 6",
            "    let $pagestr = \"PAGE \" + (%page + 1) + \" / \" + %totalpages",
            "endif",
            "end"
        }, "gallery_empty.aria");

        vm.LoadScript(result, "gallery_empty.aria");
        vm.Step();

        vm.State.RegisterState.StringRegisters["pagestr"].Should().Be("NO ENTRIES");
    }

    [Fact]
    public void ConditionComparison_NamedZeroRegisterGreaterThanZero_IsFalse()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        vm.SetGlobalRegister("%count", 0);

        var condition = Condition.FromTokens(new[] { "%count", ">", "0" });

        vm.EvaluateCondition(condition).Should().BeFalse();
    }

    [Fact]
    public void BlockIfFalse_SkipsBody()
    {
        var reporter = new ErrorReporter();
        var parser = new Parser(reporter);
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var result = parser.Parse(new[]
        {
            "let %count = 0",
            "if %count > 0",
            "    let %hit = 1",
            "endif",
            "end"
        }, "block_if_false.aria");

        vm.LoadScript(result, "block_if_false.aria");
        vm.Step();

        vm.State.RegisterState.Registers.Should().NotContainKey("hit");
    }

    [Fact]
    public void UiControls_RenderAboveDefaultPanels()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new UiCommandHandler(vm);

        handler.Execute(new Instruction
        {
            Op = OpCode.UiSlider,
            Arguments = new List<string> { "220", "500", "172", "420", "0", "100", "50" }
        }).Should().BeTrue();
        handler.Execute(new Instruction
        {
            Op = OpCode.UiCheckbox,
            Arguments = new List<string> { "260", "230", "420", "SKIP UNREAD", "1" }
        }).Should().BeTrue();

        vm.State.Render.Sprites[220].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[221].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[222].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[223].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[260].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[261].Z.Should().BeGreaterThan(5);
        vm.State.Render.Sprites[262].Z.Should().BeGreaterThan(5);
    }

    [Fact]
    public void TryGet_AssertAndPanicCommands_AreRegistered()
    {
        CommandRegistry.TryGet("assert", out var assertOp).Should().BeTrue();
        assertOp.Should().Be(OpCode.Assert);

        CommandRegistry.TryGet("panic", out var panicOp).Should().BeTrue();
        panicOp.Should().Be(OpCode.Panic);
    }

    [Fact]
    public void ScriptCommandHandler_Assert_ReportsErrorWhenConditionIsFalse()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new ScriptCommandHandler(vm);

        handler.Execute(new Instruction
        {
            Op = OpCode.Assert,
            Arguments = new List<string> { "%0 == 0", "test message" }
        });

        reporter.Errors.Count.Should().BeGreaterThan(0);
        reporter.Errors[0].Code.Should().Be("VM_ASSERT_FAILED");
    }

    [Fact]
    public void ScriptCommandHandler_Panic_StopsVmAndReportsError()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new ScriptCommandHandler(vm);

        handler.Execute(new Instruction
        {
            Op = OpCode.Panic,
            Arguments = new List<string> { "test panic" }
        });

        reporter.Errors.Count.Should().BeGreaterThan(0);
        reporter.Errors.Should().Contain(e => e.Code == "VM_PANIC");
        vm.State.Execution.State.Should().Be(VmState.Ended);
    }

    // ===========================================================
    // T13: owned sprite lifetime tracking tests
    // ===========================================================

    [Fact]
    public void RenderCommandHandler_OwnedSprite_TrackLifetimeEvenWithoutScope()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new RenderCommandHandler(vm);

        // Mark %bg as owned
        vm.State.OwnedSprites.Add("%bg");

        // Create sprite at top level (no scope)
        handler.Execute(new Instruction
        {
            Op = OpCode.Lsp,
            Arguments = new List<string> { "%bg", "bg.png", "0", "0" }
        });

        // Sprite should exist
        vm.State.Render.Sprites.ContainsKey(0).Should().BeTrue();
        // Lifetime stack should have been pushed because it's owned
        vm.State.Execution.SpriteLifetimeStacks.Count.Should().Be(1);
        vm.State.Execution.SpriteLifetimeStacks.Peek().Should().Contain(0);
    }

    [Fact]
    public void RenderCommandHandler_OwnedSprite_CleanedUpOnScopeExit()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var render = new RenderCommandHandler(vm);
        var flow = new FlowCommandHandler(vm);

        // Mark %bg as owned
        vm.State.OwnedSprites.Add("%bg");

        // Enter scope
        flow.Execute(new Instruction { Op = OpCode.ScopeEnter, Arguments = new List<string>(), SourceLine = 0 });

        // Create owned sprite inside scope
        render.Execute(new Instruction
        {
            Op = OpCode.Lsp,
            Arguments = new List<string> { "%bg", "bg.png", "0", "0" }
        });

        vm.State.Render.Sprites.ContainsKey(0).Should().BeTrue();

        // Exit scope
        flow.Execute(new Instruction { Op = OpCode.ScopeExit, Arguments = new List<string>(), SourceLine = 0 });

        // Owned sprite should be cleaned up
        vm.State.Render.Sprites.ContainsKey(0).Should().BeFalse();
    }

    [Fact]
    public void RenderCommandHandler_NonOwnedSprite_NotTrackedAtTopLevel()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new RenderCommandHandler(vm);

        // Create sprite at top level without owning
        handler.Execute(new Instruction
        {
            Op = OpCode.Lsp,
            Arguments = new List<string> { "10", "sprite.png", "0", "0" }
        });

        // Sprite should exist
        vm.State.Render.Sprites.ContainsKey(10).Should().BeTrue();
        // No lifetime stack should exist at top level for non-owned sprites
        vm.State.Execution.SpriteLifetimeStacks.Count.Should().Be(0);
    }

    [Fact]
    public void RenderCommandHandler_OwnedSprite_LspTextAndLspRect_AlsoTracked()
    {
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
        var handler = new RenderCommandHandler(vm);
        var flow = new FlowCommandHandler(vm);

        vm.State.OwnedSprites.Add("%txt");
        vm.State.OwnedSprites.Add("%rect");

        // Initialize registers to distinct sprite IDs
        flow.Execute(new Instruction { Op = OpCode.Mov, Arguments = new List<string> { "%txt", "100" } });
        flow.Execute(new Instruction { Op = OpCode.Mov, Arguments = new List<string> { "%rect", "101" } });

        handler.Execute(new Instruction
        {
            Op = OpCode.LspText,
            Arguments = new List<string> { "%txt", "Hello", "0", "0" }
        });

        handler.Execute(new Instruction
        {
            Op = OpCode.LspRect,
            Arguments = new List<string> { "%rect", "0", "0", "100", "100" }
        });

        vm.State.Execution.SpriteLifetimeStacks.Count.Should().Be(1);
        vm.State.Execution.SpriteLifetimeStacks.Peek().Should().Contain(100); // txt
        vm.State.Execution.SpriteLifetimeStacks.Peek().Should().Contain(101); // rect
    }
}
