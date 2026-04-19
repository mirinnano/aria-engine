using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace AriaEngine.Core;

public class SaveManager
{
    private readonly ErrorReporter _reporter;
    private readonly string _screenshotDir = "screenshots";

    public SaveManager(ErrorReporter reporter)
    {
        _reporter = reporter;
        CreateScreenshotDirectory();
    }

    private string GetSavePath(int slot) => $"save_data_{slot}.json";

    public void Save(int slot, GameState state, string currentScriptFile)
    {
        try
        {
            var options = new JsonSerializerOptions {
                WriteIndented = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            // プレイ時間を計算
            TimeSpan playTime = DateTime.Now - state.SessionStartTime + state.TotalPlayTime;

            var saveObj = new SaveData
            {
                SlotId = slot,
                ScriptFile = currentScriptFile,
                State = state,
                SaveTime = DateTime.Now,
                ChapterTitle = state.CurrentChapter,
                PreviewText = state.CurrentTextBuffer.Length > 0 ? state.CurrentTextBuffer.Substring(0, Math.Min(50, state.CurrentTextBuffer.Length)) : "",
                PlayTime = playTime,
                ThumbnailWidth = 320,
                ThumbnailHeight = 180
            };

            var json = JsonSerializer.Serialize(saveObj, options);
            File.WriteAllText(GetSavePath(slot), json);
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"セーブ失敗: {ex.Message}", -1, GetSavePath(slot), AriaErrorLevel.Error));
        }
    }

    public (SaveData? Data, bool Success) Load(int slot)
    {
        string path = GetSavePath(slot);
        if (!File.Exists(path)) return (null, false);

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SaveData>(json);
            return (data, true);
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"ロード失敗: {ex.Message}", -1, path, AriaErrorLevel.Error));
            return (null, false);
        }
    }

    public List<SaveData> GetAllSaveSlots()
    {
        var saves = new List<SaveData>();
        for (int i = 0; i < 10; i++) // 10スロット
        {
            var (data, success) = Load(i);
            if (success && data != null)
            {
                saves.Add(data);
            }
            else
            {
                saves.Add(new SaveData { SlotId = i });
            }
        }
        return saves;
    }

    public SaveData? GetSaveData(int slot)
    {
        var (data, success) = Load(slot);
        return success ? data : null;
    }

    public string GetSaveFilePath(int slot) => GetSavePath(slot);

    public bool HasSaveData(int slot)
    {
        return File.Exists(GetSavePath(slot));
    }

    public void DeleteSave(int slot)
    {
        try
        {
            string path = GetSavePath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"セーブ削除失敗: {ex.Message}", -1, GetSavePath(slot), AriaErrorLevel.Warning));
        }
    }

    private void CreateScreenshotDirectory()
    {
        if (!Directory.Exists(_screenshotDir))
        {
            Directory.CreateDirectory(_screenshotDir);
        }
    }
}

public class SaveData
{
    public int SlotId { get; set; }
    public string ScriptFile { get; set; } = "";
    public GameState State { get; set; } = new();
    public DateTime SaveTime { get; set; }
    public string ScreenshotPath { get; set; } = "";
    public string ChapterTitle { get; set; } = "";
    public string PreviewText { get; set; } = "";
    public TimeSpan PlayTime { get; set; } = TimeSpan.Zero;
    public int ThumbnailWidth { get; set; } = 320;
    public int ThumbnailHeight { get; set; } = 180;
    public byte[] ScreenshotData { get; set; } = Array.Empty<byte>();
}
