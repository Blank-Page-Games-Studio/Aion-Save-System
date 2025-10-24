// com.bpg.aion/Runtime/Interfaces/IStorageProvider.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Abstraction for durable storage of textual save data.
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>Write data to a path, replacing existing content.</summary>
        void Write(string path, string data);

        /// <summary>Read all text from path.</summary>
        string Read(string path);

        /// <summary>Delete the file if it exists.</summary>
        void Delete(string path);

        /// <summary>Whether a file exists at path.</summary>
        bool Exists(string path);

        /// <summary>Return the absolute file path for a logical slot.</summary>
        string PathForSlot(int slot);
    }
}
