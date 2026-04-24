using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AriaEngine.Packaging;

namespace AriaEngine.Scripting;

public static class CompiledBundleCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ARIAC1");

    public static void Save(string outputPath, CompiledScriptBundle bundle, string? keyMaterial)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(bundle);
        bool enc = !string.IsNullOrWhiteSpace(keyMaterial);
        byte[] payload = enc ? CryptoHelper.Encrypt(json, CryptoHelper.DeriveKey(keyMaterial!)) : json;

        using var fs = File.Create(outputPath);
        fs.Write(Magic, 0, Magic.Length);
        fs.WriteByte(enc ? (byte)1 : (byte)0);
        fs.Write(BitConverter.GetBytes(payload.Length), 0, sizeof(int));
        fs.Write(payload, 0, payload.Length);
    }

    public static CompiledScriptBundle Load(Stream stream, string? keyMaterial)
    {
        byte[] magic = new byte[Magic.Length];
        stream.ReadExactly(magic);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidOperationException("Invalid ARIAC header.");

        int encFlag = stream.ReadByte();
        if (encFlag < 0) throw new InvalidOperationException("Invalid ARIAC payload flag.");

        byte[] lenBuf = new byte[sizeof(int)];
        stream.ReadExactly(lenBuf);
        int len = BitConverter.ToInt32(lenBuf, 0);
        if (len <= 0) throw new InvalidOperationException("Invalid ARIAC payload length.");

        byte[] payload = new byte[len];
        stream.ReadExactly(payload);

        byte[] plain = payload;
        if (encFlag == 1)
        {
            if (string.IsNullOrWhiteSpace(keyMaterial))
                throw new InvalidOperationException("Encrypted ARIAC requires --key.");
            plain = CryptoHelper.Decrypt(payload, CryptoHelper.DeriveKey(keyMaterial!));
        }

        return JsonSerializer.Deserialize<CompiledScriptBundle>(plain)
            ?? throw new InvalidOperationException("Failed to deserialize ARIAC.");
    }
}
