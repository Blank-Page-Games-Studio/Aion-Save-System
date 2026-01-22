// com.bpg.aion/Runtime/Bootstrap/AionSaveManagerFactoryOptions.cs
#nullable enable

namespace BPG.Aion
{
    /// <summary>
    /// Options for overriding default components when creating a SaveManager via the factory.
    /// All fields are nullable; when null, the factory uses defaults based on settings.
    /// </summary>
    public sealed class AionSaveManagerFactoryOptions
    {
        /// <summary>
        /// Override the default serializer (JsonSerializer).
        /// </summary>
        public ISerializer? SerializerOverride { get; set; }

        /// <summary>
        /// Override the default compressor (GZipCompressor when compression is enabled).
        /// </summary>
        public ICompressor? CompressorOverride { get; set; }

        /// <summary>
        /// Override the default encryptor (AesGcmEncryptor when encryption is enabled).
        /// </summary>
        public IEncryptor? EncryptorOverride { get; set; }

        /// <summary>
        /// Override the key provider used for encryption.
        /// Required when encryption is enabled and no default provider is registered.
        /// </summary>
        public IKeyProvider? KeyProviderOverride { get; set; }

        /// <summary>
        /// Override the default storage (FileSystemStorage).
        /// Note: Must be castable to FileSystemStorage for SaveManager compatibility.
        /// </summary>
        public FileSystemStorage? StorageOverride { get; set; }
    }
}
