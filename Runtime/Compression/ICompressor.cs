// com.bpg.aion/Runtime/Compression/ICompressor.cs
#nullable enable
namespace BPG.Aion
{
    /// <summary>
    /// Byte-level compressor abstraction.
    /// </summary>
    public interface ICompressor
    {
        /// <summary>Compressor algorithm name (e.g., "gzip").</summary>
        string Name { get; }

        /// <summary>Compress raw bytes.</summary>
        byte[] Compress(byte[] data);

        /// <summary>Decompress compressed bytes.</summary>
        byte[] Decompress(byte[] data);
    }
}
