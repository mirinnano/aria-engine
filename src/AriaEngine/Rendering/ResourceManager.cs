using Raylib_cs;
using AriaEngine.Core;
using AriaEngine.Assets;
using System.Collections.Generic;

namespace AriaEngine.Rendering;

/// <summary>
/// Raylibリソース（テクスチャ、音楽、サウンド）のロードと管理を一元化するクラス
/// </summary>
public class ResourceManager
{
    private readonly ErrorReporter? _reporter;
    private readonly IAssetProvider _assetProvider;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Music> _music = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sound> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedMusic = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedSounds = new(StringComparer.OrdinalIgnoreCase);

    public int TextureCount => _textures.Count;
    public int MusicCount => _music.Count;
    public int SoundCount => _sounds.Count;

    public ResourceManager(IAssetProvider assetProvider, ErrorReporter? reporter = null)
    {
        _assetProvider = assetProvider;
        _reporter = reporter;
    }

    /// <summary>
    /// テクスチャをロードする（キャッシュから取得または新規ロード）
    /// </summary>
    public Texture2D LoadTexture(string path, Raylib_cs.TextureFilter filter = Raylib_cs.TextureFilter.Bilinear)
    {
        if (string.IsNullOrEmpty(path))
        {
            _reporter?.Report(new AriaError(
                "テクスチャパスが空です。",
                level: AriaErrorLevel.Warning,
                code: "RESOURCE_TEXTURE_PATH_EMPTY"));
            return default;
        }

        if (_failedTextures.Contains(path))
        {
            return default;
        }

        // キャッシュをチェック
        if (_textures.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // 新規ロード
        try
        {
            string resolvedPath = _assetProvider.MaterializeToFile(path);
            var texture = Raylib.LoadTexture(resolvedPath);

            if (texture.Id != 0)
            {
                Raylib.SetTextureFilter(texture, filter);
                _textures[path] = texture;
                return texture;
            }
            else
            {
                _failedTextures.Add(path);
                _reporter?.Report(new AriaError(
                    $"テクスチャ '{path}' のロード結果が空でした。",
                    level: AriaErrorLevel.Warning,
                    code: "RESOURCE_TEXTURE_EMPTY",
                    hint: "画像ファイルの破損、未対応形式、Pak内の内容を確認してください。"));
                return default;
            }
        }
        catch (Exception ex)
        {
            _failedTextures.Add(path);
            _reporter?.ReportException(
                "RESOURCE_TEXTURE_LOAD",
                ex,
                $"テクスチャ '{path}' の読み込みに失敗しました。",
                AriaErrorLevel.Warning,
                hint: "画像ファイル名、Pak収録名、大文字小文字、拡張子を確認してください。");
            return default;
        }
    }

    /// <summary>
    /// 音楽をロードする（キャッシュから取得または新規ロード）
    /// </summary>
    public Music LoadMusic(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            _reporter?.Report(new AriaError(
                "音楽パスが空です。",
                level: AriaErrorLevel.Warning,
                code: "RESOURCE_MUSIC_PATH_EMPTY"));
            return default;
        }

        if (_failedMusic.Contains(path))
        {
            return default;
        }

        // キャッシュをチェック
        if (_music.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // 新規ロード
        try
        {
            string resolvedPath = _assetProvider.MaterializeToFile(path);
            var music = Raylib.LoadMusicStream(resolvedPath);
            _music[path] = music;
            return music;
        }
        catch (Exception ex)
        {
            _failedMusic.Add(path);
            _reporter?.ReportException(
                "RESOURCE_MUSIC_LOAD",
                ex,
                $"音楽 '{path}' の読み込みに失敗しました。",
                AriaErrorLevel.Warning,
                hint: "音声ファイル名、Pak収録名、対応形式を確認してください。");
            return default;
        }
    }

    /// <summary>
    /// サウンドをロードする（キャッシュから取得または新規ロード）
    /// </summary>
    public Sound LoadSound(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            _reporter?.Report(new AriaError(
                "サウンドパスが空です。",
                level: AriaErrorLevel.Warning,
                code: "RESOURCE_SOUND_PATH_EMPTY"));
            return default;
        }

        if (_failedSounds.Contains(path))
        {
            return default;
        }

        // キャッシュをチェック
        if (_sounds.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // 新規ロード
        try
        {
            string resolvedPath = _assetProvider.MaterializeToFile(path);
            var sound = Raylib.LoadSound(resolvedPath);
            _sounds[path] = sound;
            return sound;
        }
        catch (Exception ex)
        {
            _failedSounds.Add(path);
            _reporter?.ReportException(
                "RESOURCE_SOUND_LOAD",
                ex,
                $"サウンド '{path}' の読み込みに失敗しました。",
                AriaErrorLevel.Warning,
                hint: "音声ファイル名、Pak収録名、対応形式を確認してください。");
            return default;
        }
    }

    /// <summary>
    /// すべてのリソースをアンロードする
    /// </summary>
    public void UnloadAll()
    {
        // テクスチャをアンロード
        foreach (var texture in _textures.Values)
        {
            try
            {
                if (texture.Id != 0)
                {
                    Raylib.UnloadTexture(texture);
                }
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_TEXTURE_UNLOAD",
                    ex,
                    "テクスチャのアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }

        // 音楽をアンロード
        foreach (var music in _music.Values)
        {
            try
            {
                Raylib.StopMusicStream(music);
                Raylib.UnloadMusicStream(music);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_MUSIC_UNLOAD",
                    ex,
                    "音楽のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }

        // サウンドをアンロード
        foreach (var sound in _sounds.Values)
        {
            try
            {
                Raylib.UnloadSound(sound);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_SOUND_UNLOAD",
                    ex,
                    "サウンドのアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
        }

        _textures.Clear();
        _music.Clear();
        _sounds.Clear();
        _failedTextures.Clear();
        _failedMusic.Clear();
        _failedSounds.Clear();
    }

    /// <summary>
    /// テクスチャをアンロードする
    /// </summary>
    public void UnloadTexture(string path)
    {
        if (_textures.TryGetValue(path, out var texture))
        {
            try
            {
                if (texture.Id != 0)
                {
                    Raylib.UnloadTexture(texture);
                }
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_TEXTURE_UNLOAD",
                    ex,
                    $"テクスチャ '{path}' のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
            _textures.Remove(path);
        }
    }

    /// <summary>
    /// 音楽をアンロードする
    /// </summary>
    public void UnloadMusic(string path)
    {
        if (_music.TryGetValue(path, out var music))
        {
            try
            {
                Raylib.StopMusicStream(music);
                Raylib.UnloadMusicStream(music);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_MUSIC_UNLOAD",
                    ex,
                    $"音楽 '{path}' のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
            _music.Remove(path);
        }
    }

    /// <summary>
    /// サウンドをアンロードする
    /// </summary>
    public void UnloadSound(string path)
    {
        if (_sounds.TryGetValue(path, out var sound))
        {
            try
            {
                Raylib.UnloadSound(sound);
            }
            catch (Exception ex)
            {
                _reporter?.ReportException(
                    "RESOURCE_SOUND_UNLOAD",
                    ex,
                    $"サウンド '{path}' のアンロード中にエラーが発生しました。",
                    AriaErrorLevel.Warning);
            }
            _sounds.Remove(path);
        }
    }

    /// <summary>
    /// テクスチャがロードされているか確認する
    /// </summary>
    public bool HasTexture(string path)
    {
        return _textures.ContainsKey(path);
    }

    /// <summary>
    /// 音楽がロードされているか確認する
    /// </summary>
    public bool HasMusic(string path)
    {
        return _music.ContainsKey(path);
    }

    /// <summary>
    /// サウンドがロードされているか確認する
    /// </summary>
    public bool HasSound(string path)
    {
        return _sounds.ContainsKey(path);
    }

    /// <summary>
    /// テクスチャを取得する（ロードされていない場合はdefaultを返す）
    /// </summary>
    public Texture2D? GetTexture(string path)
    {
        return _textures.TryGetValue(path, out var texture) ? texture : null;
    }

    /// <summary>
    /// 音楽を取得する（ロードされていない場合はdefaultを返す）
    /// </summary>
    public Music? GetMusic(string path)
    {
        return _music.TryGetValue(path, out var music) ? music : null;
    }

    /// <summary>
    /// サウンドを取得する（ロードされていない場合はdefaultを返す）
    /// </summary>
    public Sound? GetSound(string path)
    {
        return _sounds.TryGetValue(path, out var sound) ? sound : null;
    }
}
