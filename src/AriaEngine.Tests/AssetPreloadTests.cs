#nullable enable

using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Scripting;
using AriaEngine.Tests.TestSupport;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace AriaEngine.Tests;

public sealed class AssetPreloadTests
{
    [Fact]
    public void Parser_CompilerFacingOpcode_IsRegistered()
    {
        var result = new Parser(new ErrorReporter()).Parse(
            new[] { "asset_preload \"scenario_01\"", "end" },
            "preload.aria");

        result.Instructions[0].Op.Should().Be(OpCode.AssetPreload);
        result.Instructions[0].Arguments.Should().Equal("scenario_01");
        CommandRegistry.GetInfo(OpCode.AssetPreload)!.MinArgs.Should().Be(1);
    }

    [Fact]
    public void NativeLoader_CompletesImmediately()
    {
        VirtualMachine vm = CreateVm(ImmediateAssetGroupLoader.Instance);
        vm.LoadScript(Parse("asset_preload \"scenario_01\"\nend"), "native.aria");

        vm.Step();

        vm.State.Execution.State.Should().Be(VmState.Ended);
        vm.State.AssetPreload.GroupName.Should().BeEmpty();
    }

    [Fact]
    public void ScriptCompiler_PreservesAssetPreloadOpcode()
    {
        var reporter = new ErrorReporter();
        var provider = new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = "script \"main.aria\"",
            ["main.aria"] = "asset_preload \"scenario_04\"\nend"
        });
        var compiler = new ScriptCompiler(new Parser(reporter), reporter, provider);

        CompiledScriptBundle bundle = compiler.CompileBundle("init.aria", "main.aria");

        bundle.Scripts["main.aria"].Instructions
            .Should().Contain(instruction => instruction.Op == (int)OpCode.AssetPreload);
    }

    [Fact]
    public void AsyncLoader_PausesUntilSuccess()
    {
        var loader = new FakeAssetGroupLoader(AssetGroupLoadResult.Loading());
        VirtualMachine vm = CreateVm(loader);
        vm.LoadScript(Parse("asset_preload \"scenario_02\"\nend"), "web.aria");

        vm.Step();
        vm.State.Execution.State.Should().Be(VmState.WaitingForAssetGroup);
        vm.State.AssetPreload.GroupName.Should().Be("scenario_02");

        loader.Complete("scenario_02");
        vm.State.Execution.State.Should().Be(VmState.Running);
        vm.Step();
        vm.State.Execution.State.Should().Be(VmState.Ended);
    }

    [Fact]
    public void AsyncLoader_FailureCanBeRetriedWithoutAdvancingPc()
    {
        var loader = new FakeAssetGroupLoader(AssetGroupLoadResult.Loading());
        VirtualMachine vm = CreateVm(loader);
        vm.LoadScript(Parse("asset_preload \"scenario_03\"\nend"), "web.aria");
        vm.Step();
        int pcAfterInstruction = vm.State.Execution.ProgramCounter;

        loader.Fail("scenario_03", "offline");
        vm.State.AssetPreload.IsFailed.Should().BeTrue();
        vm.State.Execution.ProgramCounter.Should().Be(pcAfterInstruction);

        loader.NextResult = AssetGroupLoadResult.Loading();
        vm.RetryAssetPreload().Should().BeTrue();
        vm.State.AssetPreload.Attempt.Should().Be(2);
        vm.State.Execution.ProgramCounter.Should().Be(pcAfterInstruction);

        loader.Complete("scenario_03");
        vm.State.Execution.State.Should().Be(VmState.Running);
    }

    private static VirtualMachine CreateVm(IAssetGroupLoader loader)
    {
        var reporter = new ErrorReporter();
        return new VirtualMachine(
            reporter,
            new TweenManager(),
            new SaveManager(reporter),
            new ConfigManager(reporter),
            assetGroupLoader: loader);
    }

    private static ParseResult Parse(string script) =>
        new Parser(new ErrorReporter()).Parse(script.Split('\n'), "test.aria");

    private sealed class FakeAssetGroupLoader : IAssetGroupLoader
    {
        public FakeAssetGroupLoader(AssetGroupLoadResult result) => NextResult = result;

        public event Action<string>? GroupLoaded;
        public event Action<string, string>? GroupLoadFailed;
        public AssetGroupLoadResult NextResult { get; set; }
        public AssetGroupLoadResult Request(string groupName) => NextResult;
        public void Complete(string groupName) => GroupLoaded?.Invoke(groupName);
        public void Fail(string groupName, string error) => GroupLoadFailed?.Invoke(groupName, error);
    }
}
