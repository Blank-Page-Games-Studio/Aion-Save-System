// com.bpg.aion/Runtime/Bootstrap/AionAutosaveBootstrap.cs
#nullable enable
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Helper for configuring <see cref="AutosaveController"/> from project settings.
    /// </summary>
    public static class AionAutosaveBootstrap
    {
        /// <summary>
        /// Configures an AutosaveController instance using the provided effective settings.
        /// </summary>
        /// <param name="controller">The AutosaveController to configure.</param>
        /// <param name="effective">Effective settings computed from AionSaveSettings.</param>
        /// <param name="manager">The SaveManager instance to use for autosaves.</param>
        /// <param name="profile">Optional profile name override. If null, uses effective profile.</param>
        public static void Configure(
            AutosaveController controller,
            AionSaveSettingsEffective effective,
            SaveManager manager,
            string? profile = null)
        {
            if (controller == null)
            {
                Debug.LogError("[AionAutosaveBootstrap] Controller is null.");
                return;
            }

            if (manager == null)
            {
                Debug.LogError("[AionAutosaveBootstrap] SaveManager is null.");
                return;
            }

            var profileName = string.IsNullOrWhiteSpace(profile)
                ? effective.EffectiveProfileName
                : profile!;

            var options = new SaveOptions
            {
                UseCompression = effective.CompressionEnabled,
                UseEncryption = effective.EncryptionEnabled,
                ProfileName = profileName,
                Summary = "Autosave",
                AppVersion = Application.version
            };

            controller.Configure(
                manager: manager,
                options: options,
                profile: profileName,
                intervalSeconds: Mathf.RoundToInt(effective.AutosaveIntervalSeconds),
                maxRollingBackups: effective.AutosaveMaxRollingBackups,
                enabled: effective.AutosaveEnabled,
                onSceneChange: effective.AutosaveOnSceneChange
            );
        }

        /// <summary>
        /// Configures an AutosaveController using the singleton <see cref="AionSaveManagerProvider.Instance"/>
        /// and current effective settings.
        /// </summary>
        /// <param name="controller">The AutosaveController to configure.</param>
        /// <param name="profile">Optional profile name override.</param>
        public static void ConfigureWithDefaults(AutosaveController controller, string? profile = null)
        {
            var effective = AionSaveSettingsProvider.Effective;
            var manager = AionSaveManagerProvider.Instance;
            Configure(controller, effective, manager, profile);
        }
    }
}
