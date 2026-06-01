using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Core;
using AriaEngine.Tests.TestSupport;
using AriaEngine.Web.Assets;
using AriaEngine.Web.Input;
using AriaEngine.Web.Rendering;
using AriaEngine.Web.Runtime;
using AriaEngine.Web.Storage;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class PlatformBoundaryTests
{
    [Fact]
    public void CoreState_DoesNotExposeRaylibTypes()
    {
        string coreDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AriaEngine", "Core"));
        string[] files = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            text.Should().NotContain("Raylib_cs.TextureFilter", Path.GetFileName(file));
            text.Should().NotContain("using Raylib_cs;", Path.GetFileName(file));
        }
    }

    [Fact]
    public void CanvasScaleMapper_PreservesNativeLogicalCoordinatesAcross16x9Viewports()
    {
        var desktop = CanvasScaleMapper.Create(cssWidth: 1920, cssHeight: 1080);
        desktop.Scale.Should().BeApproximately(1.5, 0.0001);
        desktop.OffsetX.Should().BeApproximately(0, 0.0001);
        desktop.OffsetY.Should().BeApproximately(0, 0.0001);
        desktop.MapLogicalToCss(640, 360).Should().Be(new CssPoint(960, 540));
        desktop.MapCssToLogical(960, 540).Should().Be(new LogicalPoint(640, 360));

        var mobilePortrait = CanvasScaleMapper.Create(cssWidth: 390, cssHeight: 844);
        mobilePortrait.Scale.Should().BeApproximately(390d / 1280d, 0.0001);
        mobilePortrait.OffsetY.Should().BeApproximately((844 - (720 * mobilePortrait.Scale)) / 2, 0.0001);
        mobilePortrait.MapLogicalToCss(640, 360).Should().Be(new CssPoint(195, 422));
        mobilePortrait.MapCssToLogical(195, 422).Should().Be(new LogicalPoint(640, 360));
    }

    [Fact]
    public void BrowserInputMapper_MapsPointerHitTestsBackToNativeLogicalRects()
    {
        var mapper = CanvasScaleMapper.Create(cssWidth: 390, cssHeight: 844);
        var input = new BrowserInputMapper(mapper);
        var button = new LogicalRect(100, 50, 200, 100);
        CssPoint buttonCenter = mapper.MapLogicalToCss(200, 100);

        input.MapPointerToLogical(buttonCenter.X + 10, buttonCenter.Y + 20, canvasLeft: 10, canvasTop: 20)
            .Should().Be(new LogicalPoint(200, 100));
        input.IsPointerInside(button, buttonCenter.X + 10, buttonCenter.Y + 20, canvasLeft: 10, canvasTop: 20)
            .Should().BeTrue();
        input.IsPointerInside(button, 10, 20, canvasLeft: 10, canvasTop: 20)
            .Should().BeFalse();
    }

    [Fact]
    public void BrowserRenderer_PreservesNativeSpriteLayoutInDrawCommands()
    {
        var mapper = CanvasScaleMapper.Create(cssWidth: 1920, cssHeight: 1080);
        var renderer = new BrowserRenderer(mapper);
        var sprite = new Sprite
        {
            Id = 42,
            Type = SpriteType.Text,
            X = 120,
            Y = 80,
            Width = 640,
            Height = 180,
            FontSize = 32,
            Text = "Hello Web",
            Color = "#e7e2d6",
            TextAlign = TextAlignment.Center,
            TextVAlign = TextVerticalAlignment.Center,
            Opacity = 0.8f,
            Z = 9000
        };

        BrowserDrawCommand command = renderer.ToDrawCommands(new[] { sprite }).Should().ContainSingle().Subject;

        command.Kind.Should().Be(BrowserDrawKind.Text);
        command.SpriteId.Should().Be(42);
        command.CssX.Should().BeApproximately(180, 0.0001);
        command.CssY.Should().BeApproximately(120, 0.0001);
        command.CssWidth.Should().BeApproximately(960, 0.0001);
        command.CssHeight.Should().BeApproximately(270, 0.0001);
        command.LogicalX.Should().Be(120);
        command.LogicalY.Should().Be(80);
        command.LogicalWidth.Should().Be(640);
        command.LogicalHeight.Should().Be(180);
        command.FontSize.Should().Be(32);
        command.Text.Should().Be("Hello Web");
        command.Color.Should().Be("#e7e2d6");
        command.TextAlign.Should().Be("center");
        command.TextVAlign.Should().Be("center");
        command.Opacity.Should().BeApproximately(0.8, 0.0001);
        command.Z.Should().Be(9000);
    }

    [Fact]
    public void BrowserRenderer_MarksZeroSizedImagesForNaturalSizeFallback()
    {
        var mapper = CanvasScaleMapper.Create(cssWidth: 1280, cssHeight: 720);
        var renderer = new BrowserRenderer(mapper);
        var sprite = new Sprite
        {
            Id = 7,
            Type = SpriteType.Image,
            ImagePath = "assets/bg/title.png",
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0
        };

        BrowserDrawCommand command = renderer.ToDrawCommands(new[] { sprite }).Should().ContainSingle().Subject;

        command.Kind.Should().Be(BrowserDrawKind.Image);
        command.CssWidth.Should().Be(0);
        command.CssHeight.Should().Be(0);
        command.UseNaturalImageSize.Should().BeTrue();
    }

    [Fact]
    public void BrowserRenderer_NormalizesNativeAssetRootImagePathsForStaticWebPackage()
    {
        var mapper = CanvasScaleMapper.Create(cssWidth: 1280, cssHeight: 720);
        var renderer = new BrowserRenderer(mapper);
        var sprite = new Sprite
        {
            Id = 0,
            Type = SpriteType.Image,
            ImagePath = "bg/title.png",
            Width = 1280,
            Height = 720
        };

        BrowserDrawCommand command = renderer.ToDrawCommands(new[] { sprite }).Should().ContainSingle().Subject;

        command.ImagePath.Should().Be("assets/bg/title.png");
    }

    [Fact]
    public void BrowserRenderer_PreservesNativeDecorationForCanvasParity()
    {
        var mapper = CanvasScaleMapper.Create(cssWidth: 1280, cssHeight: 720);
        var renderer = new BrowserRenderer(mapper);
        var rect = new Sprite
        {
            Id = 30,
            Type = SpriteType.Rect,
            Width = 240,
            Height = 42,
            BorderColor = "#9aa18f",
            BorderWidth = 1,
            BorderOpacity = 142,
            ShadowColor = "#000000",
            ShadowOffsetX = 0,
            ShadowOffsetY = 4,
            ShadowAlpha = 150,
            CornerRadius = 4
        };
        var text = new Sprite
        {
            Id = 1,
            Type = SpriteType.Text,
            Text = "海風",
            TextShadowColor = "#000000",
            TextShadowX = 0,
            TextShadowY = 4
        };

        BrowserDrawCommand[] commands = renderer.ToDrawCommands(new[] { rect, text }).ToArray();
        BrowserDrawCommand rectCommand = commands.Single(command => command.SpriteId == 30);
        BrowserDrawCommand textCommand = commands.Single(command => command.SpriteId == 1);

        rectCommand.BorderColor.Should().Be("#9aa18f");
        rectCommand.BorderWidth.Should().Be(1);
        rectCommand.BorderOpacity.Should().Be(142);
        rectCommand.ShadowColor.Should().Be("#000000");
        rectCommand.ShadowOffsetY.Should().Be(4);
        rectCommand.ShadowAlpha.Should().Be(150);
        rectCommand.CornerRadius.Should().Be(4);
        textCommand.TextShadowColor.Should().Be("#000000");
        textCommand.TextShadowY.Should().Be(4);
    }

    [Fact]
    public void BrowserFontLoader_UsesLocaleFontAndNormalizesAssetUrl()
    {
        var provider = new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["assets/i18n/locales.json"] = """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP", "en-US"],
              "resources": ["ui"],
              "fonts": {
                "ja-JP": "assets/fonts/NotoSansJP-Regular.ttf",
                "en-US": "assets/fonts/Inter-Regular.ttf"
              }
            }
            """,
            ["assets/i18n/ui.ja-JP.json"] = """{ "menu.save": "保存" }""",
            ["assets/i18n/ui.en-US.json"] = """{ "menu.save": "Save" }"""
        });
        var localization = LocalizationManager.Load(provider, "assets/i18n/locales.json");
        localization.SetLanguage("en-US");

        BrowserFontFace font = BrowserFontLoader.Resolve(localization, "assets/fonts/fallback.ttf");

        font.Family.Should().Be("AriaRuntime");
        font.SourceUrl.Should().Be("assets/fonts/Inter-Regular.ttf");
        font.CssDeclaration.Should().Contain("font-family: 'AriaRuntime'");
        font.CssDeclaration.Should().Contain("url('assets/fonts/Inter-Regular.ttf')");
    }

    [Fact]
    public void WebRuntimeHost_BootsInitAndMainScriptIntoPlayableFrame()
    {
        var host = WebRuntimeHost.Boot(CreatePlayableProvider(), new WebRuntimeOptions(CreateTempRuntimeRoot()));

        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1920, cssHeight: 1080);

        frame.ExecutionState.Should().Be(VmState.WaitingForButton);
        frame.LogicalWidth.Should().Be(1280);
        frame.LogicalHeight.Should().Be(720);
        frame.Font.SourceUrl.Should().Be("assets/fonts/NotoSansJP-Regular.ttf");
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Image && command.ImagePath == "assets/bg/title.png");
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text == "START");
        frame.DrawCommands.Should().Contain(command => command.SpriteId == 10 && command.CssX.ShouldApprox(750) && command.CssY.ShouldApprox(450));
    }

    [Fact]
    public void WebRuntimeHost_MapsPointerPressThroughNativeButtonState()
    {
        var host = WebRuntimeHost.Boot(CreatePlayableProvider(), new WebRuntimeOptions(CreateTempRuntimeRoot()));

        host.HandlePointerPress(cssX: 960, cssY: 532.5, cssWidth: 1920, cssHeight: 1080);
        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1920, cssHeight: 1080);

        frame.ExecutionState.Should().Be(VmState.WaitingForClick);
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text == "PLAYING");
    }

    [Fact]
    public void WebRuntimeHost_RendersCompatScenarioTextAfterClickWait()
    {
        var host = WebRuntimeHost.Boot(CreateCompatTextProvider(), new WebRuntimeOptions(CreateTempRuntimeRoot()));

        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1280, cssHeight: 720);

        frame.ExecutionState.Should().Be(VmState.WaitingForClick);
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text.Contains("春が来る"));
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Triangle && command.SpriteId == -9400);
        frame.DrawCommands.Should().NotContain(command => command.SpriteId == -9000);
        frame.DrawCommands.Should().Contain(command =>
            command.Kind == BrowserDrawKind.Rect &&
            command.BorderColor == "#9aa18f" &&
            command.ShadowColor == "#000000");
    }

    [Fact]
    public void PreloadedWebAssetProvider_OnlyStoresTextPayloadsAndTreatsMediaAsExternalAssets()
    {
        var provider = new PreloadedWebAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = "script \"assets/scripts/main.aria\"",
            ["assets/scripts/main.aria"] = "end"
        });

        provider.Exists("init.aria").Should().BeTrue();
        provider.Exists("assets/scripts/main.aria").Should().BeTrue();
        provider.Exists("assets/bg/title.png").Should().BeTrue();
        provider.Exists("bg/title.png").Should().BeTrue();
        provider.ReadAllText("assets/scripts/main.aria").Should().Be("end");
        provider.PreloadedByteCount.Should().BeLessThan(128);
        provider.Invoking(x => x.ReadAllBytes("assets/bg/title.png")).Should().Throw<FileNotFoundException>();
        provider.Invoking(x => x.ReadAllBytes("bg/title.png")).Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void WebRuntimeHost_RendersNativeAssetRootBackgroundThroughWebAssetProvider()
    {
        var provider = new PreloadedWebAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = """
            window 1280, 720, "Web Native Asset Root"
            compat_mode off
            font "assets/fonts/NotoSansJP-Regular.ttf"
            script "assets/scripts/main.aria"
            """,
            ["assets/scripts/main.aria"] = """
            *start
            bg "bg/title.png", 1
            ui_rect 10, 500, 300, 280, 70
            ui_text 11, "START", 560, 318, 160, 40
            ui_button 10, 1
            btnwait %0
            """
        });

        var host = WebRuntimeHost.Boot(provider, new WebRuntimeOptions(CreateTempRuntimeRoot()));

        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1280, cssHeight: 720);

        frame.DrawCommands.Should().Contain(command =>
            command.Kind == BrowserDrawKind.Image &&
            command.SpriteId == 0 &&
            command.ImagePath == "assets/bg/title.png");
    }

    [Fact]
    public void WebRuntimeHost_FinishesInteractiveScreenTweensBeforeCapturingFrame()
    {
        var provider = new PreloadedWebAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = """
            window 1280, 720, "Web Tween Settle"
            compat_mode off
            font "assets/fonts/NotoSansJP-Regular.ttf"
            script "assets/scripts/main.aria"
            """,
            ["assets/scripts/main.aria"] = """
            *start
            ui_rect 34, 520, 560, 240, 36
            ui sprite:34, opacity, 0
            ui_text 14, "EXIT", 640, 570
            ui sprite:14, opacity, 0
            ui_fade 34, 255, 600
            ui_fade 14, 255, 600
            ui_button 34, 6
            btnwait %0
            """
        });

        var host = WebRuntimeHost.Boot(provider, new WebRuntimeOptions(CreateTempRuntimeRoot()));

        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1280, cssHeight: 720);

        frame.ExecutionState.Should().Be(VmState.WaitingForButton);
        frame.DrawCommands.Should().Contain(command =>
            command.SpriteId == 34 &&
            command.Kind == BrowserDrawKind.Rect &&
            command.Opacity.ShouldApprox(1));
        frame.DrawCommands.Should().Contain(command =>
            command.SpriteId == 14 &&
            command.Kind == BrowserDrawKind.Text &&
            command.Text == "EXIT" &&
            command.Opacity.ShouldApprox(1));
    }

    [Fact]
    public void IndexedDbSaveStore_ReadSave_UsesSameSlotKeyAsWriteSave()
    {
        BrowserStorageOperation write = IndexedDbSaveStore.WriteSave(3, "{}");
        BrowserStorageOperation read = IndexedDbSaveStore.ReadSave(3);

        read.Area.Should().Be(BrowserStorageArea.IndexedDb);
        read.Kind.Should().Be(BrowserStorageOperationKind.Read);
        read.DatabaseName.Should().Be(write.DatabaseName);
        read.StoreName.Should().Be(write.StoreName);
        read.Key.Should().Be(write.Key);
    }

    [Fact]
    public void WebRuntimeHost_RightClickOpensMenuOverlayWithSaveLoadEntries()
    {
        var host = WebRuntimeHost.Boot(CreateMenuProvider(), new WebRuntimeOptions(CreateTempRuntimeRoot()));

        host.HandleContextMenu(cssX: 128, cssY: 96, cssWidth: 1280, cssHeight: 720);
        WebRuntimeFrame frame = host.CreateFrame(cssWidth: 1280, cssHeight: 720);

        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text == "■");
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text == "SAVE" && command.LogicalX < 90);
        frame.DrawCommands.Should().Contain(command => command.Kind == BrowserDrawKind.Text && command.Text == "LOAD" && command.LogicalX < 90);
        frame.DrawCommands.Should().Contain(command => command.SpriteId == -9100 && command.Kind == BrowserDrawKind.Rect);
    }

    [Fact]
    public void WebRuntimeHost_MenuSaveClickQueuesIndexedDbSaveWrite()
    {
        var host = WebRuntimeHost.Boot(CreateMenuProvider(), new WebRuntimeOptions(CreateTempRuntimeRoot()));

        host.HandleContextMenu(cssX: 128, cssY: 96, cssWidth: 1280, cssHeight: 720);
        host.HandlePointerPress(cssX: 90, cssY: 70, cssWidth: 1280, cssHeight: 720);

        BrowserStorageOperation operation = host.DrainStorageOperations().Should().ContainSingle().Subject;
        operation.Area.Should().Be(BrowserStorageArea.IndexedDb);
        operation.Kind.Should().Be(BrowserStorageOperationKind.Write);
        operation.StoreName.Should().Be("saves");
        operation.Key.Should().Be("save:000");
        operation.Payload.Should().StartWith("{");
        operation.Payload.Should().Contain("\"SlotId\": 0");
    }

    [Fact]
    public void WebRuntimeHost_MenuLoadClickQueuesIndexedDbReadAndAppliesLoadedSave()
    {
        var root = CreateTempRuntimeRoot();
        var host = WebRuntimeHost.Boot(CreateMenuProvider(), new WebRuntimeOptions(root));
        host.HandleContextMenu(cssX: 128, cssY: 96, cssWidth: 1280, cssHeight: 720);
        host.HandlePointerPress(cssX: 90, cssY: 70, cssWidth: 1280, cssHeight: 720);
        BrowserStorageOperation write = host.DrainStorageOperations().Should().ContainSingle().Subject;

        var reloaded = WebRuntimeHost.Boot(CreateMenuProvider(), new WebRuntimeOptions(root));
        reloaded.HandleContextMenu(cssX: 128, cssY: 96, cssWidth: 1280, cssHeight: 720);
        reloaded.HandlePointerPress(cssX: 90, cssY: 130, cssWidth: 1280, cssHeight: 720);
        BrowserStorageOperation read = reloaded.DrainStorageOperations().Should().ContainSingle().Subject;

        read.Kind.Should().Be(BrowserStorageOperationKind.Read);
        read.Key.Should().Be("save:000");
        reloaded.ApplyLoadedStorage(read, write.Payload).Should().BeTrue();
        reloaded.CreateFrame(cssWidth: 1280, cssHeight: 720).ExecutionState.Should().Be(VmState.WaitingForClick);
    }

    private static InMemoryAssetProvider CreatePlayableProvider()
    {
        return new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = """
            window 1280, 720, "Web Boot"
            compat_mode off
            font "assets/fonts/NotoSansJP-Regular.ttf"
            script "assets/scripts/main.aria"
            """,
            ["assets/scripts/main.aria"] = """
            *start
            ui_image 1, "assets/bg/title.png", 0, 0
            bg "#050607", 60
            transition bg, "#050607", "fade", 60
            wait 60
            ui_rect 10, 500, 300, 280, 70
            ui_text 11, "START", 560, 318, 160, 40
            ui_button 10, 1
            btnwait %0
            if %0 == 1 goto *play
            end

            *play
            ui_text 20, "PLAYING", 100, 100, 400, 60
            @
            """,
            ["assets/i18n/locales.json"] = """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP"],
              "resources": [],
              "fonts": {
                "ja-JP": "assets/fonts/NotoSansJP-Regular.ttf"
              }
            }
            """
        });
    }

    private static InMemoryAssetProvider CreateCompatTextProvider()
    {
        return new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = """
            window 1280, 720, "Web Text"
            compat_mode on
            font "assets/fonts/NotoSansJP-Regular.ttf"
            script "assets/scripts/main.aria"
            textbox 50, 500, 1180, 200
            fontsize 32
            textbox_style 8, 1, "#9aa18f", 116, 30, 22, 0, 6, "#000000", 150
            """,
            ["assets/scripts/main.aria"] = """
            *start
            bg "#050607", 0
            nvl
            春が来るたび、俺はあの不格好な雛菊を思い出す。@
            """,
            ["assets/i18n/locales.json"] = """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP"],
              "resources": [],
              "fonts": {
                "ja-JP": "assets/fonts/NotoSansJP-Regular.ttf"
              }
            }
            """
        });
    }

    private static InMemoryAssetProvider CreateMenuProvider()
    {
        return new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["init.aria"] = """
            window 1280, 720, "Web Menu"
            compat_mode on
            font "assets/fonts/NotoSansJP-Regular.ttf"
            script "assets/scripts/main.aria"
            textbox 50, 500, 1180, 200
            fontsize 32
            """,
            ["assets/scripts/main.aria"] = """
            *start
            rmenu "SAVE",save,"LOAD",load
            bg "#050607", 0
            nvl
            Web menu save load test.@
            """,
            ["assets/i18n/locales.json"] = """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP"],
              "resources": [],
              "fonts": {
                "ja-JP": "assets/fonts/NotoSansJP-Regular.ttf"
              }
            }
            """
        });
    }

    private static string CreateTempRuntimeRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "aria-web-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}

internal static class FluentApproxExtensions
{
    public static bool ShouldApprox(this double actual, double expected)
    {
        actual.Should().BeApproximately(expected, 0.0001);
        return true;
    }
}
