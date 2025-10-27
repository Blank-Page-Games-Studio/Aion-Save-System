// com.bpg.aion/Runtime/Interfaces/IStreamWriter.cs
#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BPG.Aion
{
    /// <summary>
    /// Abstraction over a writable stream that preserves atomicity (tmp -> replace)
    /// and exposes async chunked writes.
    /// </summary>
    public interface IStreamWriter : IDisposable
    {
        /// <summary>Absolute path the final file will take after Commit().</summary>
        string FinalPath { get; }

        /// <summary>Write bytes asynchronously.</summary>
        Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default);

        /// <summary>Expose the underlying Stream if low-level piping is needed.</summary>
        Stream BaseStream { get; }

        /// <summary>Commit the temp file to FinalPath atomically.</summary>
        void Commit();
    }
}
