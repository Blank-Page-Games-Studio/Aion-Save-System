// Packages/com.bpg.aion/Runtime/Attributes/SaveFieldAttribute.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>Marks a field or property for inclusion in save snapshots.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class SaveFieldAttribute : Attribute { }
}
