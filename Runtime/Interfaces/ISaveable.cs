// com.bpg.aion/Runtime/Interfaces/ISaveable.cs
#nullable enable
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Marker interface for MonoBehaviours that opt into the save system.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// The Unity component instance. Used for reflection and scene key generation.
        /// </summary>
        Component AsComponent { get; }

        /// <summary>
        /// Optional explicit key. If null/empty, SaveManager derives a deterministic key.
        /// Prefer to supply via [SaveKey] on the class; you may also return a value here.
        /// </summary>
        string? SaveKeyOverride => null;

        /// <summary>
        /// Hook invoked before snapshot capture.
        /// </summary>
        void OnBeforeSave() { }

        /// <summary>
        /// Hook invoked after snapshot application.
        /// </summary>
        void OnAfterLoad() { }
    }
}
