using AriaEngine.Core;
using AriaEngine.Rendering;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var workspace = Path.Combine(Path.GetTempPath(), "aria-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(workspace);
var originalCwd = Environment.CurrentDirectory;
Environment.CurrentDirectory = workspace;

try
{
    var reporter = new ErrorReporter();
    var parser = new Parser(reporter);

    var parsed = parser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "textbox 0, 0, 640, 120",
        "本文: コロンを含む文章\\",
        "lsp_rect 1, 0, 0, 100, 40",
        "spbtn 1, 50",
        "btnwait %0",
        "if %0 == 50 goto *unlock_all",
        "goto *end",
        "*unlock_all",
        "set_pflag chapter_day1, 1",
        "csp -1",
        "*end"
    }, "smoke.aria");

    Assert(parsed.Instructions.Any(i => i.Op == OpCode.Text && i.Arguments.Count > 0 && i.Arguments[0].Contains(":")), "Parser split plain text containing ':'");

    var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager());
    vm.LoadScript(parsed.Instructions, parsed.Labels, "smoke.aria");
    vm.Step();
    Assert(vm.State.State == VmState.WaitingForClick, "VM did not wait for text click");
    vm.ResumeFromClick();
    vm.Step();
    Assert(vm.State.State == VmState.WaitingForButton, "VM did not reach button wait");
    vm.ResumeFromButton(1);
    vm.Step();

    Assert(vm.State.Flags.TryGetValue("chapter_day1", out var unlocked) && unlocked, "UNLOCK ALL style branch did not set pflag");
    Assert(vm.State.Sprites.Count == 0, "csp -1 did not clear sprites");
    Assert(vm.State.SpriteButtonMap.Count == 0, "csp -1 did not clear button map");

    vm.JumpTo("*missing_label");
    Assert(vm.State.State != VmState.Ended || reporter.Errors.Any(e => e.Code == "VM_LABEL_MISSING"), "Missing label was not reported safely");

    var saveReporter = new ErrorReporter();
    var saveParser = new Parser(saveReporter);
    var saveScript = saveParser.Parse(new[]
    {
        "compat_mode on",
        "textspeed 0",
        "textbox 0, 0, 640, 120",
        "ロード前@",
        "ロード後@"
    }, "save-load.aria");

    var saveVm = new VirtualMachine(saveReporter, new TweenManager(), new SaveManager(saveReporter), new ConfigManager());
    saveVm.LoadScript(saveScript.Instructions, saveScript.Labels, "save-load.aria");
    saveVm.Step();
    Assert(saveVm.State.State == VmState.WaitingForClick, "Save fixture did not reach first wait");
    saveVm.SaveGame(1);

    var loadVm = new VirtualMachine(saveReporter, new TweenManager(), new SaveManager(saveReporter), new ConfigManager());
    loadVm.LoadScript(saveScript.Instructions, saveScript.Labels, "save-load.aria");
    loadVm.LoadGame(1);
    Assert(loadVm.State.TextTargetSpriteId >= 0, "Load did not restore text target id");
    Assert(loadVm.State.TextboxBackgroundSpriteId >= 0, "Load did not restore textbox background id");
    loadVm.ResumeFromClick();
    loadVm.Step();

    int textboxRects = loadVm.State.Sprites.Values.Count(s => s.Type == SpriteType.Rect && s.Z == 9000);
    int textTargets = loadVm.State.Sprites.Values.Count(s => s.Type == SpriteType.Text && s.Z == 9001);
    Assert(textboxRects <= 1, "Load created duplicate textbox background sprites");
    Assert(textTargets <= 1, "Load created duplicate textbox text sprites");

    Console.WriteLine("ARIA smoke tests passed.");
}
finally
{
    Environment.CurrentDirectory = originalCwd;
    try { Directory.Delete(workspace, recursive: true); } catch { }
}
