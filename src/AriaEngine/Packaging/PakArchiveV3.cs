using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Linq;
using System.IO.MemoryMappedFiles;

namespace AriaEngine.Packaging;

// Version 3 Pak Archive (binary manifest)
// NOTE: This implementation focuses on the format described by the task.
// - 32/36-byte header (see below). Implemented with explicit fields for reliability.
// - Binary manifest: EntryTable + PathStringPool.
// - Payloads follow the manifest.
// - Encryption/Compression phases are out of scope for Phase 1.
public static class PakArchiveV3
{
    private static readonly string Magic = "ARIA";

    // Category values (as per requirement comment): boot/scenario/data/stream/voice/update
    public enum Category : byte
    {
        Boot = 0x00,
        Scenario = 0x01,
        Data = 0x02,
        Stream = 0x03,
        Voice = 0x04,
        Update = 0x05
    }

    // Simple, fixed 36-byte header composition (as derived from task description).
    // [Magic: 4bytes] "ARIA"
    // [Version: 1byte] 0x03
    // [Category: 1byte] 0x00-0x05
    // [PakVersion: 1byte] 1-255
    // [Flags: 1byte] b0: encrypted; b1: compressed; b2: chunked
    // [EntryCount: 4bytes]
    // [ManifestOffset: 8bytes]
    // [ManifestSize: 4bytes]
    // [PayloadOffset: 8bytes]
    // [Reserved: 4bytes]

    public static void Write(Stream output, PakManifestV3 manifest, byte[][] files, Category category = Category.Data, byte pakVersion = 1, byte flags = 0x00)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (files == null) throw new ArgumentNullException(nameof(files));
        if (manifest.Entries.Count != files.Length)
            throw new ArgumentException("Entries count must match the number of provided file payloads.");

        // Ensure entries are sorted by PathHash AND PathStrings stays aligned
        // Pair them so sorting entries by PathHash also reorders PathStrings identically
        var paired = manifest.Entries
            .Select((e, idx) => (Entry: e, OriginalIndex: idx))
            .OrderBy(p => p.Entry.PathHash)
            .ToList();

        manifest.Entries = paired.Select(p => p.Entry).ToList();
        manifest.PathStrings = paired.Select(p => manifest.PathStrings[p.OriginalIndex]).ToList();

        // Build manifest binary
        var manifestBytes = BuildManifestBytes(manifest.Entries, manifest.PathStrings);
        // Compute header values
        const int headerSize = 36; // 36 bytes as per layout above
        uint entryCount = (uint)manifest.Entries.Count;
        ulong manifestOffset = headerSize;
        uint manifestSize = (uint)manifestBytes.Length;
        ulong payloadOffset = manifestOffset + manifestSize;

        // Write final header + manifest + payloads to the output stream
        output.Position = 0; // ensure reset
        using var bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        // Header
        bw.Write(Encoding.ASCII.GetBytes(Magic)); // 4
        bw.Write((byte)0x03); // Version 0x03
        bw.Write((byte)category); // Category
        bw.Write((byte)pakVersion); // PakVersion
        bw.Write(flags); // Flags
        bw.Write(entryCount); // EntryCount (4 bytes)
        bw.Write(manifestOffset); // ManifestOffset (8 bytes)
        bw.Write(manifestSize); // ManifestSize (4 bytes)
        bw.Write(payloadOffset); // PayloadOffset (8 bytes)
        bw.Write((uint)0); // Reserved (4 bytes)

        // Manifest
        bw.Write(manifestBytes);

