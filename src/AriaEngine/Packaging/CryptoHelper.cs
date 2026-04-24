using System;
using System.Security.Cryptography;
using System.Text;

namespace AriaEngine.Packaging;

public static class CryptoHelper
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static byte[] DeriveKey(string keyMaterial)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    public static byte[] Encrypt(byte[] plain, byte[] key)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        byte[] output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return output;
    }

    public static byte[] Decrypt(byte[] encrypted, byte[] key)
    {
        if (encrypted.Length < NonceSize + TagSize)
            throw new InvalidOperationException("Encrypted payload is too short.");

        byte[] nonce = new byte[NonceSize];
        byte[] tag = new byte[TagSize];
        int cipherLen = encrypted.Length - NonceSize - TagSize;
        byte[] cipher = new byte[cipherLen];
        byte[] plain = new byte[cipherLen];

        Buffer.BlockCopy(encrypted, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encrypted, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(encrypted, NonceSize + TagSize, cipher, 0, cipherLen);

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    public static string Sha256Hex(byte[] data)
    {
        byte[] hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
