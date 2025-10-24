// com.bpg.aion/Runtime/Core/SlotMetadata.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Lightweight metadata written alongside each slot save.
    /// </summary>
    [Serializable]
    public sealed class SlotMetadata
    {
        public int Slot = -1;
        public string Profile = "Default";
        public string CreatedUtc = DateTime.UtcNow.ToString("o");
        public string ModifiedUtc = DateTime.UtcNow.ToString("o");
        public long DurationMs = 0;
        public string Scene = string.Empty;
        public string? Summary = null;
        public long ApproxBytes = 0;
        public bool IsAutosave = false;
        public int AutosaveIndex = -1;
    }
}
