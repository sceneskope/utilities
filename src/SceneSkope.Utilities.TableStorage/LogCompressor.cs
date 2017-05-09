using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace SceneSkope.Utilities.TableStorage
{
    internal static class LogCompressor
    {
        private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();

        public static byte[] Compress(string line)
        {
            var buffer = _pool.Rent(line.Length);
            try
            {
                using (var ms = new MemoryStream(buffer))
                using (var gzs = new GZipStream(ms, CompressionLevel.Optimal))
                using (var writer = new StreamWriter(gzs))
                {
                    writer.Write(line);
                    writer.Flush();
                    gzs.Flush();
                    var data = new byte[ms.Position];
                    Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                    return data;
                }
            }
            finally
            {
                _pool.Return(buffer);
            }
        }

        public static string Decompress(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var gzs = new GZipStream(ms, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzs))
            {
                var str = reader.ReadToEnd();
                return str;
            }
        }
    }
}
