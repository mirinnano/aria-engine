using System;

namespace AriaEngine.Packaging.Compression;
public static class CompressionHelper
{
    public static ICompressionAlgorithm Create(CompressionAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case CompressionAlgorithm.Zstd:
                return new ZstdCompression();
            case CompressionAlgorithm.Lz4:
                // LZ4 implementation is out of scope for this task.
                return new Lz4Compression();
            default:
                throw new NotSupportedException($"Unsupported compression algorithm: {algorithm}");
        }
    }
}
