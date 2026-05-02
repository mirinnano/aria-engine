using Raylib_cs;
using AriaEngine.Core;
using System.Collections.Generic;
using AriaEngine.Assets;

namespace AriaEngine.Audio;

public class AudioManager
{
    private readonly IAssetProvider _assetProvider;
    private readonly ErrorReporter? _reporter;
    private const int MaxBgmCache = 8;
    private const int MaxSeCache = 16;
    private readonly Dictionary<string, Music> _bgmCache = new();
    private readonly List<string> _bgmOrder = new();
    private readonly Dictionary<string, Sound> _seCache = new();
    private readonly List<string> _seOrder = new();
    private readonly HashSet<string> _failedBgm = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedSe = new(StringComparer.OrdinalIgnoreCase);
    private string _currentBgmName = "";
    private Music? _currentBgm;

    public AudioManager(IAssetProvider assetProvider, ErrorReporter? reporter = null)
    {
        _assetProvider = assetProvider;
        _reporter = reporter;
    }

    public void Update(GameState state)
    {
        if (state.Audio.CurrentBgm != _currentBgmName)
        {
            if (_currentBgm.HasValue)
            {
                try
                {
                    Raylib.StopMusicStream(_currentBgm.Value);
                }
                catch (Exception ex)
                {
                    _reporter?.ReportException(
                        "AUDIO_BGM_STOP",
                        ex,
                        $"BGM '{_currentBgmName}' の停止に失敗しました。",
                        AriaErrorLevel.Warning);
                }
            }

            _currentBgmName = state.Audio.CurrentBgm;

            if (!string.IsNullOrEmpty(_currentBgmName))
            {
                if (_failedBgm.Contains(_currentBgmName))
                {
                    _currentBgm = null;
                }
                else if (!_bgmCache.ContainsKey(_currentBgmName))
                {
                    try
                    {
                        string resolved = _assetProvider.MaterializeToFile(_currentBgmName);
                        var music = Raylib.LoadMusicStream(resolved);
                        if (_bgmCache.Count >= MaxBgmCache && _bgmOrder.Count > 0)
                        {
                            string oldest = _bgmOrder[0];
                            _bgmOrder.RemoveAt(0);
                            if (_bgmCache.Remove(oldest, out var oldMusic))
                                Raylib.UnloadMusicStream(oldMusic);
                        }
                        _bgmCache[_currentBgmName] = music;
                        _bgmOrder.Add(_currentBgmName);
                    }
                    catch (Exception ex)
                    {
                        _failedBgm.Add(_currentBgmName);
                        _reporter?.ReportException(
                            "AUDIO_BGM_LOAD",
                            ex,
                            $"BGM '{_currentBgmName}' を読み込めませんでした。無音で続行します。",
                            AriaErrorLevel.Warning,
                            hint: "音声ファイル名、Pak収録名、対応形式を確認してください。");
                        _currentBgm = null;
                    }
                }

                if (_bgmCache.TryGetValue(_currentBgmName, out var bgm))
                {
                    _currentBgm = bgm;
                    try
                    {
                        Raylib.PlayMusicStream(_currentBgm.Value);
                    }
                    catch (Exception ex)
                    {
                        _reporter?.ReportException(
                            "AUDIO_BGM_PLAY",
                            ex,
                            $"BGM '{_currentBgmName}' の再生に失敗しました。",
                            AriaErrorLevel.Warning);
                        _currentBgm = null;
                    }
                }
            }
            else
            {
                _currentBgm = null;
            }
        }

        if (_currentBgm.HasValue)
        {
            try
            {
                float volume = state.Audio.BgmVolume / 100f;
                if (state.Audio.BgmFadeOutTimerMs > 0f)
                {
                    float frameMs = Raylib.GetFrameTime() * 1000f;
                    state.Audio.BgmFadeOutTimerMs = Math.Max(0f, state.Audio.BgmFadeOutTimerMs - frameMs);
                    float ratio = state.Audio.BgmFadeOutDurationMs <= 0f ? 0f : state.Audio.BgmFadeOutTimerMs / state.Audio.BgmFadeOutDurationMs;
                    volume *= Math.Clamp(ratio, 0f, 1f);
                    if (state.Audio.BgmFadeOutTimerMs <= 0f)
                    {
                        state.Audio.BgmFadeOutDurationMs = 0f;
                        state.Audio.CurrentBgm = "";
                    }
                }

                Raylib.SetMusicVolume(_currentBgm.Value, volume);
                Raylib.UpdateMusicStream(_currentBgm.Value);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "AUDIO_BGM_UPDATE",
                    ex,
                    $"BGM '{_currentBgmName}' の更新に失敗しました。BGMを停止して続行します。",
                    AriaErrorLevel.Warning);
                _currentBgm = null;
            }
        }

