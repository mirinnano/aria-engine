using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class LocalizationManagerTests
{
    [Fact]
    public void Lookup_UsesCurrentLanguageThenFallback()
    {
        var provider = new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["assets/i18n/locales.json"] = """
            { "defaultLanguage": "ja-JP", "fallbackLanguage": "ja-JP", "languages": ["ja-JP", "en-US"], "resources": ["ui"], "fonts": { "en-US": "fonts/en.ttf" } }
            """,
            ["assets/i18n/ui.ja-JP.json"] = """{ "menu.save": "セーブ", "menu.load": "ロード" }""",
            ["assets/i18n/ui.en-US.json"] = """{ "menu.save": "Save" }"""
        });

        var manager = LocalizationManager.Load(provider, "assets/i18n/locales.json");
        manager.SetLanguage("en-US");

        manager.Get("menu.save").Should().Be("Save");
        manager.Get("menu.load").Should().Be("ロード");
        manager.Get("missing.key").Should().Be("missing.key");
        manager.EnumerateTextForGlyphs().Should().Contain(new[] { "Save", "ロード" });
        manager.GetFontForLanguage("en-US").Should().Be("fonts/en.ttf");
    }

    [Fact]
    public void Format_UsesLocalizedTemplateAndDateFormat()
    {
        var provider = new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["assets/i18n/locales.json"] = """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP", "en-US"],
              "resources": ["ui"],
              "dateFormat": {
                "ja-JP": "yyyy/MM/dd HH:mm",
                "en-US": "MM/dd/yyyy HH:mm"
              }
            }
            """,
            ["assets/i18n/ui.ja-JP.json"] = """{ "confirm.load_slot": "スロット{0:00}をロードしますか？" }""",
            ["assets/i18n/ui.en-US.json"] = """{ "confirm.load_slot": "LOAD SLOT {0:00}?" }"""
        });

        var manager = LocalizationManager.Load(provider, "assets/i18n/locales.json");
        manager.SetLanguage("en-US");

        manager.Format("confirm.load_slot", 5).Should().Be("LOAD SLOT 05?");
        manager.GetDateFormat().Should().Be("MM/dd/yyyy HH:mm");
    }

    [Fact]
    public void EngineOwnedMenuLabels_UseCurrentLocalization()
    {
        var provider = new InMemoryAssetProvider(new Dictionary<string, string>
        {
            ["assets/i18n/locales.json"] = """
            { "defaultLanguage": "ja-JP", "fallbackLanguage": "ja-JP", "languages": ["ja-JP", "en-US"], "resources": ["ui"] }
            """,
            ["assets/i18n/ui.ja-JP.json"] = """
            { "menu.save": "セーブ", "menu.load": "ロード", "menu.backlog": "回想", "menu.skip": "スキップ", "menu.reset": "リセット", "menu.end": "終了", "menu.settings": "設定" }
            """,
            ["assets/i18n/ui.en-US.json"] = """
            { "menu.save": "Save", "menu.load": "Load", "menu.backlog": "Backlog", "menu.skip": "Skip", "menu.reset": "Reset", "menu.end": "Quit", "menu.settings": "Settings" }
            """
        });
        var reporter = new ErrorReporter();
        var vm = new VirtualMachine(reporter, new TweenManager(), new SaveManager(reporter), new ConfigManager())
        {
            Localization = LocalizationManager.Load(provider, "assets/i18n/locales.json")
        };
        vm.Localization.SetLanguage("en-US");

        var method = vm.Menu.GetType().GetMethod("GetVisibleMainEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var entries = ((IEnumerable<RightMenuEntry>)method!.Invoke(vm.Menu, null)!).ToList();

        entries.Should().Contain(e => e.Action == "save" && e.Label == "Save");
        entries.Should().Contain(e => e.Action == "lookback" && e.Label == "Backlog");
        entries.Should().Contain(e => e.Action == "settings" && e.Label == "Settings");
    }
}
