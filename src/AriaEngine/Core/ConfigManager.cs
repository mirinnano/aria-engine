using System;
using System.IO;
using System.Text.Json;

namespace AriaEngine.Core;

public class AppConfig
{
    public int GlobalTextSpeedMs { get; set; } = 30;
    public int DefaultTextSpeedMs { get; set; } = 30; // engine default
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
    public bool IsFullscreen { get; set; } = false;
    public string TextMode { get; set; } = "adv"; // "adv" or "nvl"
}

public class ConfigManager
{
    private string _configPath = "config.json";
    public AppConfig Config { get; private set; } = new();

    public void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定ファイルの読み込みに失敗しました: {ex.Message}");
            }
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"設定ファイルの保存に失敗しました: {ex.Message}");
        }
    }
}
