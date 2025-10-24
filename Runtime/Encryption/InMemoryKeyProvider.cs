// com.bpg.aion/Runtime/Encryption/InMemoryKeyProvider.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Simple in-memory key provider for samples/tests.
    /// </summary>
    public sealed class InMemoryKeyProvider : IKeyProvider
    {
        private readonly byte[] _key;

        /// <summary>
        /// Provide a 32-byte key for AES-256-GCM.
        /// </summary>
        public InMemoryKeyProvider(byte[] key)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("AES-256-GCM requires a 32-byte key.", nameof(key));
            _key = (byte[])key.Clone();
        }

        public byte[] GetKey(string algorithmName) => (byte[])_key.Clone();
    }
}
