// com.bpg.aion/Runtime/Attributes/SaveKey.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Optional attribute for declaring a stable, explicit save key for a component.
    /// If omitted, the system falls back to a deterministic path-based key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class SaveKeyAttribute : Attribute
    {
        /// <summary>Explicit key used to identify this save target in snapshots.</summary>
        public string Key { get; }

        public SaveKeyAttribute(string key) => Key = key;
    }
}
