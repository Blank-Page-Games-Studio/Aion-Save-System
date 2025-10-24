// com.bpg.aion/Runtime/Encryption/IEncryptor.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Authenticated encryption abstraction (AEAD).
    /// </summary>
    public interface IEncryptor
    {
        /// <summary>Algorithm name (e.g., "aes-gcm").</summary>
        string Name { get; }

        /// <summary>
        /// Encrypt plaintext with AEAD.
        /// </summary>
        /// <param name="plaintext">Plain bytes.</param>
        /// <param name="nonce">Unique per save. 12 bytes recommended for AES-GCM.</param>
        /// <param name="aad">Additional Authenticated Data (not encrypted) to bind header.</param>
        /// <param name="tag">Returned authentication tag.</param>
        byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, out byte[] tag);

        /// <summary>
        /// Decrypt ciphertext with AEAD; throws on authentication failure.
        /// </summary>
        byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> tag);
    }
}
