using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AriaEngine.Core;

public class SaveManager
{
    private const int CurrentSaveVersion = 3;
    private const int MaxSaveSlots = 99;
    public const int AutoSaveSlot = 98;
    private static readonly byte[] SaveMagic = Encoding.ASCII.GetBytes("ARIASAVE3");
    private static readonly byte[] SaveMagicV2 = Encoding.ASCII.GetBytes("ARIASAVE2");
    private readonly ErrorReporter _reporter;
    private readonly string _saveDir;

    public SaveManager(ErrorReporter reporter, string saveDir = "saves")
    {
        _reporter = reporter;
        _saveDir = string.IsNullOrWhiteSpace(saveDir) ? "saves" : saveDir;
        Directory.CreateDirectory(_saveDir);
    }

    private string GetSavePath(int slot) => Path.Combine(_saveDir, $"slot_{slot:00}.ariasav");
    private string GetJsonSavePath(int slot) => Path.Combine(_saveDir, $"slot_{slot:00}.json");
    private string GetLegacySavePath(int slot) => $"save_data_{slot}.json";
    public string GetThumbnailPath(int slot) => Path.Combine(_saveDir, $"slot_{slot:00}.png");

    public void Save(int slot, GameState state, string currentScriptFile, byte[]? screenshotData = null)
    {
        try
        {
            Directory.CreateDirectory(_saveDir);
            TimeSpan playTime = DateTime.Now - state.SaveRuntime.SessionStartTime + state.SaveRuntime.TotalPlayTime;
            var originalRegisters = state.RegisterState.Registers;
            state.RegisterState.Registers = state.RegisterState.Registers
                .Where(pair => RegisterStoragePolicy.IsSaveStored(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            try
            {
                var file = new SaveFile
                {
                    Format = "AriaSave",
                    Version = CurrentSaveVersion,
                    Meta = new SaveMeta
                    {
                        SlotId = slot,
                        ScriptFile = currentScriptFile,
                        SaveTime = DateTime.Now,
                        ChapterTitle = state.SaveRuntime.CurrentChapter,
                        PreviewText = state.TextRuntime.CurrentTextBuffer.Length > 0 ? state.TextRuntime.CurrentTextBuffer[^Math.Min(80, state.TextRuntime.CurrentTextBuffer.Length)..] : "",
                        PlayTimeSeconds = (long)playTime.TotalSeconds,
                        ThumbnailPath = screenshotData != null ? GetThumbnailPath(slot) : ""
                    },
                    Runtime = state
                };

                WritePackedSave(GetSavePath(slot), file);

                if (screenshotData != null)
                {
                    File.WriteAllBytes(GetThumbnailPath(slot), screenshotData);
                }
            }
            finally
            {
                state.RegisterState.Registers = originalRegisters;
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"セーブ失敗: {ex.Message}", -1, GetSavePath(slot), AriaErrorLevel.Error));
        }
    }

    public (SaveData? Data, bool Success) Load(int slot)
    {
        string path = File.Exists(GetSavePath(slot)) ? GetSavePath(slot) :
            File.Exists(GetJsonSavePath(slot)) ? GetJsonSavePath(slot) :
            GetLegacySavePath(slot);
        if (!File.Exists(path)) return (null, false);

        try
        {
            string json = File.ReadAllText(path);
            if (Path.GetExtension(path).Equals(".ariasav", StringComparison.OrdinalIgnoreCase))
            {
                var packed = ReadPackedSave(path);
                return (SaveData.FromSaveFile(packed), true);
            }

            if (Path.GetFileName(path).StartsWith("slot_", StringComparison.OrdinalIgnoreCase))
            {
                var file = JsonSerializer.Deserialize<SaveFile>(json, CreateJsonOptions());
                if (file?.Runtime == null) return (null, false);
                return (SaveData.FromSaveFile(file), true);
            }

            var legacy = JsonSerializer.Deserialize<SaveData>(json, CreateJsonOptions());
            return (legacy, legacy != null);
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"ロード失敗: {ex.Message}", -1, path, AriaErrorLevel.Error));
            return (null, false);
        }
    }

    public List<SaveData> GetAllSaveSlots()
    {
        var saves = new List<SaveData>();
        for (int i = 0; i < 10; i++)
        {
            var (data, success) = Load(i);
            saves.Add(success && data != null ? data : new SaveData { SlotId = i });
        }
        return saves;
    }

    public SaveData? GetSaveData(int slot)
    {
        var (data, success) = Load(slot);
        if (success && data != null && string.IsNullOrEmpty(data.ScreenshotPath) && File.Exists(GetThumbnailPath(slot)))
        {
            data.ScreenshotPath = GetThumbnailPath(slot);
        }
        return success ? data : null;
    }

    public string GetSaveFilePath(int slot) => GetSavePath(slot);

    public int MigrateAll(bool backup = true)
    {
        Directory.CreateDirectory(_saveDir);
        int migrated = 0;
        string backupDir = Path.Combine(_saveDir, "migration-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        for (int slot = 0; slot < MaxSaveSlots; slot++)
        {
            string path = File.Exists(GetSavePath(slot)) ? GetSavePath(slot) :
                File.Exists(GetJsonSavePath(slot)) ? GetJsonSavePath(slot) :
                GetLegacySavePath(slot);
            if (!File.Exists(path)) continue;

            try
            {
                var (data, success) = Load(slot);
                if (!success || data == null) continue;
                if (backup)
                {
                    Directory.CreateDirectory(backupDir);
                    File.Copy(path, Path.Combine(backupDir, Path.GetFileName(path)), overwrite: true);
                    string thumb = GetThumbnailPath(slot);
                    if (File.Exists(thumb)) File.Copy(thumb, Path.Combine(backupDir, Path.GetFileName(thumb)), overwrite: true);
                }

                Save(slot, data.State, data.ScriptFile, null);
                migrated++;
            }
            catch (Exception ex)
            {
                _reporter.ReportException("SAVE_MIGRATE", ex, $"セーブスロット{slot}のmigrationに失敗しました。", AriaErrorLevel.Error, path);
            }
        }

        return migrated;
    }

    public bool HasSaveData(int slot)
    {
        return File.Exists(GetSavePath(slot)) || File.Exists(GetLegacySavePath(slot));
    }

    public void DeleteSave(int slot)
    {
        try
        {
            foreach (var path in new[] { GetSavePath(slot), GetJsonSavePath(slot), GetLegacySavePath(slot), GetThumbnailPath(slot) })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _reporter.Report(new AriaError($"セーブ削除失敗: {ex.Message}", -1, GetSavePath(slot), AriaErrorLevel.Warning));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }

    private static void WritePackedSave(string path, SaveFile file)
    {
        byte[] plainJson = JsonSerializer.SerializeToUtf8Bytes(file, CreateJsonOptions());
        byte[] compressed = Compress(plainJson);
        using var aes = Aes.Create();
        aes.Key = DeriveSaveKey();
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        byte[] cipher = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(SaveMagic);
        writer.Write(CurrentSaveVersion);
        writer.Write(aes.IV.Length);
        writer.Write(aes.IV);
        writer.Write(cipher.Length);
        writer.Write(cipher);
    }

    private static SaveFile ReadPackedSave(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        byte[] magic = reader.ReadBytes(SaveMagic.Length);
        int version;
        byte[] key;

        if (magic.SequenceEqual(SaveMagic))
        {
            version = reader.ReadInt32();
            key = DeriveSaveKey();
        }
        else if (magic.SequenceEqual(SaveMagicV2))
        {
            version = reader.ReadInt32();
            key = DeriveSaveKeyV2();
        }
        else
        {
            throw new InvalidDataException($"Invalid Aria save header. Expected {Encoding.ASCII.GetString(SaveMagic)} or {Encoding.ASCII.GetString(SaveMagicV2)}.");
        }

        byte[] iv = reader.ReadBytes(reader.ReadInt32());
        byte[] cipher = reader.ReadBytes(reader.ReadInt32());

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        byte[] compressed = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        byte[] json = Decompress(compressed);
        var file = JsonSerializer.Deserialize<SaveFile>(json, CreateJsonOptions()) ?? throw new InvalidDataException("Broken Aria save payload.");

        if (version <= 2)
        {
            MigrateFromV2(file);
        }

        return file;
    }

    private static void MigrateFromV2(SaveFile file)
    {
        file.Version = CurrentSaveVersion;
        file.Format = "AriaSave";
        if (file.Runtime.Declarations == null)
        {
            file.Runtime.Declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
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

    private static byte[] DeriveSaveKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("AriaEngine.LocalSave.Format.v3"));
    }

    private static byte[] DeriveSaveKeyV2()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("AriaEngine.LocalSave.Format.v2"));
    }
}

