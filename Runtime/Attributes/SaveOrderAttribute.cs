// Packages/com.bpg.aion/Runtime/Attributes/SaveOrderAttribute.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Controls deterministic ordering of members during generation.
    /// Lower numbers come first; ties resolved by declaration order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class SaveOrderAttribute : Attribute
    {
        public int Order { get; }
        public SaveOrderAttribute(int order) { Order = order; }
    }
}
