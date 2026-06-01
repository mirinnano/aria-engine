using System;
using System.IO;
using System.Text;
using AriaEngine.Tools;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class I18nCheckTests
{
    [Fact]
    public void I18nCheck_FindsMissingAndUnusedLocalizationKeys()
    {
        string root = Path.Combine(Path.GetTempPath(), "aria-i18n-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string i18nDir = Path.Combine(root, "assets", "i18n");
            string scriptDir = Path.Combine(root, "assets", "scripts");
            Directory.CreateDirectory(i18nDir);
            Directory.CreateDirectory(scriptDir);
            File.WriteAllText(Path.Combine(i18nDir, "locales.json"), """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP", "en-US"],
              "resources": ["ui"]
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.ja-JP.json"), """
            {
              "menu.start": "はじめる",
              "confirm.load_slot": "スロット{0:00}をロードしますか？",
              "menu.unused": "未使用"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.en-US.json"), """
            {
              "menu.start": "Start"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(scriptDir, "main.aria"), """
            loc_get $start, "menu.start"
            loc_format $load, "confirm.load_slot", 5
            """, Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try
            {
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = AriaI18nCheckCommand.Run(new[]
                {
                    "--root", root,
                    "--manifest", "assets/i18n/locales.json",
                    "--scripts", "assets/scripts"
                });

                exitCode.Should().Be(1);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            string text = output + error.ToString();
            text.Should().Contain("missing");
            text.Should().Contain("en-US");
            text.Should().Contain("confirm.load_slot");
            text.Should().Contain("unused");
            text.Should().Contain("ja-JP");
            text.Should().Contain("menu.unused");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void I18nCheck_TreatsUnusedKeysAsWarnings()
    {
        string root = Path.Combine(Path.GetTempPath(), "aria-i18n-check-unused-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string i18nDir = Path.Combine(root, "assets", "i18n");
            string scriptDir = Path.Combine(root, "assets", "scripts");
            Directory.CreateDirectory(i18nDir);
            Directory.CreateDirectory(scriptDir);
            File.WriteAllText(Path.Combine(i18nDir, "locales.json"), """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP"],
              "resources": ["ui"]
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.ja-JP.json"), """
            {
              "menu.start": "はじめる",
              "menu.unused": "未使用"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(scriptDir, "main.aria"), """
            loc_get $start, "menu.start"
            """, Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try
            {
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = AriaI18nCheckCommand.Run(new[]
                {
                    "--root", root,
                    "--manifest", "assets/i18n/locales.json",
                    "--scripts", "assets/scripts"
                });

                exitCode.Should().Be(0);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            string text = output + error.ToString();
            text.Should().Contain("warning");
            text.Should().Contain("unused");
            text.Should().Contain("menu.unused");
            text.Should().Contain("passed");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void I18nCheck_CanCountCodeLocalizationCallsAsReferences()
    {
        string root = Path.Combine(Path.GetTempPath(), "aria-i18n-check-code-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string i18nDir = Path.Combine(root, "assets", "i18n");
            string scriptDir = Path.Combine(root, "assets", "scripts");
            string codeDir = Path.Combine(root, "src");
            Directory.CreateDirectory(i18nDir);
            Directory.CreateDirectory(scriptDir);
            Directory.CreateDirectory(codeDir);
            File.WriteAllText(Path.Combine(i18nDir, "locales.json"), """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP"],
              "resources": ["ui"]
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.ja-JP.json"), """
            {
              "menu.start": "はじめる",
              "menu.save": "保存"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(scriptDir, "main.aria"), """
            loc_get $start, "menu.start"
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(codeDir, "MenuSystem.cs"), """
            private static readonly Dictionary<string, string> Labels = new()
            {
                ["save"] = "menu.save"
            };
            """, Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try
            {
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = AriaI18nCheckCommand.Run(new[]
                {
                    "--root", root,
                    "--manifest", "assets/i18n/locales.json",
                    "--scripts", "assets/scripts",
                    "--code", "src"
                });

                exitCode.Should().Be(0);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            string text = output + error.ToString();
            text.Should().NotContain("menu.save");
            text.Should().Contain("passed");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void I18nCheck_FailsWhenLocaleScenarioFileIsMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "aria-i18n-check-scenario-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string i18nDir = Path.Combine(root, "assets", "i18n");
            string scenarioDir = Path.Combine(root, "assets", "scripts", "scenario", "ja-JP");
            Directory.CreateDirectory(i18nDir);
            Directory.CreateDirectory(scenarioDir);
            File.WriteAllText(Path.Combine(i18nDir, "locales.json"), """
            {
              "defaultLanguage": "ja-JP",
              "fallbackLanguage": "ja-JP",
              "languages": ["ja-JP", "en-US"],
              "resources": ["ui"],
              "scenarioRoot": "assets/scripts/scenario",
              "scenarioFiles": ["scenario_01.aria"]
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.ja-JP.json"), """
            {
              "menu.start": "はじめる"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(i18nDir, "ui.en-US.json"), """
            {
              "menu.start": "Start"
            }
            """, Encoding.UTF8);
            File.WriteAllText(Path.Combine(scenarioDir, "scenario_01.aria"), """
            *scenario_01
            text "source"
            """, Encoding.UTF8);

            var output = new StringWriter();
            var error = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            try
            {
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = AriaI18nCheckCommand.Run(new[]
                {
                    "--root", root,
                    "--manifest", "assets/i18n/locales.json",
                    "--scripts", "assets/scripts"
                });

                exitCode.Should().Be(1);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }

            string text = output + error.ToString();
            text.Should().Contain("missing scenario file");
            text.Should().Contain("en-US");
            text.Should().Contain("scenario_01.aria");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
