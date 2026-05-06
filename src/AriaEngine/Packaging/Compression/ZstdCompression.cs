using System;
using System.IO;
using ZstdSharp;

namespace AriaEngine.Packaging.Compression;
public class ZstdCompression : ICompressionAlgorithm
{
    private const long MaxOutputSize = 256L * 1024 * 1024; // 256MB limit to prevent zip bomb attacks

    // Compress with a given level (default 3)
    public byte[] Compress(byte[] data, int level = 3)
    {
        // Normalize level within Zstandard bounds (1..22). If out of range, clamp to valid range.
        if (level < 1) level = 1;
        if (level > 22) level = 22;
        return Zstd.Compress(data, level);
    }

    // Decompress using Zstd frame. This uses a stream-based approach to automatically
    // determine the decompressed size when the frame contains a content size or allows
    // streaming to exhaust the uncompressed data.
    public byte[] Decompress(byte[] compressed)
    {
        if (compressed == null) throw new ArgumentNullException(nameof(compressed));
        // Use ZstdStream to handle unknown uncompressed size gracefully.
        using var input = new MemoryStream(compressed);
        using var zstream = new ZstdStream(input, ZstdStreamMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = zstream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxOutputSize)
                throw new InvalidDataException("Zstd decompressed size exceeds maximum allowed");
            output.Write(buffer, 0, bytesRead);
        }
        return output.ToArray();
    }
}