        if (state.Audio.PendingSe.Count > 0)
        {
            string[] pendingSe = state.Audio.PendingSe.ToArray();
            state.Audio.PendingSe.Clear();
            foreach (string sePath in pendingSe)
            {
                if (!_seCache.ContainsKey(sePath))
                {
                    if (_failedSe.Contains(sePath)) continue;

                    try
                    {
                        string resolved = _assetProvider.MaterializeToFile(sePath);
                        var seSound = Raylib.LoadSound(resolved);
                        if (_seCache.Count >= MaxSeCache && _seOrder.Count > 0)
                        {
                            string oldest = _seOrder[0];
                            _seOrder.RemoveAt(0);
                            if (_seCache.Remove(oldest, out var oldSound))
                                Raylib.UnloadSound(oldSound);
                        }
                        _seCache[sePath] = seSound;
                        _seOrder.Add(sePath);
                    }
                    catch (Exception ex)
                    {
                        _failedSe.Add(sePath);
                        _reporter?.ReportException(
                            "AUDIO_SE_LOAD",
                            ex,
                            $"効果音 '{sePath}' を読み込めませんでした。無音で続行します。",
                            AriaErrorLevel.Warning,
                            hint: "音声ファイル名、Pak収録名、対応形式を確認してください。");
                        continue;
                    }
                }

                if (_seCache.TryGetValue(sePath, out var sound))
                {
                    try
                    {
                        Raylib.SetSoundVolume(sound, state.Audio.SeVolume / 100f);
                        Raylib.PlaySound(sound);
                    }
                    catch (Exception ex)
                    {
                        _reporter?.ReportException(
                            "AUDIO_SE_PLAY",
                            ex,
                            $"効果音 '{sePath}' の再生に失敗しました。",
                            AriaErrorLevel.Warning);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Plays a voice file directly (used by backlog voice replay).
    /// </summary>
    public void PlayVoice(string path, int volumePercent)
    {
        if (_failedSe.Contains(path)) return;

        if (!_seCache.ContainsKey(path))
        {
            try
            {
                string resolved = _assetProvider.MaterializeToFile(path);
                var seSound = Raylib.LoadSound(resolved);
                if (_seCache.Count >= MaxSeCache && _seOrder.Count > 0)
                {
                    string oldest = _seOrder[0];
                    _seOrder.RemoveAt(0);
                    if (_seCache.Remove(oldest, out var oldSound))
                        Raylib.UnloadSound(oldSound);
                }
                _seCache[path] = seSound;
                _seOrder.Add(path);
            }
            catch (Exception ex)
            {
                _failedSe.Add(path);
                _reporter?.ReportException(
                    "AUDIO_VOICE_LOAD",
                    ex,
                    $"Voice '{path}' を読み込めませんでした。",
                    AriaErrorLevel.Warning);
                return;
            }
        }

        if (_seCache.TryGetValue(path, out var sound))
        {
            try
            {
                Raylib.SetSoundVolume(sound, volumePercent / 100f);
                Raylib.PlaySound(sound);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "AUDIO_VOICE_PLAY",
                    ex,
                    $"Voice '{path}' の再生に失敗しました。",
                    AriaErrorLevel.Warning);
            }
        }
    }

    public void Unload()
    {
        foreach (var bgm in _bgmCache.Values)
        {
            try
            {
                Raylib.StopMusicStream(bgm);
                Raylib.UnloadMusicStream(bgm);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "AUDIO_BGM_UNLOAD",
                    ex,
                    "BGMのアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }

        foreach (var se in _seCache.Values)
        {
            try
            {
                Raylib.UnloadSound(se);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "AUDIO_SE_UNLOAD",
                    ex,
                    "効果音のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }

        _bgmCache.Clear();
        _bgmOrder.Clear();
        _seCache.Clear();
        _seOrder.Clear();
        _failedBgm.Clear();
        _failedSe.Clear();
        _currentBgm = null;
        _currentBgmName = "";
    }
}
