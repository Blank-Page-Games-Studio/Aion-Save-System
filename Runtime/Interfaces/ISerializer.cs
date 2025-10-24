// com.bpg.aion/Runtime/Interfaces/ISerializer.cs
#nullable enable

namespace BPG.Aion
{
    /// <summary>
    /// Abstraction for serialization to/from a textual representation.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>Serialize a value to a deterministic JSON string.</summary>
        string Serialize<T>(T value);

        /// <summary>Deserialize a value from a JSON string.</summary>
        T Deserialize<T>(string json);
    }
}
