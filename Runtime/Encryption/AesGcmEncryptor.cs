// com.bpg.aion/Runtime/Encryption/AesGcmEncryptor.cs
#nullable enable
using System;
using System.Security.Cryptography;

namespace BPG.Aion
{
    /// <summary>
    /// AES-256-GCM encryptor using System.Security.Cryptography.AesGcm.
    /// Compatible with Unity’s .NET Standard profile.
    /// </summary>
    public sealed class AesGcmEncryptor : IEncryptor
    {
        private readonly IKeyProvider _keys;
        public string Name => "aes-gcm";

        public AesGcmEncryptor(IKeyProvider keyProvider)
        {
            _keys = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            // We cannot check AesGcm.IsSupported here (not defined on Unity’s API surface),
            // so runtime failures will be caught below and reported clearly.
        }

        public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, out byte[] tag)
        {
            try
            {
                var key = _keys.GetKey(Name);
                var ciphertext = new byte[plaintext.Length];
                tag = new byte[16];
                using var aes = new AesGcm(key);
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
                return ciphertext;
            }
            catch (NotSupportedException ex)
            {
                throw new PlatformNotSupportedException("AES-GCM encryption not supported on this platform/runtime.", ex);
            }
            catch (Exception ex)
            {
                throw new CryptographicException($"AES-GCM encryption failed: {ex.Message}", ex);
            }
        }

        public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> tag)
        {
            try
            {
                var key = _keys.GetKey(Name);
                var plaintext = new byte[ciphertext.Length];
                using var aes = new AesGcm(key);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
                return plaintext;
            }
            catch (NotSupportedException ex)
            {
                throw new PlatformNotSupportedException("AES-GCM decryption not supported on this platform/runtime.", ex);
            }
            catch (Exception ex)
            {
                throw new CryptographicException($"AES-GCM decryption failed: {ex.Message}", ex);
            }
        }
    }
}
