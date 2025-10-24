// com.bpg.aion/Runtime/Attributes/SaveField.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Marks a field to be captured/restored by the save system.
    /// Supported (Phase 1): bool, numeric primitives, string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SaveFieldAttribute : Attribute { }
}
