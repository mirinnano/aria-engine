using System;
using System.Security.Cryptography;
using System.Text;

namespace AriaEngine.Packaging
{
    // Provides per-chunk AES-CTR encryption/decryption for Pak v3.0 Phase 2-3
    public static class ChunkEncryption
    {
        // Derive a 32-byte key for this chunk from a master key and chunk index
        private static byte[] DeriveChunkKey(byte[] masterKey, long chunkIndex)
        {
            // 8-byte big-endian representation of chunkIndex
            byte[] indexBytes = BitConverter.GetBytes(chunkIndex);
            if (BitConverter.IsLittleEndian) Array.Reverse(indexBytes);
            byte[] label = Encoding.ASCII.GetBytes("chunk");
            byte[] data = new byte[label.Length + indexBytes.Length];
            Buffer.BlockCopy(label, 0, data, 0, label.Length);
            Buffer.BlockCopy(indexBytes, 0, data, label.Length, indexBytes.Length);

            using (var hmac = new HMACSHA256(masterKey))
            {
                byte[] full = hmac.ComputeHash(data);
                byte[] key = new byte[32];
                Array.Copy(full, 0, key, 0, 32);
                return key;
            }
        }

        // Derive a 16-byte IV from the derived chunk key and chunk index
        private static byte[] DeriveIV(byte[] chunkKey, long chunkIndex)
        {
            // IV = HMAC-SHA256(chunkKey, "chunk" + chunkIndex)[0..16]
            byte[] indexBytes = BitConverter.GetBytes(chunkIndex);
            if (BitConverter.IsLittleEndian) Array.Reverse(indexBytes);
            byte[] label = Encoding.ASCII.GetBytes("chunk");
            byte[] data = new byte[label.Length + indexBytes.Length];
            Buffer.BlockCopy(label, 0, data, 0, label.Length);
            Buffer.BlockCopy(indexBytes, 0, data, label.Length, indexBytes.Length);

            using (var hmac = new HMACSHA256(chunkKey))
            {
                byte[] full = hmac.ComputeHash(data);
                byte[] iv = new byte[16];
                Array.Copy(full, 0, iv, 0, 16);
                return iv;
            }
        }

        // Encrypt a single chunk of data with AES-CTR
        public static byte[] EncryptChunk(byte[] data, byte[] masterKey, long chunkIndex)
        {
            byte[] chunkKey = DeriveChunkKey(masterKey, chunkIndex);
            byte[] iv = DeriveIV(chunkKey, chunkIndex);

            // .NET in this environment does not expose CipherMode.CTR. Implement CTR manually using AES-ECB.
            int blockSize = 16;
            byte[] output = new byte[data.Length];

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.ECB; // ECB to generate keystream blocks
                aes.Padding = PaddingMode.None;
                aes.Key = chunkKey;

                using (ICryptoTransform ecb = aes.CreateEncryptor(aes.Key, null))
                {
                    long blocks = (data.Length + blockSize - 1) / blockSize;
                    for (long b = 0; b < blocks; b++)
                    {
                        // Construct counter block: IV[0..11] as nonce, then big-endian block counter in IV[12..15]
                        byte[] counterBlock = new byte[16];
                        Buffer.BlockCopy(iv, 0, counterBlock, 0, 12);
                        counterBlock[12] = (byte)((b >> 24) & 0xFF);
                        counterBlock[13] = (byte)((b >> 16) & 0xFF);
                        counterBlock[14] = (byte)((b >> 8) & 0xFF);
                        counterBlock[15] = (byte)(b & 0xFF);

                        byte[] ks = new byte[16];
                        ks = ecb.TransformFinalBlock(counterBlock, 0, 16);

                        int offset = (int)(b * blockSize);
                        int remaining = Math.Min(blockSize, data.Length - offset);
                        for (int i = 0; i < remaining; i++)
                        {
                            output[offset + i] = (byte)(data[offset + i] ^ ks[i]);
                        }
                    }
                }
            }
            return output;
        }

        // Decrypt a single chunk of data with AES-CTR
        public static byte[] DecryptChunk(byte[] encrypted, byte[] masterKey, long chunkIndex)
        {
            byte[] chunkKey = DeriveChunkKey(masterKey, chunkIndex);
            byte[] iv = DeriveIV(chunkKey, chunkIndex);

            // Manual CTR decryption mirrors encryption
            int blockSize = 16;
            byte[] output = new byte[encrypted.Length];

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = chunkKey;

                using (ICryptoTransform ecb = aes.CreateEncryptor(aes.Key, null))
                {
                    long blocks = (encrypted.Length + blockSize - 1) / blockSize;
                    for (long b = 0; b < blocks; b++)
                    {
                        byte[] counterBlock = new byte[16];
                        Buffer.BlockCopy(iv, 0, counterBlock, 0, 12);
                        counterBlock[12] = (byte)((b >> 24) & 0xFF);
                        counterBlock[13] = (byte)((b >> 16) & 0xFF);
                        counterBlock[14] = (byte)((b >> 8) & 0xFF);
                        counterBlock[15] = (byte)(b & 0xFF);

                        byte[] ks = ecb.TransformFinalBlock(counterBlock, 0, 16);

                        int offset = (int)(b * blockSize);
                        int remaining = Math.Min(blockSize, encrypted.Length - offset);
                        for (int i = 0; i < remaining; i++)
                        {
                            output[offset + i] = (byte)(encrypted[offset + i] ^ ks[i]);
                        }
                    }
                }
            }
            return output;
        }
    }
}
