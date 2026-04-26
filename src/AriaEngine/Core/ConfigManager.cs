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
    public int GlobalTextSpeedMs { get; set; } = 30;
    public int DefaultTextSpeedMs { get; set; } = 30; // engine default
    public int BgmVolume { get; set; } = 100;
    public int SeVolume { get; set; } = 100;
    public bool IsFullscreen { get; set; } = false;
    public string TextMode { get; set; } = "adv"; // "adv" or "nvl"
    public bool SkipUnread { get; set; } = false;
}

public class PersistentGameData
{
    public Dictionary<string, int> Registers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> SaveFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Counters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ReadKeys { get; set; } = new();
    public bool SkipUnread { get; set; }
}

public class ConfigManager
{
    private string _configPath = "config.json";
    private string _persistentPath = Path.Combine("saves", "persistent.ariasav");
    private static readonly byte[] PersistentMagic = Encoding.ASCII.GetBytes("ARIAPERSIST2");
    private readonly ErrorReporter? _reporter;
    public AppConfig Config { get; private set; } = new();

    public ConfigManager(ErrorReporter? reporter = null)
    {
        _reporter = reporter;
    }

    public void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
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
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Config, options);
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
            return JsonSerializer.Deserialize<PersistentGameData>(json) ?? new PersistentGameData();
        }
        catch (Exception ex)
        {
            ReportConfigException("PERSISTENT_LOAD", ex, "永続データの読み込みに失敗しました。新規データで続行します。");
            return new PersistentGameData();
        }
    }

    public void SavePersistentGameData(PersistentGameData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_persistentPath) ?? ".");
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = false });
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
