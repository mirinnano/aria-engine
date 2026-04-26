using System;
using System.Collections.Generic;

namespace AriaEngine.Core;

public class GameFlowManager
{
    private GameScene _currentScene = GameScene.TitleScreen;
    private Stack<GameScene> _sceneHistory = new();
    private Dictionary<GameScene, string> _sceneLabels = new();

    public GameScene CurrentScene => _currentScene;

    public void Initialize()
    {
        // 各シーンのデフォルトラベルを設定
        SetSceneLabel(GameScene.TitleScreen, "*title_start");
        SetSceneLabel(GameScene.ChapterSelect, "*chapter_select");
        SetSceneLabel(GameScene.GamePlay, "*start_game");
        SetSceneLabel(GameScene.SystemMenu, "*system_menu");
        SetSceneLabel(GameScene.SaveLoadMenu, "*save_menu");
        SetSceneLabel(GameScene.Settings, "*settings_menu");
        SetSceneLabel(GameScene.Gallery, "*gallery_menu");
    }

    public void TransitionTo(GameScene scene, VirtualMachine vm)
    {
        if (_currentScene != scene)
        {
            _sceneHistory.Push(_currentScene);
            _currentScene = scene;
            vm.State.CurrentScene = scene;

            // シーン遷移用ラベルにジャンプ
            if (_sceneLabels.TryGetValue(scene, out string? label))
            {
                vm.JumpTo(label);
            }
        }
    }

    public void GoBack(VirtualMachine vm)
    {
        if (_sceneHistory.Count > 0)
        {
            _currentScene = _sceneHistory.Pop();
            vm.State.CurrentScene = _currentScene;

            if (_sceneLabels.TryGetValue(_currentScene, out string? label))
            {
                vm.JumpTo(label);
            }
        }
    }

    public void SetSceneLabel(GameScene scene, string label)
    {
        _sceneLabels[scene] = label;
    }

    public string? GetSceneLabel(GameScene scene)
    {
        return _sceneLabels.TryGetValue(scene, out string? label) ? label : null;
    }

    public void ClearHistory()
    {
        _sceneHistory.Clear();
    }

    public int GetHistoryDepth()
    {
        return _sceneHistory.Count;
    }
}
