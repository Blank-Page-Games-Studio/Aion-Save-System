// com.bpg.aion/Runtime/Config/AionSaveSettingsEffective.cs
#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Effective, validated settings computed from <see cref="AionSaveSettings"/>.
    /// </summary>
    public readonly struct AionSaveSettingsEffective
    {
        public string EffectiveProfileName { get; }
        public string EffectiveSaveFolderPath { get; }
        public int ChunkSizeBytes { get; }

        public bool CompressionEnabled { get; }
        public int CompressionThresholdBytes { get; }

        public bool EncryptionEnabled { get; }
        public string EncryptionSchemeId { get; }
        public string KeyProviderId { get; }

        public bool AutosaveEnabled { get; }
        public float AutosaveIntervalSeconds { get; }
        public int AutosaveMaxRollingBackups { get; }
        public bool AutosaveOnSceneChange { get; }
        public float SceneChangeDebounceSeconds { get; }

        private AionSaveSettingsEffective(
            string effectiveProfileName,
            string effectiveSaveFolderPath,
            int chunkSizeBytes,
            bool compressionEnabled,
            int compressionThresholdBytes,
            bool encryptionEnabled,
            string encryptionSchemeId,
            string keyProviderId,
            bool autosaveEnabled,
            float autosaveIntervalSeconds,
            int autosaveMaxRollingBackups,
            bool autosaveOnSceneChange,
            float sceneChangeDebounceSeconds)
        {
            EffectiveProfileName = effectiveProfileName;
            EffectiveSaveFolderPath = effectiveSaveFolderPath;
            ChunkSizeBytes = chunkSizeBytes;
            CompressionEnabled = compressionEnabled;
            CompressionThresholdBytes = compressionThresholdBytes;
            EncryptionEnabled = encryptionEnabled;
            EncryptionSchemeId = encryptionSchemeId;
            KeyProviderId = keyProviderId;
            AutosaveEnabled = autosaveEnabled;
            AutosaveIntervalSeconds = autosaveIntervalSeconds;
            AutosaveMaxRollingBackups = autosaveMaxRollingBackups;
            AutosaveOnSceneChange = autosaveOnSceneChange;
            SceneChangeDebounceSeconds = sceneChangeDebounceSeconds;
        }

        /// <summary>
        /// Computes effective settings from the provided asset. Does not mutate the asset.
        /// </summary>
        public static AionSaveSettingsEffective FromSettings(AionSaveSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var profileName = AionSaveSettings.NormalizeNonEmptyString(
                settings.DefaultProfileName,
                AionSaveSettings.DefaultProfileNameFallback);

            var relativeFolder = AionSaveSettings.NormalizeNonEmptyString(
                settings.RelativeSaveFolder,
                AionSaveSettings.DefaultRelativeSaveFolder);

            var chunkSize = AionSaveSettings.ClampChunkSizeBytes(settings.StreamingChunkSizeBytes);
            var compressionThreshold = AionSaveSettings.ClampCompressionThresholdBytes(settings.CompressionStreamingThresholdBytes);
            var autosaveInterval = AionSaveSettings.ClampAutosaveIntervalSeconds(settings.AutosaveIntervalSeconds);
            var autosaveMax = AionSaveSettings.ClampAutosaveMaxRollingBackups(settings.AutosaveMaxRollingBackups);
            var debounce = AionSaveSettings.ClampSceneChangeDebounceSeconds(settings.SceneChangeDebounceSeconds);

            var saveFolderPath = settings.UsePersistentDataPath
                ? Path.Combine(Application.persistentDataPath, relativeFolder)
                : relativeFolder;

            return new AionSaveSettingsEffective(
                profileName,
                saveFolderPath,
                chunkSize,
                settings.EnableCompression,
                compressionThreshold,
                settings.EnableEncryption,
                settings.EncryptionSchemeId ?? AionSaveSettings.DefaultEncryptionSchemeId,
                settings.KeyProviderId ?? AionSaveSettings.DefaultKeyProviderId,
                settings.EnableAutosave,
                autosaveInterval,
                autosaveMax,
                settings.AutosaveOnSceneChange,
                debounce);
        }
    }
}
