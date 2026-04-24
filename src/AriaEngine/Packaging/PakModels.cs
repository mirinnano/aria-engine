using System;
using System.Collections.Generic;

namespace AriaEngine.Packaging;

public sealed class PakManifest
{
    public string Version { get; set; } = "1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<PakManifestEntry> Entries { get; set; } = new();
}

public sealed class PakManifestEntry
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "binary";
    public long Offset { get; set; }
    public int Size { get; set; }
    public int OriginalSize { get; set; }
    public bool Enc { get; set; }
    public string Hash { get; set; } = "";
}
