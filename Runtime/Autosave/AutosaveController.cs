// com.bpg.aion/Runtime/Autosave/AutosaveController.cs
#nullable enable
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BPG.Aion
{
    /// <summary>
    /// Periodic autosave controller with rolling backups: slot_autosave_{k}.bpgsave.
    /// </summary>
    public sealed class AutosaveController : MonoBehaviour
    {
        [SerializeField] private bool _enabled = false;
        [SerializeField] private int _intervalSeconds = 60;
        [SerializeField] private int _maxRollingBackups = 3;
        [SerializeField] private string _profile = "Default";

        [SerializeField] private bool _onSceneChange = true;
        [SerializeField] private float _sceneChangeDebounceSeconds = 3f;
        [SerializeField] private string _summary = "Autosave";

        private float _timer;
        private float _sceneChangeTimer = -1f;
        private bool _busy;

        private SaveManager _manager = null!;
        private SaveOptions _options = new SaveOptions
        {
            UseCompression = true,
            UseEncryption = false,
            ProfileName = "Default",
            Summary = "Autosave"
        };

        public void Configure(SaveManager manager, SaveOptions options, string profile, int intervalSeconds, int maxRollingBackups, bool enabled, bool onSceneChange)
        {
            _manager = manager;
            _options = options;
            _profile = profile;
            _intervalSeconds = Mathf.Max(5, intervalSeconds);
            _maxRollingBackups = Mathf.Max(1, maxRollingBackups);
            _enabled = enabled;
            _onSceneChange = onSceneChange;
        }

        private void Awake()
        {
            _timer = 0f;
            if (_onSceneChange)
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (_onSceneChange)
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void Update()
        {
            if (!_enabled || _manager == null) return;

            _timer += Time.unscaledDeltaTime;

            if (_sceneChangeTimer >= 0f)
            {
                _sceneChangeTimer += Time.unscaledDeltaTime;
                if (_sceneChangeTimer >= _sceneChangeDebounceSeconds)
                {
                    _sceneChangeTimer = -1f;
                    TriggerAutosave();
                }
            }

            if (_timer >= _intervalSeconds)
            {
                _timer = 0f;
                TriggerAutosave();
            }
        }

        private void OnActiveSceneChanged(Scene a, Scene b)
        {
            if (!_onSceneChange) return;
            _sceneChangeTimer = 0f; // debounce
        }

        private void TriggerAutosave()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                // Rotate index: 0..max-1
                var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var index = (int)(t % _maxRollingBackups);

                // Explicit deep copy of SaveOptions for autosave
                var opts = new SaveOptions
                {
                    UseCompression = _options.UseCompression,
                    UseEncryption = _options.UseEncryption,
                    ProfileName = _profile,
                    Summary = _summary,
                    ContentType = _options.ContentType,
                    AppVersion = _options.AppVersion
                };

                _manager.SaveAutosave(index, opts);
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
