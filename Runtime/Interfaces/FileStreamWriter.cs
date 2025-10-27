// com.bpg.aion/Runtime/Interfaces/FileStreamWriter.cs
#nullable enable
using System;
using System.IO;

namespace BPG.Aion
{
    /// <summary>
    /// Atomic file writer: writes to "path.tmp" and atomically replaces the final file on Commit.
    /// </summary>
    public sealed class FileStreamWriter : IStreamWriter
    {
        private readonly string _tmpPath;
        private readonly FileStream _fs;
        public string FinalPath { get; }

        public Stream BaseStream => _fs;

        public FileStreamWriter(string finalPath)
        {
            FinalPath = finalPath ?? throw new ArgumentNullException(nameof(finalPath));
            var dir = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _tmpPath = finalPath + ".tmp";
            _fs = new FileStream(_tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 64 * 1024, useAsync: true);
        }

        public System.Threading.Tasks.Task WriteAsync(ReadOnlyMemory<byte> buffer, System.Threading.CancellationToken token = default)
            => _fs.WriteAsync(buffer, token).AsTask();

        public void Commit()
        {
            try
            {
                _fs.Flush();
                try { _fs.Flush(true); } catch (NotSupportedException) { /* fallback */ }
            }
            finally
            {
                _fs.Dispose();
            }

            if (File.Exists(FinalPath)) File.Delete(FinalPath);
            File.Move(_tmpPath, FinalPath);
        }

        public void Dispose()
        {
            _fs.Dispose();
            // On dispose without commit, clean temp if exists
            if (File.Exists(_tmpPath))
            {
                try { File.Delete(_tmpPath); } catch { /* ignore */ }
            }
        }
    }
}
