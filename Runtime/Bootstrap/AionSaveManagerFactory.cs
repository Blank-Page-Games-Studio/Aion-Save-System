// com.bpg.aion/Runtime/Bootstrap/AionSaveManagerFactory.cs
#nullable enable
using System;
using System.IO;

namespace BPG.Aion
{
    /// <summary>
    /// Factory that builds a configured <see cref="SaveManager"/> from <see cref="AionSaveSettingsEffective"/>.
    /// </summary>
    public static class AionSaveManagerFactory
    {
        /// <summary>
        /// Creates a new <see cref="SaveManager"/> configured according to project settings.
        /// </summary>
        /// <param name="options">Optional overrides for components. Pass null to use all defaults.</param>
        /// <returns>A fully configured SaveManager ready for use.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when encryption is enabled but no key provider can be resolved.
        /// </exception>
        public static SaveManager Create(AionSaveManagerFactoryOptions? options = null)
        {
            var effective = AionSaveSettingsProvider.Effective;
            options ??= new AionSaveManagerFactoryOptions();

            // 1. Serializer
            ISerializer serializer = options.SerializerOverride ?? new JsonSerializer();

            // 2. Storage
            FileSystemStorage storage = options.StorageOverride ?? CreateStorage(effective);

            // 3. Compressor (optional)
            ICompressor? compressor = null;
            if (effective.CompressionEnabled)
            {
                compressor = options.CompressorOverride ?? new GZipCompressor();
            }

            // 4. Encryptor + KeyProvider (optional)
            IEncryptor? encryptor = null;
            if (effective.EncryptionEnabled)
            {
                // Resolve key provider first
                IKeyProvider? keyProvider = options.KeyProviderOverride ?? ResolveKeyProvider(effective);

                if (keyProvider == null)
                {
                    throw new InvalidOperationException(
                        $"Encryption is enabled in settings but no IKeyProvider could be resolved. " +
                        $"KeyProviderId='{effective.KeyProviderId}'. " +
                        $"Either disable encryption in AionSaveSettings, provide a KeyProviderOverride in " +
                        $"AionSaveManagerFactoryOptions, or register a key provider with the expected ID.");
                }

                // Create encryptor with key provider
                encryptor = options.EncryptorOverride ?? CreateEncryptor(effective, keyProvider);
            }

            return new SaveManager(serializer, storage, compressor, encryptor);
        }

        /// <summary>
        /// Creates a FileSystemStorage with the appropriate save folder path.
        /// </summary>
        /// <remarks>
        /// FileSystemStorage combines its rootFolder param with Application.persistentDataPath.
        /// We pass the relative folder portion from settings.
        /// </remarks>
        private static FileSystemStorage CreateStorage(AionSaveSettingsEffective effective)
        {
            // FileSystemStorage already combines with persistentDataPath internally,
            // so we need the relative folder, not the full effective path.
            // The effective folder name is the last component of the path.
            var relativeSaveFolder = System.IO.Path.GetFileName(effective.EffectiveSaveFolderPath);
            
            // If the effective path equals persistentDataPath exactly, use default
            if (string.IsNullOrEmpty(relativeSaveFolder))
            {
                relativeSaveFolder = "AionSaves";
            }

            return new FileSystemStorage(relativeSaveFolder);
        }

        /// <summary>
        /// Attempts to resolve a key provider by ID. Returns null if not found.
        /// </summary>
        /// <remarks>
        /// Currently returns null for all IDs since there's no registry.
        /// Games should provide KeyProviderOverride in options or implement a registry pattern.
        /// </remarks>
        private static IKeyProvider? ResolveKeyProvider(AionSaveSettingsEffective effective)
        {
            // No default registry - key providers must be explicitly provided
            // This ensures encryption keys are never accidentally used without explicit setup
            return null;
        }

        /// <summary>
        /// Creates the appropriate encryptor based on scheme ID.
        /// </summary>
        private static IEncryptor CreateEncryptor(AionSaveSettingsEffective effective, IKeyProvider keyProvider)
        {
            // Currently only aes-gcm is supported
            if (string.Equals(effective.EncryptionSchemeId, "aes-gcm", StringComparison.OrdinalIgnoreCase))
            {
                return new AesGcmEncryptor(keyProvider);
            }

            throw new InvalidOperationException(
                $"Unknown encryption scheme: '{effective.EncryptionSchemeId}'. Supported: 'aes-gcm'.");
        }
    }
}
