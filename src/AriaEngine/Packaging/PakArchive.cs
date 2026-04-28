using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AriaEngine.Packaging;

public static class PakArchive
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARPK1");

    public static void Write(string outputPath, IEnumerable<(string LogicalPath, string Type, byte[] Data)> entries, byte[]? encryptionKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        var manifest = new PakManifest();
        var payloads = new List<byte[]>();
        long offset = 0;

        foreach (var entry in entries)
        {
            bool enc = encryptionKey is not null;
            byte[] original = entry.Data;
            byte[] payload = enc ? CryptoHelper.Encrypt(original, encryptionKey!) : original;

            manifest.Entries.Add(new PakManifestEntry
            {
                Path = NormalizePath(entry.LogicalPath),
                Type = entry.Type,
                Offset = offset,
                Size = payload.Length,
                OriginalSize = original.Length,
                Enc = enc,
                Hash = CryptoHelper.Sha256Hex(original)
            });

            payloads.Add(payload);
            offset += payload.Length;
        }

        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = false });

        using var fs = File.Create(outputPath);
        fs.Write(Magic, 0, Magic.Length);
        fs.Write(BitConverter.GetBytes(manifestBytes.Length), 0, sizeof(int));
        fs.Write(manifestBytes, 0, manifestBytes.Length);
        foreach (byte[] payload in payloads)
            fs.Write(payload, 0, payload.Length);
    }

    public static PakReader Open(string pakPath, byte[]? decryptionKey)
    {
        using var fs = File.OpenRead(pakPath);
        byte[] magic = new byte[Magic.Length];
        fs.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidOperationException($"Invalid pak header. Expected magic 'ARPK1' but got '{Encoding.ASCII.GetString(magic)}'. The file may be corrupted or not a pak archive.");

        byte[] lenBuf = new byte[sizeof(int)];
        fs.ReadExactly(lenBuf);
        int manifestLen = BitConverter.ToInt32(lenBuf);
        if (manifestLen <= 0) throw new InvalidOperationException($"Invalid manifest length ({manifestLen}). The pak file may be corrupted.");

        byte[] manifestBytes = new byte[manifestLen];
        fs.ReadExactly(manifestBytes);
        var manifest = JsonSerializer.Deserialize<PakManifest>(manifestBytes) ?? throw new InvalidOperationException("Failed to parse pak manifest. The file may be corrupted.");

        long dataStart = Magic.Length + sizeof(int) + manifestLen;
        return new PakReader(pakPath, manifest, dataStart, decryptionKey);
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
}

public sealed class PakReader
{
    private readonly string _pakPath;
    private readonly Dictionary<string, PakManifestEntry> _entries;
    private readonly long _dataStart;
    private readonly byte[]? _decryptionKey;

    public PakReader(string pakPath, PakManifest manifest, long dataStart, byte[]? decryptionKey)
    {
        _pakPath = pakPath;
        _dataStart = dataStart;
        _decryptionKey = decryptionKey;
        _entries = new Dictionary<string, PakManifestEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries)
            _entries[PakArchive.NormalizePath(entry.Path)] = entry;
    }

    public bool Contains(string logicalPath) => _entries.ContainsKey(PakArchive.NormalizePath(logicalPath));

    public IReadOnlyCollection<PakManifestEntry> GetAllEntries() => _entries.Values;

    public byte[] ReadAllBytes(string logicalPath, bool verifyHash = true)
    {
        string key = PakArchive.NormalizePath(logicalPath);
        if (!_entries.TryGetValue(key, out var entry))
            throw new FileNotFoundException($"Pak entry not found: {logicalPath}");

        using var fs = File.OpenRead(_pakPath);
        fs.Position = _dataStart + entry.Offset;
        byte[] payload = new byte[entry.Size];
        fs.ReadExactly(payload);

        byte[] plain;
        if (entry.Enc)
        {
            if (_decryptionKey is null)
                throw new InvalidOperationException($"Pak entry is encrypted but no key was provided: {logicalPath}");
            plain = CryptoHelper.Decrypt(payload, _decryptionKey);
        }
        else
        {
            plain = payload;
        }

        if (verifyHash)
        {
            string hash = CryptoHelper.Sha256Hex(plain);
            if (!hash.Equals(entry.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Corruption detected: hash mismatch for '{logicalPath}'. Expected {entry.Hash} but computed {hash}. The pak file may be corrupted or the wrong decryption key was provided.");
        }

        return plain;
    }
}