        // Payloads
        for (int i = 0; i < files.Length; i++)
        {
            bw.Write(files[i]);
        }
    }

    // Build a binary manifest consisting of: EntryTable followed by PathStringPool
    // EntryTable layout per entry: PathHash (8) | Offset (8) | Size (4) | OriginalSize (4) | Flags (1)
    // PathStringPool: N strings separated by '\0' (N must equal Entries.Count)
    private static byte[] BuildManifestBytes(List<PakManifestEntryV3> entries, List<string> pathStrings)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // Entry count (4 bytes)
        bw.Write((int)entries.Count);
        // EntryTable
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bw.Write(e.PathHash); // 8
            bw.Write(e.Offset); // 8
            bw.Write(e.Size); // 4
            bw.Write(e.OriginalSize); // 4
            bw.Write(e.Flags); // 1
        }

        // PathStringPool
        // Strings are written in the same order as entries
        for (int i = 0; i < pathStrings.Count; i++)
        {
            // Write as UTF-8 bytes followed by a NULL terminator
            var b = Encoding.UTF8.GetBytes(pathStrings[i]);
            bw.Write(b);
            bw.Write((byte)0); // NULL terminator
        }

        return ms.ToArray();
    }

    // Read and parse a PakManifestV3 from a stream at a given position
    // Returns a tuple: manifest, pathStrings, Entries with PathHash populated
    public static (PakManifestV3 manifest, List<string> pathStrings, List<PakManifestEntryV3> entries) ReadManifest(Stream input, long manifestStart, int manifestLength)
    {
        input.Position = manifestStart;
        using var br = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        int entryCount = br.ReadInt32();
        var entries = new List<PakManifestEntryV3>(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            var e = new PakManifestEntryV3
            {
                PathHash = br.ReadUInt64(),
                Offset = br.ReadUInt64(),
                Size = br.ReadUInt32(),
                OriginalSize = br.ReadUInt32(),
                Flags = br.ReadByte()
            };
            entries.Add(e);
        }

        // PathStrings follow: read until manifestLength exhausted
        // We cannot know exact boundaries; instead, parse by consuming remaining bytes until EOF of manifestLength
        long consumed = 4 + entryCount * (8 + 8 + 4 + 4 + 1); // approximate header portion
        long poolLength = manifestLength - consumed;
        if (poolLength < 0) poolLength = 0;
        // Guard against overflow when casting to int
        if (poolLength > int.MaxValue) throw new InvalidOperationException($"PathStringPool too large: {poolLength} bytes");
        var poolBytes = br.ReadBytes((int)poolLength);
        // Split by 0 terminator
        var pools = new List<string>();
        int start = 0;
        for (int j = 0; j < poolBytes.Length; j++)
        {
            if (poolBytes[j] == 0)
            {
                if (j > start)
                    pools.Add(Encoding.UTF8.GetString(poolBytes, start, j - start));
                else
                    pools.Add(string.Empty);
                start = j + 1;
            }
        }
        // If poolBytes didn't end with 0, append remaining as empty (robustness)
        if (start < poolBytes.Length)
        {
            pools.Add(Encoding.UTF8.GetString(poolBytes, start, poolBytes.Length - start));
        }
        // Ensure pool length matches entries length
        var pathStrings = pools.Count > entries.Count ? pools.GetRange(0, entries.Count) : pools;

        var manifest = new PakManifestV3
        {
            Entries = entries,
            PathStrings = pathStrings
        };
        return (manifest, pathStrings, entries);
    }
}

// Reader wrapper for PakArchiveV3
public sealed class PakArchiveV3Reader : IDisposable
{
    private readonly Stream _stream;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private readonly bool _leaveOpen;
    private long _fileLength;
    public PakManifestV3 Manifest { get; }
    public List<string> PathStrings { get; }
    public List<PakManifestEntryV3> Entries { get; }
    public ulong PayloadOffset { get; }

