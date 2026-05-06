using System;
using System.IO;
using K4os.Compression.LZ4;

namespace AriaEngine.Packaging.Compression;

public class Lz4Compression : ICompressionAlgorithm
{
    public byte[] Compress(byte[] data, int level = 3)
    {
        if (data == null || data.Length == 0)
            return Array.Empty<byte>();

        // Map level to LZ4 compression level
        var compressionLevel = level switch
        {
            <= 1 => LZ4Level.L00_FAST,
            <= 3 => LZ4Level.L03_HC,
            <= 6 => LZ4Level.L06_HC,
            <= 9 => LZ4Level.L09_HC,
            _ => LZ4Level.L12_MAX
        };

        // Allocate maximum possible output size
        var maxOutputSize = LZ4Codec.MaximumOutputSize(data.Length);
        var buffer = new byte[maxOutputSize];

        // Compress the data using block compression
        var encodedLength = LZ4Codec.Encode(
            data, 0, data.Length,
            buffer, 0, buffer.Length);

if (encodedLength > 0 && encodedLength < data.Length)
        {
            // Compression was beneficial - store positive originalSize in header
            var result = new byte[encodedLength + 4];
            BitConverter.GetBytes(data.Length).CopyTo(result, 0);
            Buffer.BlockCopy(buffer, 0, result, 4, encodedLength);
            return result;
        }

        // Compression didn't help - store negative originalSize to mark as uncompressed
        var uncompressed = new byte[data.Length + 4];
        BitConverter.GetBytes(-data.Length).CopyTo(uncompressed, 0);
        data.CopyTo(uncompressed, 4);
        return uncompressed;
    }

    public byte[] Decompress(byte[] compressed)
    {
        if (compressed == null || compressed.Length < 4)
            throw new InvalidDataException("LZ4 input too short");

        // Read stored size from first 4 bytes
        int storedSize = BitConverter.ToInt32(compressed, 0);

        // Reject int.MinValue which would overflow when negated
        if (storedSize == int.MinValue)
            throw new InvalidDataException("Invalid LZ4 header");

        // Negative storedSize means data is uncompressed (stored as-is)
        if (storedSize < 0)
        {
            var originalLength = -storedSize;
            var result = new byte[originalLength];
            Buffer.BlockCopy(compressed, 4, result, 0, originalLength);
            return result;
        }

        // Positive storedSize means data is LZ4 compressed - storedSize is original (uncompressed) size
        if (storedSize > 256 * 1024 * 1024)
            throw new InvalidDataException("LZ4 decompressed size exceeds maximum allowed");

        var output = new byte[storedSize];
        int compressedLength = compressed.Length - 4;
        var bytesDecoded = LZ4Codec.Decode(
            compressed, 4, compressedLength,
            output, 0, output.Length);

        if (bytesDecoded < 0)
            throw new InvalidDataException("LZ4 decompression failed.");

        if (bytesDecoded != storedSize)
            throw new InvalidDataException($"LZ4 decompression size mismatch: expected {storedSize}, got {bytesDecoded}.");

        return output;
    }
}
