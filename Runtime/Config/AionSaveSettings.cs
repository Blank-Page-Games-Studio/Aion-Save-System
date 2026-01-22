// com.bpg.aion/Runtime/Config/AionSaveSettings.cs
#nullable enable
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Project-wide settings for the Aion save system. Values are validated on edit-time
    /// and normalized at runtime via <see cref="GetEffective"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "BPG/Aion/Save Settings", fileName = "AionSaveSettings")]
    public sealed class AionSaveSettings : ScriptableObject
    {
        private const int KiB = 1024;
        private const int MiB = 1024 * 1024;
        private const int ChunkSizeStepBytes = 4 * KiB;

        public const string DefaultProfileNameFallback = "Player1";
        public const string DefaultRelativeSaveFolder = "AionSaves";
        public const string DefaultEncryptionSchemeId = "aes-gcm";
        public const string DefaultKeyProviderId = "default";

        public const int MinChunkSizeBytes = 64 * KiB;
        public const int MaxChunkSizeBytes = 4 * MiB;

        public const int MinCompressionThresholdBytes = 1 * MiB;
        public const int MaxCompressionThresholdBytes = 1024 * MiB;

        public const float MinAutosaveIntervalSeconds = 5f;
        public const float MaxAutosaveIntervalSeconds = 3600f;

        public const int MinAutosaveRollingBackups = 1;
        public const int MaxAutosaveRollingBackups = 20;

        public const float MinSceneChangeDebounceSeconds = 0f;
        public const float MaxSceneChangeDebounceSeconds = 30f;

        [Header("General")]
        [SerializeField] private string _defaultProfileName = DefaultProfileNameFallback;
        [SerializeField] private bool _usePersistentDataPath = true;
        [SerializeField] private string _relativeSaveFolder = DefaultRelativeSaveFolder;
        [SerializeField] private int _streamingChunkSizeBytes = 256 * KiB;

        [Header("Compression")]
        [SerializeField] private bool _enableCompression = false;
        [SerializeField] private int _compressionStreamingThresholdBytes = 32 * MiB;

        [Header("Encryption")]
        [SerializeField] private bool _enableEncryption = false;
        [SerializeField] private string _encryptionSchemeId = DefaultEncryptionSchemeId;
        [SerializeField] private string _keyProviderId = DefaultKeyProviderId;

        [Header("Autosave")]
        [SerializeField] private bool _enableAutosave = true;
        [SerializeField] private float _autosaveIntervalSeconds = 60f;
        [SerializeField] private int _autosaveMaxRollingBackups = 3;
        [SerializeField] private bool _autosaveOnSceneChange = true;
        [SerializeField] private float _sceneChangeDebounceSeconds = 3f;

        /// <summary>
        /// Default save profile name. Trimmed and forced to a non-empty value.
        /// </summary>
        public string DefaultProfileName
        {
            get => _defaultProfileName;
            set => _defaultProfileName = value;
        }

        public bool UsePersistentDataPath
        {
            get => _usePersistentDataPath;
            set => _usePersistentDataPath = value;
        }

        /// <summary>
        /// Relative folder used for saves. Trimmed and forced to a non-empty value.
        /// </summary>
        public string RelativeSaveFolder
        {
            get => _relativeSaveFolder;
            set => _relativeSaveFolder = value;
        }

        /// <summary>
        /// Streaming chunk size in bytes. Rounded to nearest 4 KiB and clamped to [64 KiB, 4 MiB].
        /// </summary>
        public int StreamingChunkSizeBytes
        {
            get => _streamingChunkSizeBytes;
            set => _streamingChunkSizeBytes = value;
        }

        public bool EnableCompression
        {
            get => _enableCompression;
            set => _enableCompression = value;
        }

        /// <summary>
        /// Compression threshold in bytes. Clamped to [1 MiB, 1024 MiB].
        /// </summary>
        public int CompressionStreamingThresholdBytes
        {
            get => _compressionStreamingThresholdBytes;
            set => _compressionStreamingThresholdBytes = value;
        }

        public bool EnableEncryption
        {
            get => _enableEncryption;
            set => _enableEncryption = value;
        }

        public string EncryptionSchemeId
        {
            get => _encryptionSchemeId;
            set => _encryptionSchemeId = value;
        }

        public string KeyProviderId
        {
            get => _keyProviderId;
            set => _keyProviderId = value;
        }

        public bool EnableAutosave
        {
            get => _enableAutosave;
            set => _enableAutosave = value;
        }

        /// <summary>
        /// Autosave interval in seconds. Clamped to [5, 3600].
        /// </summary>
        public float AutosaveIntervalSeconds
        {
            get => _autosaveIntervalSeconds;
            set => _autosaveIntervalSeconds = value;
        }

        /// <summary>
        /// Maximum rolling autosave backups. Clamped to [1, 20].
        /// </summary>
        public int AutosaveMaxRollingBackups
        {
            get => _autosaveMaxRollingBackups;
            set => _autosaveMaxRollingBackups = value;
        }

        public bool AutosaveOnSceneChange
        {
            get => _autosaveOnSceneChange;
            set => _autosaveOnSceneChange = value;
        }

        /// <summary>
        /// Scene-change debounce in seconds. Clamped to [0, 30].
        /// </summary>
        public float SceneChangeDebounceSeconds
        {
            get => _sceneChangeDebounceSeconds;
            set => _sceneChangeDebounceSeconds = value;
        }

        /// <summary>
        /// Validates and normalizes settings in-place. Safe to call at runtime.
        /// </summary>
        public void ValidateAndNormalize()
        {
            _defaultProfileName = NormalizeNonEmptyString(_defaultProfileName, DefaultProfileNameFallback);
            _relativeSaveFolder = NormalizeNonEmptyString(_relativeSaveFolder, DefaultRelativeSaveFolder);
            _streamingChunkSizeBytes = ClampChunkSizeBytes(_streamingChunkSizeBytes);
            _compressionStreamingThresholdBytes = ClampCompressionThresholdBytes(_compressionStreamingThresholdBytes);
            _autosaveIntervalSeconds = ClampAutosaveIntervalSeconds(_autosaveIntervalSeconds);
            _autosaveMaxRollingBackups = ClampAutosaveMaxRollingBackups(_autosaveMaxRollingBackups);
            _sceneChangeDebounceSeconds = ClampSceneChangeDebounceSeconds(_sceneChangeDebounceSeconds);
        }

        /// <summary>
        /// Computes effective runtime settings without mutating the asset.
        /// </summary>
        public AionSaveSettingsEffective GetEffective()
        {
            return AionSaveSettingsEffective.FromSettings(this);
        }

        private void OnValidate()
        {
            ValidateAndNormalize();
        }

        internal static string NormalizeNonEmptyString(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var trimmed = value.Trim();
            return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
        }

        internal static int ClampChunkSizeBytes(int value)
        {
            var rounded = RoundToNearestStep(value, ChunkSizeStepBytes);
            return Mathf.Clamp(rounded, MinChunkSizeBytes, MaxChunkSizeBytes);
        }

        internal static int ClampCompressionThresholdBytes(int value)
        {
            return Mathf.Clamp(value, MinCompressionThresholdBytes, MaxCompressionThresholdBytes);
        }

        internal static float ClampAutosaveIntervalSeconds(float value)
        {
            return Mathf.Clamp(value, MinAutosaveIntervalSeconds, MaxAutosaveIntervalSeconds);
        }

        internal static int ClampAutosaveMaxRollingBackups(int value)
        {
            return Mathf.Clamp(value, MinAutosaveRollingBackups, MaxAutosaveRollingBackups);
        }

        internal static float ClampSceneChangeDebounceSeconds(float value)
        {
            return Mathf.Clamp(value, MinSceneChangeDebounceSeconds, MaxSceneChangeDebounceSeconds);
        }

        private static int RoundToNearestStep(int value, int step)
        {
            if (step <= 0)
                return value;

            return Mathf.RoundToInt(value / (float)step) * step;
        }
    }
}
