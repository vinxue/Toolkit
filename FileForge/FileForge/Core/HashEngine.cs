using System;
using System.IO;
using System.Security.Cryptography;

namespace FileForge.Core
{
    public static class HashEngine
    {
        private const int BufferSize = 65536;

        public static string ComputeMD5(string filePath, long offset = 0, long size = -1)
        {
            using (var md5 = MD5.Create())
                return ComputeHash(md5, filePath, offset, size);
        }

        public static string ComputeSHA1(string filePath, long offset = 0, long size = -1)
        {
            using (var sha1 = SHA1.Create())
                return ComputeHash(sha1, filePath, offset, size);
        }

        public static string ComputeSHA256(string filePath, long offset = 0, long size = -1)
        {
            using (var sha256 = SHA256.Create())
                return ComputeHash(sha256, filePath, offset, size);
        }

        public static string ComputeSHA512(string filePath, long offset = 0, long size = -1)
        {
            using (var sha512 = SHA512.Create())
                return ComputeHash(sha512, filePath, offset, size);
        }

        public static uint ComputeCRC32(string filePath, long offset = 0, long size = -1)
        {
            byte[] buffer = new byte[BufferSize];
            uint crc = 0xFFFFFFFF;

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (offset > 0) stream.Seek(offset, SeekOrigin.Begin);
                long remaining = size < 0
                    ? stream.Length - stream.Position
                    : Math.Min(size, stream.Length - stream.Position);

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = stream.Read(buffer, 0, toRead);
                    if (read == 0) break;
                    for (int i = 0; i < read; i++)
                        crc = (crc >> 8) ^ s_crc32Table[(crc ^ buffer[i]) & 0xFF];
                    remaining -= read;
                }
            }
            return crc ^ 0xFFFFFFFF;
        }

        private static string ComputeHash(HashAlgorithm algo, string filePath, long offset, long size)
        {
            byte[] buffer = new byte[BufferSize];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (offset > 0) stream.Seek(offset, SeekOrigin.Begin);
                long remaining = size < 0
                    ? stream.Length - stream.Position
                    : Math.Min(size, stream.Length - stream.Position);

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(buffer.Length, remaining);
                    int read = stream.Read(buffer, 0, toRead);
                    if (read == 0) break;
                    algo.TransformBlock(buffer, 0, read, buffer, 0);
                    remaining -= read;
                }
                algo.TransformFinalBlock(buffer, 0, 0);
            }
            return BitConverter.ToString(algo.Hash).Replace("-", "").ToLowerInvariant();
        }

        private static readonly uint[] s_crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                table[i] = crc;
            }
            return table;
        }
    }
}
