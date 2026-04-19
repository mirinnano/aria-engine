using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AriaEngine.Core;

public class ChapterInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ScriptPath { get; set; } = "";
    public bool IsUnlocked { get; set; } = false;
    public string ThumbnailPath { get; set; } = "";
    public int LastProgress { get; set; } = 0;
    public DateTime? LastPlayed { get; set; }
}

public class ChapterData
{
    public List<ChapterInfo> Chapters { get; set; } = new();
}

public class ChapterManager
{
    private List<ChapterInfo> _chapters = new();
    private string _dataPath = "chapters.json";
    private readonly ErrorReporter _reporter;

    public ChapterManager(ErrorReporter reporter)
    {
        _reporter = reporter;
    }

    public void LoadChapters()
    {
        if (!File.Exists(_dataPath))
        {
            // デフォルトチャプターを作成
            CreateDefaultChapters();
            SaveChapters();
            return;
        }

        try
        {
            string json = File.ReadAllText(_dataPath);
            var data = JsonSerializer.Deserialize<ChapterData>(json);
            if (data != null && data.Chapters != null)
            {
                _chapters = data.Chapters;
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"チャプターデータの読み込みに失敗しました: {ex.Message}", -1, _dataPath, AriaErrorLevel.Warning));
            CreateDefaultChapters();
        }
    }

    public void SaveChapters()
    {
        try
        {
            var data = new ChapterData { Chapters = _chapters };
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"チャプターデータの保存に失敗しました: {ex.Message}", -1, _dataPath, AriaErrorLevel.Warning));
        }
    }

    public List<ChapterInfo> GetAvailableChapters()
    {
        return _chapters;
    }

    public void UnlockChapter(int chapterId)
    {
        var chapter = _chapters.Find(c => c.Id == chapterId);
        if (chapter != null && !chapter.IsUnlocked)
        {
            chapter.IsUnlocked = true;
            chapter.LastPlayed = DateTime.Now;
        }
    }

    public void UpdateProgress(int chapterId, int progress)
    {
        var chapter = _chapters.Find(c => c.Id == chapterId);
        if (chapter != null)
        {
            chapter.LastProgress = Math.Max(0, Math.Min(100, progress));
            chapter.LastPlayed = DateTime.Now;
        }
    }

    public ChapterInfo? GetChapter(int chapterId)
    {
        return _chapters.Find(c => c.Id == chapterId);
    }

    public ChapterInfo? GetChapterByIndex(int index)
    {
        if (index >= 0 && index < _chapters.Count)
        {
            return _chapters[index];
        }
        return null;
    }

    // スクリプトからのチャプター追加
    public void AddChapter(ChapterInfo chapter)
    {
        var existing = _chapters.FirstOrDefault(c => c.Id == chapter.Id);
        if (existing != null)
        {
            // 既存のチャプターを更新
            existing.Title = chapter.Title;
            existing.Description = chapter.Description;
            existing.ScriptPath = chapter.ScriptPath;
            existing.ThumbnailPath = chapter.ThumbnailPath;
        }
        else
        {
            // 新しいチャプターを追加
            _chapters.Add(chapter);
        }
    }

    private void CreateDefaultChapters()
    {
        _chapters = new List<ChapterInfo>
        {
            new ChapterInfo
            {
                Id = 1,
                Title = "第一章 はじまり",
                Description = "物語の始まり。新しい世界への第一歩。",
                ScriptPath = "assets/scripts/main.aria",
                IsUnlocked = true,
                ThumbnailPath = "",
                LastProgress = 0,
                LastPlayed = null
            },
            new ChapterInfo
            {
                Id = 2,
                Title = "第二章 展開",
                Description = "物語が大きく動き出す。",
                ScriptPath = "assets/scripts/chapter2.aria",
                IsUnlocked = false,
                ThumbnailPath = "",
                LastProgress = 0,
                LastPlayed = null
            },
            new ChapterInfo
            {
                Id = 3,
                Title = "第三章 結末",
                Description = "すべての謎が解き明かされる。",
                ScriptPath = "assets/scripts/chapter3.aria",
                IsUnlocked = false,
                ThumbnailPath = "",
                LastProgress = 0,
                LastPlayed = null
            }
        };
    }
}
