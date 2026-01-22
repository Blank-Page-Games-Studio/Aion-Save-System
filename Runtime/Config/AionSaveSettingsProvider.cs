// com.bpg.aion/Runtime/Config/AionSaveSettingsProvider.cs
#nullable enable
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Runtime-safe accessor for <see cref="AionSaveSettings"/>.
    /// Place a settings asset at Assets/Resources/AionSaveSettings.asset to use it at runtime.
    /// </summary>
    public static class AionSaveSettingsProvider
    {
        private const string ResourcesAssetPath = "AionSaveSettings";
        private static AionSaveSettings? _cached;

        public static AionSaveSettings Settings
        {
            get
            {
                if (_cached != null)
                    return _cached;

                _cached = Resources.Load<AionSaveSettings>(ResourcesAssetPath);
                if (_cached == null)
                {
                    _cached = ScriptableObject.CreateInstance<AionSaveSettings>();
                    _cached.ValidateAndNormalize();
                    _cached.hideFlags = HideFlags.DontSave;
                }

                return _cached;
            }
        }

        public static AionSaveSettingsEffective Effective => Settings.GetEffective();

        /// <summary>
        /// Clears the cached instance. Useful for tests or hot reload.
        /// </summary>
        public static void ClearCache()
        {
            _cached = null;
        }
    }
}
