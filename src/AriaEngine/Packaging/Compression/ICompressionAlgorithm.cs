using System;
using System.IO;

namespace AriaEngine.Packaging.Compression;
public interface ICompressionAlgorithm
{
    // Compress data and return the compressed bytes. Level is optional and platform dependent.
    byte[] Compress(byte[] data, int level = 3);

    // Decompress data given compressed bytes and return the original bytes.
    // Implementations may auto-detect original size via frame metadata or stream handling.
    byte[] Decompress(byte[] compressed);
}
