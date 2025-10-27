// com.bpg.aion/Runtime/Core/SaveDiagnostics.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Snapshot of profiling/counters for a save/load.
    /// </summary>
    [Serializable]
    public struct SaveDiagnostics
    {
        public long DurationMs;
        public long BytesWritten;
        public long BytesRead;
        public double CompressionRatio;
        public long ManagedAllocatedBefore;
        public long ManagedAllocatedAfter;
        public string Pipeline; // "plain", "gzip", "aes-gcm", "gzip+aes-gcm"
        public bool RecoveredFromBackup;
    }
}
