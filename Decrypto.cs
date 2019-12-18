﻿using System;
using System.IO;
using System.Security.Cryptography;

namespace MusicDecrypto
{
    internal abstract class Decrypto
    {
        public static bool SkipDuplicate { get; set; } = false;
        public static string OutputDir { get; set; } = null;
        public static ulong SuccessCount { get; private set; } = 0;

        public string InPath { get; private set; }
        protected BinaryReader InFile { get; set; } = null;
        protected MemoryStream OutBuffer { get; set; } = new MemoryStream();
        protected MemoryStream CoverBuffer { get; set; } = new MemoryStream();
        protected string CoverMime { get; set; }
        protected string MusicMime { get; set; }
        public Metadata StdMetadata { get; protected set; }

        protected Decrypto(string path, string mime = null)
        {
            InPath = path;
            InFile = new BinaryReader(new FileStream(InPath, FileMode.Open));
            MusicMime = mime;
        }

        ~Decrypto()
        {
            InFile.Dispose();
            OutBuffer.Dispose();
            CoverBuffer.Dispose();
        }

        protected abstract void Load();

        protected virtual void FixMetadata() { }

        protected void Save()
        {
            string extension = MusicMime switch
            {
                "audio/flac" => "flac",
                "audio/mpeg" => "mp3",
                _ => throw new FileLoadException($"Failed to recognize music in {InPath}."),
            };

            string path;
            if (OutputDir == null)
            {
                path = $"{Path.Combine(Path.GetDirectoryName(InPath), Path.GetFileNameWithoutExtension(InPath))}.{extension}";
            }
            else
            {
                path = $"{Path.Combine(OutputDir, Path.GetFileNameWithoutExtension(InPath))}.{extension}";
            }

            if (File.Exists(path) && SkipDuplicate)
            {
                Console.WriteLine($"[INFO] Skipping {path}");
                return;
            }

            using FileStream file = new FileStream(path, FileMode.Create);
            OutBuffer.WriteTo(file);
            SuccessCount += 1;
            Console.WriteLine($"[INFO] File was decrypted successfully at {path}.");
        }

        protected byte[] ReadFixedChunk(ref int size)
        {
            byte[] chunk = new byte[size];
            size = InFile.Read(chunk, 0, size);
            return chunk;
        }

        protected byte[] ReadIndexedChunk(byte? obfuscator)
        {
            int chunkSize = InFile.ReadInt32();

            if (chunkSize > 0)
            {
                byte[] chunk = new byte[chunkSize];
                InFile.Read(chunk, 0, chunkSize);
                if (obfuscator != null)
                {
                    for (int i = 0; i < chunkSize; i += 1)
                        chunk[i] ^= obfuscator.Value;
                }
                return chunk;
            }
            else
            {
                throw new NullFileChunkException("Failed to load file chunk.");
            }
        }

        public static byte[] AesEcbDecrypt(byte[] cipher, byte[] key)
        {
            using RijndaelManaged rijndael = new RijndaelManaged
            {
                Key = key,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };
            using ICryptoTransform cryptoTransform = rijndael.CreateDecryptor();
            byte[] plain = cryptoTransform.TransformFinalBlock(cipher, 0, cipher.Length);
            return plain;
        }
    }

    internal class NullFileChunkException : IOException
    {
        internal NullFileChunkException(string message)
            : base(message) { }
    }
}
