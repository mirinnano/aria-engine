using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AriaEngine.Core;

public class AppConfig
{
    public int SchemaVersion { get; set; } = 2;
    public int GlobalTextSpeedMs { get; set; } = 30;
    public int DefaultTextSpeedMs { get; set; } = 30; // engine default
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
    public bool IsFullscreen { get; set; } = false;
    public string TextMode { get; set; } = "adv"; // "adv" or "nvl"
    public bool SkipUnread { get; set; } = false;
    public string Language { get; set; } = "ja-JP";
    public int AutoModeWaitTimeMs { get; set; } = 2000;
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;

    /// <summary>
    /// Pak v3 redesign, Phase 5.1: configuration for the in-engine asset
    /// garbage collector. Defaults match the design (512 MB budget, 1s
    /// Gen0→Gen1 promotion, 30s Gen1→Gen2). <c>Enabled = false</c> by
    /// default so legacy bundles behave exactly as before until the
    /// staged rollout is fully verified.
    /// </summary>
    public AssetGcConfig AssetGc { get; set; } = new();
}

/// <summary>
/// Knobs for <see cref="AriaEngine.Assets.AssetRegistry"/>. All fields are
/// optional in the JSON file; missing values fall back to the design defaults
/// listed in <c>pak-asset-gc-redesign.md</c>.
/// </summary>
public class AssetGcConfig
{
    /// <summary>Master switch. Off by default for staged rollout (Phase 5.2).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Total memory budget for cached asset bytes (Q1: 512 MB).</summary>
    public long TotalBudgetBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>Seconds idle before Gen0 → Gen1 promotion (default 1s).</summary>
    public int Gen1PromotionSeconds { get; set; } = 1;

    /// <summary>Seconds idle before Gen1 → Gen2 promotion (default 30s). Gen2 is protected from eviction.</summary>
    public int Gen2PromotionSeconds { get; set; } = 30;
}

public class PersistentGameData
{
    public int SchemaVersion { get; set; } = 2;
    public Dictionary<string, int> Registers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> SaveFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Counters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ReadKeys { get; set; } = new();
    public bool SkipUnread { get; set; }
    public List<string> UnlockedCgs { get; set; } = new();
}

public class ConfigManager
{
    private string _configPath = "config.json";
    private string _persistentPath = Path.Combine("saves", "persistent.ariasav");
    private static readonly byte[] PersistentMagic = Encoding.ASCII.GetBytes("ARIAPERSIST2");
    private readonly ErrorReporter? _reporter;
    private readonly bool _usePortableJsonPersistent;
    public AppConfig Config { get; private set; } = new();

    public ConfigManager(ErrorReporter? reporter = null, string configPath = "config.json", string? persistentPath = null, bool? usePortableJsonPersistent = null)
    {
        _reporter = reporter;
        _configPath = configPath;
        _persistentPath = persistentPath ?? Path.Combine("saves", "persistent.ariasav");
        _usePortableJsonPersistent = usePortableJsonPersistent ?? false;
    }

    public void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                Config = JsonSerializer.Deserialize(json, AriaCoreJsonContext.Default.AppConfig) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                ReportConfigException("CONFIG_LOAD", ex, "設定ファイルの読み込みに失敗しました。既定値で続行します。");
            }
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Config, AriaCoreIndentedJsonContext.Default.AppConfig);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            ReportConfigException("CONFIG_SAVE", ex, "設定ファイルの保存に失敗しました。");
        }
    }

    public PersistentGameData LoadPersistentGameData()
    {
        if (!File.Exists(_persistentPath)) return new PersistentGameData();

        try
        {
            if (LooksLikeJsonFile(_persistentPath))
            {
                var jsonData = JsonSerializer.Deserialize(File.ReadAllText(_persistentPath), AriaCoreJsonContext.Default.PersistentGameData) ?? new PersistentGameData();
                MigratePersistentGameData(jsonData);
                return jsonData;
            }

            using var stream = File.OpenRead(_persistentPath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            byte[] magic = reader.ReadBytes(PersistentMagic.Length);
            if (!magic.SequenceEqual(PersistentMagic)) return new PersistentGameData();
            _ = reader.ReadInt32();
            byte[] iv = reader.ReadBytes(reader.ReadInt32());
            byte[] cipher = reader.ReadBytes(reader.ReadInt32());

            using var aes = Aes.Create();
            aes.Key = DerivePersistentKey();
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            byte[] compressed = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            byte[] json = Decompress(compressed);
            var data = JsonSerializer.Deserialize(json, AriaCoreJsonContext.Default.PersistentGameData) ?? new PersistentGameData();
            MigratePersistentGameData(data);
            return data;
        }
        catch (Exception ex)
        {
            ReportConfigException("PERSISTENT_LOAD", ex, "永続データの読み込みに失敗しました。新規データで続行します。");
            return new PersistentGameData();
        }
    }

    private static void MigratePersistentGameData(PersistentGameData data)
    {
        if (data.SchemaVersion <= 0) data.SchemaVersion = 1;
        if (data.SchemaVersion < 2)
        {
            data.SaveFlags ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            data.Counters ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.ReadKeys ??= new List<string>();
            data.UnlockedCgs ??= new List<string>();
            data.SchemaVersion = 2;
        }
    }

    public void SavePersistentGameData(PersistentGameData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_persistentPath) ?? ".");
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(data, AriaCoreJsonContext.Default.PersistentGameData);
            if (_usePortableJsonPersistent)
            {
                File.WriteAllBytes(_persistentPath, json);
                return;
            }

            byte[] compressed = Compress(json);

            using var aes = Aes.Create();
            aes.Key = DerivePersistentKey();
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var encryptor = aes.CreateEncryptor();
            byte[] cipher = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);

            using var stream = File.Create(_persistentPath);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(PersistentMagic);
            writer.Write(2);
            writer.Write(aes.IV.Length);
            writer.Write(aes.IV);
            writer.Write(cipher.Length);
            writer.Write(cipher);
        }
        catch (Exception ex)
        {
            ReportConfigException("PERSISTENT_SAVE", ex, "永続データの保存に失敗しました。");
        }
    }

    private static bool LooksLikeJsonFile(string path)
    {
        using var stream = File.OpenRead(path);
        int value = stream.ReadByte();
        if (value == 0xEF && stream.ReadByte() == 0xBB && stream.ReadByte() == 0xBF)
        {
            value = stream.ReadByte();
        }

        while (value == ' ' || value == '\r' || value == '\n' || value == '\t')
        {
            value = stream.ReadByte();
        }

        return value == '{';
    }

    private void ReportConfigException(string code, Exception ex, string message)
    {
        if (_reporter != null)
        {
            _reporter.ReportException(code, ex, message, AriaErrorLevel.Warning);
            return;
        }

        Console.Error.WriteLine($"{code}: {message} {ex.Message}");
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DerivePersistentKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("AriaEngine.PersistentFlags.Format.v2"));
    }
}
