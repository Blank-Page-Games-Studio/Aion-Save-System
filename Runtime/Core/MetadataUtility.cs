// com.bpg.aion/Runtime/Core/MetadataUtility.cs
#nullable enable
using UnityEngine.SceneManagement;

namespace BPG.Aion
{
    /// <summary>
    /// Helpers for writing metadata files.
    /// </summary>
    public static class MetadataUtility
    {
        public static SlotMetadata CreateForManual(int slot, string profile, long durationMs, string? summary, long approxBytes)
        {
            return new SlotMetadata
            {
                Slot = slot,
                Profile = profile,
                DurationMs = durationMs,
                Scene = SceneManager.GetActiveScene().name,
                Summary = summary,
                ApproxBytes = approxBytes,
                IsAutosave = false,
                AutosaveIndex = -1
            };
        }

        public static SlotMetadata CreateForAutosave(int index, string profile, long durationMs, string? summary, long approxBytes)
        {
            return new SlotMetadata
            {
                Slot = -1,
                Profile = profile,
                DurationMs = durationMs,
                Scene = SceneManager.GetActiveScene().name,
                Summary = summary,
                ApproxBytes = approxBytes,
                IsAutosave = true,
                AutosaveIndex = index
            };
        }
    }
}
