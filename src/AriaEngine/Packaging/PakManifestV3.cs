using System.Collections.Generic;

namespace AriaEngine.Packaging;

// Binary manifest for PakArchiveV3.
// EntryTable: for each entry, stores PathHash (xxHash64), Offset (payload offset), Size, OriginalSize, Flags
// PathStringPool: null-terminated path strings in the same order as Entries
public sealed class PakManifestV3
{
    public string Version { get; set; } = "3.0";
    public List<PakManifestEntryV3> Entries { get; set; } = new();
    // Path strings must be provided in the same order as Entries
    public List<string> PathStrings { get; set; } = new();
}

public sealed class PakManifestEntryV3
{
    // 8 bytes
    public ulong PathHash { get; set; }
    // 8 bytes
    public ulong Offset { get; set; }
    // 4 bytes
    public uint Size { get; set; }
    // 4 bytes
    public uint OriginalSize { get; set; }
    // 1 byte flags: bit0 encrypted, bit1 compressed, bit2 chunked
    public byte Flags { get; set; }
}
