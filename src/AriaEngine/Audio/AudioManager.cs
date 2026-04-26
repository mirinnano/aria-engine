using Raylib_cs;
using AriaEngine.Core;
using System.Collections.Generic;
using AriaEngine.Assets;

namespace AriaEngine.Audio;

public class AudioManager
{
    private readonly IAssetProvider _assetProvider;
    private readonly ErrorReporter? _reporter;
    private Dictionary<string, Music> _bgmCache = new();
    private Dictionary<string, Sound> _seCache = new();
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
        if (state.CurrentBgm != _currentBgmName)
        {
            if (_currentBgm.HasValue)
            {
                Raylib.StopMusicStream(_currentBgm.Value);
            }

            _currentBgmName = state.CurrentBgm;

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
                        _bgmCache[_currentBgmName] = Raylib.LoadMusicStream(resolved);
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
                    Raylib.PlayMusicStream(_currentBgm.Value);
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
                float volume = state.BgmVolume / 100f;
                if (state.BgmFadeOutTimerMs > 0f)
                {
                    float frameMs = Raylib.GetFrameTime() * 1000f;
                    state.BgmFadeOutTimerMs = Math.Max(0f, state.BgmFadeOutTimerMs - frameMs);
                    float ratio = state.BgmFadeOutDurationMs <= 0f ? 0f : state.BgmFadeOutTimerMs / state.BgmFadeOutDurationMs;
                    volume *= Math.Clamp(ratio, 0f, 1f);
                    if (state.BgmFadeOutTimerMs <= 0f)
                    {
                        state.BgmFadeOutDurationMs = 0f;
                        state.CurrentBgm = "";
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

        if (state.PendingSe.Count > 0)
        {
            string[] pendingSe = state.PendingSe.ToArray();
            state.PendingSe.Clear();
            foreach (string sePath in pendingSe)
            {
                if (!_seCache.ContainsKey(sePath))
                {
                    if (_failedSe.Contains(sePath)) continue;

                    try
                    {
                        string resolved = _assetProvider.MaterializeToFile(sePath);
                        _seCache[sePath] = Raylib.LoadSound(resolved);
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
                Raylib.SetSoundVolume(_seCache[sePath], state.SeVolume / 100f);
                Raylib.PlaySound(_seCache[sePath]);
            }
        }
    }

    public void Unload()
    {
        foreach (var bgm in _bgmCache.Values) Raylib.UnloadMusicStream(bgm);
        foreach (var se in _seCache.Values) Raylib.UnloadSound(se);
        _bgmCache.Clear();
        _seCache.Clear();
        _failedBgm.Clear();
        _failedSe.Clear();
    }
}
