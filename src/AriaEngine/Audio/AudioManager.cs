using Raylib_cs;
using AriaEngine.Core;
using System.Collections.Generic;

namespace AriaEngine.Audio;

public class AudioManager
{
    private Dictionary<string, Music> _bgmCache = new();
    private Dictionary<string, Sound> _seCache = new();
    private string _currentBgmName = "";
    private Music? _currentBgm;

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
                if (!_bgmCache.ContainsKey(_currentBgmName))
                {
                    _bgmCache[_currentBgmName] = Raylib.LoadMusicStream(_currentBgmName);
                }
                _currentBgm = _bgmCache[_currentBgmName];
                Raylib.PlayMusicStream(_currentBgm.Value);
            }
            else
            {
                _currentBgm = null;
            }
        }

        if (_currentBgm.HasValue)
        {
            Raylib.SetMusicVolume(_currentBgm.Value, state.BgmVolume / 100f);
            Raylib.UpdateMusicStream(_currentBgm.Value);
        }

        while (state.PendingSe.Count > 0)
        {
            string sePath = state.PendingSe[0];
            state.PendingSe.RemoveAt(0);

            if (!_seCache.ContainsKey(sePath))
            {
                _seCache[sePath] = Raylib.LoadSound(sePath);
            }
            Raylib.SetSoundVolume(_seCache[sePath], state.SeVolume / 100f);
            Raylib.PlaySound(_seCache[sePath]);
        }
    }

    public void Unload()
    {
        foreach (var bgm in _bgmCache.Values) Raylib.UnloadMusicStream(bgm);
        foreach (var se in _seCache.Values) Raylib.UnloadSound(se);
        _bgmCache.Clear();
        _seCache.Clear();
    }
}
