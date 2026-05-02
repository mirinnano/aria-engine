using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AriaEngine.Assets;
using AriaEngine.Core;
using AriaEngine.Rendering;
using AriaEngine.Scripting;

namespace AriaEngine;

/// <summary>
/// .ariaスクリプトおよび画像アセットのライブリロードを管理するクラス。
/// FileSystemWatcherでファイル変更を監視し、次のフレームで反映する。
/// </summary>
public sealed class LiveReloadManager : IDisposable
{
    private readonly VirtualMachine _vm;
    private readonly ScriptLoader _scriptLoader;
    private readonly ErrorReporter _reporter;
    private readonly SpriteRenderer? _renderer;
    private readonly string _basePath;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentQueue<string> _pendingChanges = new();
    private readonly HashSet<string> _pendingSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingLock = new();

    public bool Enabled { get; set; } = true;
    public int PendingCount => _pendingChanges.Count;

    public LiveReloadManager(
        VirtualMachine vm,
        ScriptLoader scriptLoader,
        ErrorReporter reporter,
        SpriteRenderer? renderer,
        string basePath)
    {
        _vm = vm;
        _scriptLoader = scriptLoader;
        _reporter = reporter;
        _renderer = renderer;
        _basePath = basePath;

        _watcher = new FileSystemWatcher(basePath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!Enabled) return;
        string ext = Path.GetExtension(e.FullPath);
        if (!IsWatchedExtension(ext)) return;
        EnqueueChange(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (!Enabled) return;
        string ext = Path.GetExtension(e.FullPath);
        if (!IsWatchedExtension(ext)) return;
        EnqueueChange(e.FullPath);
    }

    private static bool IsWatchedExtension(string ext)
    {
        return ext.Equals(".aria", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    internal void EnqueueChange(string path)
    {
        lock (_pendingLock)
        {
            if (_pendingSet.Add(path))
            {
                _pendingChanges.Enqueue(path);
            }
        }
    }

    /// <summary>
    /// フレームの先頭で呼び出し、保留中の変更を処理する。
    /// </summary>
    public void Update()
    {
        if (!Enabled) return;

        while (_pendingChanges.TryDequeue(out string? fullPath))
        {
            lock (_pendingLock)
            {
                _pendingSet.Remove(fullPath);
            }

            string ext = Path.GetExtension(fullPath);
            string relativePath = GetRelativePath(fullPath);

            if (ext.Equals(".aria", StringComparison.OrdinalIgnoreCase))
            {
                HandleScriptChange(relativePath);
            }
            else
            {
                HandleImageChange(relativePath);
            }
        }
    }

    private string GetRelativePath(string fullPath)
    {
        if (fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            string rel = fullPath.Substring(_basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
        return fullPath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private void HandleScriptChange(string path)
    {
        // 現在読み込まれているスクリプトのみリロード
        if (!string.Equals(path, _vm.CurrentScriptFile, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 現在のラベル位置を記録
        if (!_vm.TryGetCurrentLabelAndOffset(out string labelName, out int offset))
        {
            _reporter.Report(new AriaError(
                "ライブリロード: 現在のラベルが特定できなかったため、スクリプト先頭から再開します。",
                level: AriaErrorLevel.Warning,
                code: "LIVE_RELOAD_NO_LABEL"));
            labelName = "";
            offset = 0;
        }

        try
        {
            var result = _scriptLoader.LoadScript(path);
            _vm.ClearIncludedFiles();
            _vm.LoadScript(result, path);

            // テキスト表示状態をリセット
            _vm.State.TextRuntime.CurrentTextBuffer = "";
            _vm.State.TextRuntime.DisplayedTextLength = 0;
            _vm.State.TextRuntime.TextTimerMs = 0f;
            _vm.State.TextRuntime.CurrentTextSegments = null;
            _vm.State.TextRuntime.IsWaitingPageClear = false;

            // ラベル位置を復元
            if (!string.IsNullOrEmpty(labelName) && _vm.Labels.TryGetValue(labelName, out int newPc))
            {
                _vm.State.Execution.ProgramCounter = newPc + offset;
                if (_vm.Instructions.Count > 0)
                {
                    if (_vm.State.Execution.ProgramCounter >= _vm.Instructions.Count)
                        _vm.State.Execution.ProgramCounter = _vm.Instructions.Count - 1;
                }
                else
                {
                    _vm.State.Execution.ProgramCounter = 0;
                }
                if (_vm.State.Execution.ProgramCounter < 0)
                    _vm.State.Execution.ProgramCounter = 0;
            }
            else
            {
                _vm.State.Execution.ProgramCounter = 0;
            }

            _vm.State.Execution.State = VmState.Running;

            _reporter.Report(new AriaError(
                $"ライブリロード完了: '{path}' (ラベル *{labelName}, オフセット {offset})",
                level: AriaErrorLevel.Info,
                code: "LIVE_RELOAD_OK"));
        }
        catch (Exception ex)
        {
            _reporter.ReportException(
                "LIVE_RELOAD_SCRIPT",
                ex,
                $"スクリプト '{path}' のライブリロードに失敗しました。前の状態を維持します。",
                AriaErrorLevel.Error);
        }
    }

    private void HandleImageChange(string path)
    {
        _renderer?.InvalidateTexture(path);
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Dispose();
    }
}
