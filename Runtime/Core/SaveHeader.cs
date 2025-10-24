// com.bpg.aion/Runtime/Core/SaveHeader.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Semantic version.
    /// </summary>
    [Serializable]
    public sealed class SemVer
    {
        public int Major = 2;
        public int Minor = 0;
        public int Patch = 0;
    }

    /// <summary>
    /// Metadata stored with each save file.
    /// Header is serialized deterministically and also used as AAD for AEAD encryption.
    /// </summary>
    [Serializable]
    public sealed class SaveHeader
    {
        // Identity
        public string FormatId = "BPG.SAVE_v1"; // unchanged format id
        public SemVer Version = new() { Major = 2, Minor = 0, Patch = 0 };

        // Timestamps
        public string CreatedUtc = DateTime.UtcNow.ToString("o");
        public string ModifiedUtc = DateTime.UtcNow.ToString("o");

        // App + content
        public string AppVersion = string.Empty;
        public string ContentType = "application/bpgsave+json";

        // Transforms
        public string? Compress = null;   // e.g., "gzip"
        public string? Encrypt = null;    // e.g., "aes-gcm"
        public string? NonceB64 = null;   // present if encrypted
        public string? TagB64 = null;     // present if encrypted

        // Integrity (hex SHA-256 of final body bytes after transforms)
        public string Checksum = string.Empty;

        // Profiles
        public string Profile = "Default";
        public long PlaytimeSeconds = 0;
        public string? Summary = null;
    }

    /// <summary>
    /// One captured component's data.
    /// </summary>
    [Serializable]
    public sealed class ComponentSnapshot
    {
        public string Key = string.Empty;
        public FieldEntry[] Fields = Array.Empty<FieldEntry>();
    }

    /// <summary>
    /// One captured field entry.
    /// </summary>
    [Serializable]
    public sealed class FieldEntry
    {
        public string Name = string.Empty;
        public string Type = string.Empty;     // AssemblyQualifiedName
        public string JsonValue = string.Empty;
    }

    /// <summary>
    /// The logical save "document": Header + Body (Components). 
    /// The on-disk file stores Header JSON (plaintext) + Body bytes (possibly compressed and/or encrypted).
    /// </summary>
    [Serializable]
    public sealed class SaveFile
    {
        public SaveHeader Header = new();
        public ComponentSnapshot[] Components = Array.Empty<ComponentSnapshot>();
    }
}
