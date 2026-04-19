using System;
using System.Collections.Generic;
using System.IO;

namespace AriaEngine.Core;

public class HintSystem
{
    private List<string> _hints = new();
    private Random _random = new();
    private string _hintsFile = "hints.txt";
    private readonly ErrorReporter _reporter;

    public HintSystem(ErrorReporter reporter)
    {
        _reporter = reporter;
    }

    public void LoadHints(string filePath = "")
    {
        string path = string.IsNullOrEmpty(filePath) ? _hintsFile : filePath;

        if (!File.Exists(path))
        {
            CreateDefaultHints();
            return;
        }

        try
        {
            _hints.Clear();
            string[] lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                {
                    _hints.Add(trimmed);
                }
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"ヒントデータの読み込みに失敗しました: {ex.Message}", -1, path, AriaErrorLevel.Warning));
            CreateDefaultHints();
        }
    }

    public string GetRandomHint()
    {
        if (_hints.Count == 0)
        {
            return "";
        }
        return _hints[_random.Next(_hints.Count)];
    }

    public string GetContextualHint(GameScene scene, string context = "")
    {
        // シーンに応じたヒントを返す
        switch (scene)
        {
            case GameScene.TitleScreen:
                return "「はじめる」を選択して物語を始めましょう";
            case GameScene.ChapterSelect:
                return "プレイしたいチャプターンを選択してください";
            case GameScene.GamePlay:
                if (!string.IsNullOrEmpty(context))
                {
                    return context;
                }
                return "右クリックでシステムメニューを開けます";
            case GameScene.SystemMenu:
                return "ゲームを保存・設定変更ができます";
            case GameScene.SaveLoadMenu:
                return "セーブスロットを選択してください";
            case GameScene.Settings:
                return "各種設定を調整できます";
            default:
                return GetRandomHint();
        }
    }

    public void AddHint(string hint)
    {
        _hints.Add(hint);
    }

    private void CreateDefaultHints()
    {
        _hints = new List<string>
        {
            "右クリックでシステムメニューを開けます",
            "スペースキーまたはエンターキーでテキストを進めます",
            "Escキーで設定メニューを開けます",
            "F3キーでデバッグモードを切り替えられます",
            "自動セーブは5分ごとに行われます",
            "セーブデータは10スロットまで保存できます",
            "設定は保存されます",
            "ゲームを中断する場合はセーブを忘れずに"
        };
    }
}