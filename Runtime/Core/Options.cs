// com.bpg.aion/Runtime/Core/Options.cs
#nullable enable
namespace BPG.Aion
{
    /// <summary>
    /// Options controlling how a save operation behaves.
    /// </summary>
    public sealed class SaveOptions
    {
        public bool UseCompression { get; set; }
        public bool UseEncryption { get; set; }
        public string? ProfileName { get; set; } = "Default";
        public string? Summary { get; set; }
        public string ContentType { get; set; } = "application/bpgsave+json";
        public string AppVersion { get; set; } = "Unknown";
    }

    /// <summary>
    /// Options controlling how a load operation behaves.
    /// </summary>
    public sealed class LoadOptions
    {
        public bool UseCompression { get; set; } // used only as a hint; header is authoritative
        public bool UseEncryption { get; set; }   // same as above
        public string? ProfileName { get; set; } = "Default";
    }
}