public class SaveFile
{
    public string Format { get; set; } = "AriaSave";
    public int Version { get; set; } = 3;
    public SaveMeta Meta { get; set; } = new();
    public GameState Runtime { get; set; } = new();
}

public class SaveMeta
{
    public int SlotId { get; set; }
    public string ScriptFile { get; set; } = "";
    public DateTime SaveTime { get; set; }
    public string ChapterTitle { get; set; } = "";
    public string PreviewText { get; set; } = "";
    public long PlayTimeSeconds { get; set; }
    public string ThumbnailPath { get; set; } = "";
}

public class SaveData
{
    public int SlotId { get; set; }
    public string ScriptFile { get; set; } = "";
    public GameState State { get; set; } = new();
    public DateTime SaveTime { get; set; }
    public string ScreenshotPath { get; set; } = "";
    public string ChapterTitle { get; set; } = "";
    public string PreviewText { get; set; } = "";
    public TimeSpan PlayTime { get; set; } = TimeSpan.Zero;
    public int ThumbnailWidth { get; set; } = 320;
    public int ThumbnailHeight { get; set; } = 180;
    public byte[] ScreenshotData { get; set; } = Array.Empty<byte>();
    public Dictionary<string, string> Declarations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static SaveData FromSaveFile(SaveFile file)
    {
        return new SaveData
        {
            SlotId = file.Meta.SlotId,
            ScriptFile = file.Meta.ScriptFile,
            State = file.Runtime,
            SaveTime = file.Meta.SaveTime,
            ChapterTitle = file.Meta.ChapterTitle,
            PreviewText = file.Meta.PreviewText,
            PlayTime = TimeSpan.FromSeconds(file.Meta.PlayTimeSeconds),
            ScreenshotPath = file.Meta.ThumbnailPath,
            Declarations = file.Runtime.Declarations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
