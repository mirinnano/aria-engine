using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AriaEngine.Packaging;

public static class PakPatch
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARDP1");

    public static void Create(string basePakPath, string newPakPath, string outputPath, byte[]? decryptionKey)
    {
        PakReader baseReader = PakArchive.Open(basePakPath, decryptionKey);
        PakReader newReader = PakArchive.Open(newPakPath, decryptionKey);

        var baseEntries = baseReader.GetAllEntries().ToDictionary(e => PakArchive.NormalizePath(e.Path), StringComparer.OrdinalIgnoreCase);
        var newEntries = newReader.GetAllEntries().ToDictionary(e => PakArchive.NormalizePath(e.Path), StringComparer.OrdinalIgnoreCase);

        var added = new List<PakPatchEntry>();
        var replaced = new List<PakPatchEntry>();
        var removed = new List<string>();

        foreach (var kv in newEntries)
        {
            if (!baseEntries.TryGetValue(kv.Key, out var baseEntry))
            {
                added.Add(new PakPatchEntry { Path = kv.Value.Path, Type = kv.Value.Type, Size = kv.Value.Size });
            }
            else if (!string.Equals(baseEntry.Hash, kv.Value.Hash, StringComparison.OrdinalIgnoreCase))
            {
                replaced.Add(new PakPatchEntry { Path = kv.Value.Path, Type = kv.Value.Type, Size = kv.Value.Size });
            }
        }

        foreach (var kv in baseEntries)
        {
            if (!newEntries.ContainsKey(kv.Key))
                removed.Add(kv.Value.Path);
        }

        var manifest = new PakPatchManifest
        {
            Added = added,
            Replaced = replaced,
            Removed = removed
        };

        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = false });

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
        using var fs = File.Create(outputPath);
        fs.Write(Magic, 0, Magic.Length);
        fs.Write(BitConverter.GetBytes(manifestBytes.Length), 0, sizeof(int));
        fs.Write(manifestBytes, 0, manifestBytes.Length);

        foreach (var entry in added)
        {
            byte[] data = newReader.ReadAllBytes(entry.Path, verifyHash: true);
            fs.Write(data, 0, data.Length);
        }

        foreach (var entry in replaced)
        {
            byte[] data = newReader.ReadAllBytes(entry.Path, verifyHash: true);
            fs.Write(data, 0, data.Length);
        }
    }

    public static void Apply(string basePakPath, string patchPath, string outputPath, byte[]? decryptionKey)
    {
        PakReader baseReader = PakArchive.Open(basePakPath, decryptionKey);

        using var fs = File.OpenRead(patchPath);
        byte[] magic = new byte[Magic.Length];
        fs.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidOperationException("Invalid patch header. Expected ARDP1.");

        byte[] lenBuf = new byte[sizeof(int)];
        fs.ReadExactly(lenBuf);
        int manifestLen = BitConverter.ToInt32(lenBuf);
        if (manifestLen <= 0) throw new InvalidOperationException("Invalid patch manifest length.");

        byte[] manifestBytes = new byte[manifestLen];
        fs.ReadExactly(manifestBytes);
        var manifest = JsonSerializer.Deserialize<PakPatchManifest>(manifestBytes) ?? throw new InvalidOperationException("Patch manifest parse failed.");

        var entries = new List<(string LogicalPath, string Type, byte[] Data)>();
        var existing = baseReader.GetAllEntries().ToDictionary(e => PakArchive.NormalizePath(e.Path), StringComparer.OrdinalIgnoreCase);

        long dataStart = Magic.Length + sizeof(int) + manifestLen;

        foreach (var entry in manifest.Added)
        {
            fs.Position = dataStart;
            byte[] data = ReadFromPatch(fs, entry.Size);
            entries.Add((entry.Path, entry.Type, data));
            dataStart += entry.Size;
        }

        foreach (var entry in manifest.Replaced)
        {
            fs.Position = dataStart;
            byte[] data = ReadFromPatch(fs, entry.Size);
            existing[ PakArchive.NormalizePath(entry.Path) ] = new PakManifestEntry
            {
                Path = entry.Path,
                Type = entry.Type,
                Size = data.Length,
                OriginalSize = data.Length,
                Enc = false,
                Hash = CryptoHelper.Sha256Hex(data)
            };
            entries.Add((entry.Path, entry.Type, data));
            dataStart += entry.Size;
        }

        foreach (var entry in manifest.Removed)
        {
            existing.Remove(PakArchive.NormalizePath(entry));
        }

        foreach (var kv in existing)
        {
            if (!manifest.Added.Any(a => PakArchive.NormalizePath(a.Path).Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                && !manifest.Replaced.Any(r => PakArchive.NormalizePath(r.Path).Equals(kv.Key, StringComparison.OrdinalIgnoreCase)))
            {
                byte[] data = baseReader.ReadAllBytes(kv.Value.Path, verifyHash: true);
                entries.Add((kv.Value.Path, kv.Value.Type, data));
            }
        }

        PakArchive.Write(outputPath, entries, encryptionKey: null);
    }

    private static byte[] ReadFromPatch(Stream fs, int size)
    {
        byte[] buffer = new byte[size];
        int read = 0;
        while (read < size)
        {
            int chunk = fs.Read(buffer, read, size - read);
            if (chunk == 0)
                throw new InvalidOperationException("Unexpected end of patch file.");
            read += chunk;
        }
        return buffer;
    }
}

public sealed class PakPatchManifest
{
    public string Version { get; set; } = "1";
    public List<PakPatchEntry> Added { get; set; } = new();
    public List<PakPatchEntry> Replaced { get; set; } = new();
    public List<string> Removed { get; set; } = new();
}

public sealed class PakPatchEntry
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "binary";
    public int Size { get; set; }
}
