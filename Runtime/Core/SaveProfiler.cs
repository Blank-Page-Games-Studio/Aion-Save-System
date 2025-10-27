// com.bpg.aion/Runtime/Core/SaveProfiler.cs
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BPG.Aion
{
    /// <summary>
    /// Lightweight profiler for the save pipeline. Can optionally write JSON to disk
    /// using the project's configured serializer.
    /// </summary>
    public static class SaveProfiler
    {
        private static readonly Stopwatch Sw = new();
        private static long _allocStart;

        /// <summary>Call before starting the save/load pipeline.</summary>
        public static void Begin()
        {
            _allocStart = GC.GetTotalMemory(forceFullCollection: false);
            Sw.Restart();
        }

        /// <summary>Finish profiling and return diagnostics snapshot.</summary>
        public static SaveDiagnostics End(long bytesWritten, long bytesRead, double compressionRatio, string pipeline, bool recovered = false)
        {
            Sw.Stop();
            var after = GC.GetTotalMemory(false);
            return new SaveDiagnostics
            {
                DurationMs = Sw.ElapsedMilliseconds,
                BytesWritten = bytesWritten,
                BytesRead = bytesRead,
                CompressionRatio = compressionRatio,
                ManagedAllocatedBefore = _allocStart,
                ManagedAllocatedAfter = after,
                Pipeline = pipeline,
                RecoveredFromBackup = recovered
            };
        }

        /// <summary>
        /// Write diagnostics JSON using the project's serializer.
        /// Best-effort; failures are swallowed to avoid impacting gameplay.
        /// </summary>
        public static void WriteJson(string saveRoot, SaveDiagnostics diag, ISerializer serializer)
        {
            try
            {
                Directory.CreateDirectory(saveRoot);
                var path = Path.Combine(saveRoot, "save_profiler.json");
                var json = serializer.Serialize(diag);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch
            {
                // Intentionally ignore: diagnostics should never break the game.
            }
        }
    }
}
