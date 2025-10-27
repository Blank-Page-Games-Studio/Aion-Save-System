// Packages/com.bpg.aion/Runtime/Attributes/GenerateSaveSnapshotAttribute.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Marks a partial class implementing ISaveable for compile-time snapshot code generation.
    /// The generator will emit a nested struct (DTO) and Capture/Restore methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GenerateSaveSnapshotAttribute : Attribute
    {
        /// <summary>Include public instance properties decorated with [SaveField].</summary>
        public bool IncludeProperties { get; set; } = true;

        /// <summary>Include non-public members (private/protected) when decorated with [SaveField].</summary>
        public bool IncludeNonPublic { get; set; } = false;

        /// <summary>Name of the generated nested struct. Defaults to "Snapshot".</summary>
        public string? SnapshotName { get; set; } = null;

        /// <summary>Optional save key override emitted as a string property 'SaveKey'.</summary>
        public string? SaveKeyOverride { get; set; } = null;

        public GenerateSaveSnapshotAttribute() { }
    }
}
