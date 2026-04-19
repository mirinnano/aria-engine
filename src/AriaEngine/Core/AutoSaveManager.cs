using System;
using System.Collections.Generic;
using AutoSaveTimer = System.Timers.Timer;

namespace AriaEngine.Core;

public class AutoSaveManager
{
    private AutoSaveTimer? _autoSaveTimer;
    private VirtualMachine _vm;
    private SaveManager _saveManager;
    private int _autoSaveIntervalMinutes = 5;
    private int _maxAutoSaves = 3;
    private int _currentAutoSaveSlot = 100;
    private readonly ErrorReporter _reporter;

    public AutoSaveManager(VirtualMachine vm, SaveManager saveManager, ErrorReporter reporter)
    {
        _vm = vm;
        _saveManager = saveManager;
        _reporter = reporter;
    }

    public void StartAutoSave(int intervalMinutes = 5)
    {
        _autoSaveIntervalMinutes = intervalMinutes;
        _autoSaveTimer = new AutoSaveTimer(_autoSaveIntervalMinutes * 60 * 1000);
        _autoSaveTimer.Elapsed += AutoSaveCallback;
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }

    public void StopAutoSave()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
    }

    public void TriggerAutoSave()
    {
        try
        {
            int slot = GetNextAutoSaveSlot();
            _saveManager.Save(slot, _vm.State, _vm.CurrentScriptFile);
            _currentAutoSaveSlot = slot;

            // 古いオートセーブを削除
            CleanOldAutoSaves();
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"オートセーブ失敗: {ex.Message}", -1, "", AriaErrorLevel.Warning));
        }
    }

    public List<SaveData> GetAutoSaves()
    {
        var saves = new List<SaveData>();
        for (int i = 100; i < 100 + _maxAutoSaves; i++)
        {
            var data = _saveManager.GetSaveData(i);
            if (data != null)
            {
                saves.Add(data);
            }
        }
        return saves;
    }

    private void AutoSaveCallback(object? sender, System.Timers.ElapsedEventArgs e)
    {
        TriggerAutoSave();
    }

    private int GetNextAutoSaveSlot()
    {
        return (_currentAutoSaveSlot + 1) % _maxAutoSaves + 100;
    }

    private void CleanOldAutoSaves()
    {
        // すべてのオートセーブスロットを確認し、古いものを削除
        // この実装では、最大数を超えるものを削除
        for (int i = 100; i < 100 + _maxAutoSaves; i++)
        {
            if (i != _currentAutoSaveSlot && _saveManager.HasSaveData(i))
            {
                // 最新のオートセーブ以外は削除
                // _saveManager.DeleteSave(i);
            }
        }
    }
}