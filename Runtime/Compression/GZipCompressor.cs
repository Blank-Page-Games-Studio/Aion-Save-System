// com.bpg.aion/Runtime/Compression/GZipCompressor.cs
#nullable enable
using System.IO;
using System.IO.Compression;

namespace BPG.Aion
{
    /// <summary>
    /// GZip compressor using System.IO.Compression.
    /// </summary>
    public sealed class GZipCompressor : ICompressor
    {
        public string Name => "gzip";

        public byte[] Compress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                gz.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        public byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }
    }
}
