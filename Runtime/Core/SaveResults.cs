// com.bpg.aion/Runtime/Core/SaveResults.cs
#nullable enable
namespace BPG.Aion
{
    public enum ResultStatus { Ok, NotFound, Corrupt, Unauthorized, Error }

    public readonly struct SaveResult
    {
        public ResultStatus Status { get; }
        public string Message { get; }
        public string Path { get; }
        public long DurationMs { get; }
        public long BytesWritten { get; }
        public double CompressionRatio { get; }

        public SaveResult(ResultStatus status, string message, string path, long durationMs, long bytesWritten, double compressionRatio)
        {
            Status = status; Message = message; Path = path;
            DurationMs = durationMs; BytesWritten = bytesWritten; CompressionRatio = compressionRatio;
        }
    }

    public readonly struct LoadResult
    {
        public ResultStatus Status { get; }
        public string Message { get; }
        public string Path { get; }
        public long DurationMs { get; }
        public long BytesRead { get; }
        public bool RecoveredFromBackup { get; }

        public LoadResult(ResultStatus status, string message, string path, long durationMs, long bytesRead, bool recoveredFromBackup)
        {
            Status = status; Message = message; Path = path;
            DurationMs = durationMs; BytesRead = bytesRead; RecoveredFromBackup = recoveredFromBackup;
        }
    }

    public readonly struct DeleteResult
    {
        public ResultStatus Status { get; }
        public string Message { get; }
        public string Path { get; }
        public DeleteResult(ResultStatus status, string message, string path)
        {
            Status = status; Message = message; Path = path;
        }
    }
}
