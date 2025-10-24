// com.bpg.aion/Runtime/Encryption/IKeyProvider.cs
#nullable enable
namespace BPG.Aion
{
    /// <summary>
    /// Provides symmetric keys for encryption. Keys are not stored in the save file.
    /// </summary>
    public interface IKeyProvider
    {
        /// <summary>
        /// Get encryption key bytes for the provided algorithm (e.g., "aes-gcm").
        /// </summary>
        byte[] GetKey(string algorithmName);
    }
}
