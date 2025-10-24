// com.bpg.aion/Runtime/Core/SaveResults.cs
#nullable enable
namespace BPG.Aion
{
    /// <summary>Operation outcome categories.</summary>
    public enum ResultStatus { Ok, NotFound, Corrupt, Unauthorized, Error }

    public readonly struct SaveResult
    {
        public ResultStatus Status { get; }
        public string Message { get; }
        public string Path { get; }

        public SaveResult(ResultStatus status, string message, string path)
        {
            Status = status; Message = message; Path = path;
        }
    }

    public readonly struct LoadResult
    {
        public ResultStatus Status { get; }
        public string Message { get; }
        public string Path { get; }

        public LoadResult(ResultStatus status, string message, string path)
        {
            Status = status; Message = message; Path = path;
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