    public PakArchiveV3Reader(Stream stream, PakManifestV3 manifest, List<string> pathStrings, List<PakManifestEntryV3> entries, ulong payloadOffset, long manifestStart, int manifestLength, long fileLength = 0, bool leaveOpen = false)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        Manifest = manifest;
        PathStrings = pathStrings;
        Entries = entries;
        PayloadOffset = payloadOffset;
        ManifestStart = manifestStart;
        ManifestLength = manifestLength;
        _fileLength = fileLength;
    }

    // Memory-mapped file support (optional, used by Open(string) factory)
    private PakArchiveV3Reader(Stream stream, PakManifestV3 manifest, List<string> pathStrings, List<PakManifestEntryV3> entries, ulong payloadOffset, long manifestStart, int manifestLength, MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, long fileLength)
        : this(stream, manifest, pathStrings, entries, payloadOffset, manifestStart, manifestLength, fileLength)
    {
        _mmf = mmf;
        _accessor = accessor;
    }

    // Convenience Open factory for memory-mapped files
    public static PakArchiveV3Reader Open(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        var fi = new FileInfo(filePath);
        if (!fi.Exists) throw new FileNotFoundException("PakArchive file not found", filePath);
        // Map the entire file for read access
        var length = fi.Length;
        var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, length, MemoryMappedFileAccess.Read);
        // Use a view stream for initial parsing (header + manifest)
        var stream = mmf.CreateViewStream(0, length, MemoryMappedFileAccess.Read);
        // Parse using existing path
        var reader = Read(stream);
        // Attach memory-mapped handles for future ReadAllBytes via mmap path
        var accessor = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
        // Replace internal mmap references on the returned object
        reader._mmf = mmf;
        reader._accessor = accessor;
        reader._fileLength = length;
        return reader;
    }

    public static PakArchiveV3Reader Open(Stream stream, bool leaveOpen = false)
    {
        return Read(stream, leaveOpen);
    }

    // Factory to open and read a PakArchiveV3 from a stream
    public static PakArchiveV3Reader Read(Stream input) => Read(input, leaveOpen: false);

    private static PakArchiveV3Reader Read(Stream input, bool leaveOpen)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        input.Position = 0;
        using var br0 = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var magicBytes = br0.ReadBytes(4);
        if (Encoding.ASCII.GetString(magicBytes) != "ARIA")
            throw new InvalidOperationException("Invalid ARIA pak header. Not a PakArchiveV3 file.");

        // Read header fields (36-byte header). Read in the same order as Write writes them.
        byte version = br0.ReadByte();
        var category = (PakArchiveV3.Category)br0.ReadByte();
        byte pakVersion = br0.ReadByte();
        byte flags = br0.ReadByte();
        int entryCount = br0.ReadInt32();
        ulong manifestOffset = br0.ReadUInt64();
        uint manifestSize = br0.ReadUInt32();
        ulong payloadOffset = br0.ReadUInt64();
        br0.ReadUInt32(); // Reserved, ignored

        // Read manifest bytes
        input.Position = (long)manifestOffset;
        var manifestBytes = br0.ReadBytes((int)manifestSize);
        using var ms = new MemoryStream(manifestBytes);
        using var brManifest = new BinaryReader(ms, Encoding.UTF8);
        int readEntryCount = brManifest.ReadInt32();
        if (readEntryCount != entryCount)
            throw new InvalidOperationException("Manifest entry count mismatch with header.");
        var entries = new List<PakManifestEntryV3>(entryCount);
        for (int i = 0; i < readEntryCount; i++)
        {
            var e = new PakManifestEntryV3
            {
                PathHash = brManifest.ReadUInt64(),
                Offset = brManifest.ReadUInt64(),
                Size = brManifest.ReadUInt32(),
                OriginalSize = brManifest.ReadUInt32(),
                Flags = brManifest.ReadByte()
            };
            entries.Add(e);
        }
        // Remaining bytes in manifest form the PathStringPool; we attempt to split by nulls
        var poolBytes = brManifest.ReadBytes((int)(manifestBytes.Length - (ms.Position - 0)));
        var pathStrings = new List<string>();
        if (poolBytes.Length > 0)
        {
            int start = 0;
            for (int idx = 0; idx < poolBytes.Length; idx++)
            {
                if (poolBytes[idx] == 0)
                {
                    var s = Encoding.UTF8.GetString(poolBytes, start, idx - start);
                    pathStrings.Add(s);
                    start = idx + 1;
                }
            }
            if (start < poolBytes.Length)
            {
                pathStrings.Add(Encoding.UTF8.GetString(poolBytes, start, poolBytes.Length - start));
            }
        }

        var manifest = new PakManifestV3
        {
            Entries = entries,
            PathStrings = pathStrings
        };
        // Rewind stream for future reads if needed
        input.Position = 0;
        // Get file length if stream supports it (for bounds checking)
        long fileLength = input.CanSeek ? input.Length : 0;
        return new PakArchiveV3Reader(input, manifest, pathStrings, entries, payloadOffset, (long)manifestOffset, (int)manifestSize, fileLength, leaveOpen);
    }

    private long ManifestStart { get; }
    private int ManifestLength { get; }

    public PakManifestEntryV3? FindEntry(string path)
    {
        ulong hash = PathHash64(path);
        int left = 0, right = Entries.Count - 1;
        while (left <= right)
        {
            int mid = left + ((right - left) >> 1);
            var h = Entries[mid].PathHash;
            if (hash == h) return Entries[mid];
            if (hash < h) right = mid - 1; else left = mid + 1;
        }
        return null; // not found
    }

    public byte[] ReadAllBytes(string path, bool verifyHash = true)
    {
        var entry = FindEntry(path);
        if (entry == null) throw new FileNotFoundException($"Pak entry not found: {path}");

        // Bounds checking: validate entry offset+size against file length
        long entryStart = (long)PayloadOffset + (long)entry.Offset;
        long entryEnd = entryStart + entry.Size;
        long fileLength = _fileLength;
        // For stream-based access without MMF, try to get length from stream
        if (fileLength == 0 && _stream.CanSeek)
        {
            fileLength = _stream.Length;
        }
        if (fileLength > 0 && entryEnd > fileLength)
        {
            throw new InvalidDataException("Manifest entry exceeds file bounds");
        }

        // Prefer memory-mapped path when available for faster random access
        if (_accessor != null)
        {
            long pos = (long)PayloadOffset + (long)entry.Offset;
            var payload = new byte[entry.Size];
            // Read via memory-mapped view accessor. Use ReadArray overload.
            _accessor.ReadArray(pos, payload, 0, payload.Length);
            return payload;
        }

        // Fallback to traditional stream-based read
        _stream.Position = (long)PayloadOffset + (long)entry.Offset;
        var buffer = new byte[entry.Size];
        _stream.ReadExactly(buffer);
        // No encryption in Phase 1
        if (verifyHash)
        {
            // Recompute hash of plaintext to compare with manifest hash if provided (not stored in v3 entry here). Skip if not available.
        }
        return buffer;
    }

    // xxHash64 for path hashing (UTF-8, lowercase)
    public static ulong PathHash64(string path)
    {
        var bytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
        var hashBytes = XxHash64.Hash(bytes);
        return BitConverter.ToUInt64(hashBytes);
    }

    public void Dispose()
    {
        if (!_leaveOpen) _stream?.Dispose();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}
