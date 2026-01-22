// Assets/SaveSystem/Editor/Config/AionSaveSettingsSettingsProvider.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BPG.Aion.Editor
{
    public sealed class AionSaveSettingsSettingsProvider : SettingsProvider
    {
        private const string ProviderPath = "Project/Aion Save System";
        private const string SettingsAssetPath = "Assets/Resources/AionSaveSettings.asset";
        private const string SettingsFolderPath = "Assets/Resources";

        private VisualElement? _root;
        private HelpBox? _assetStatusHelpBox;
        private Button? _createOrLocateButton;
        private Button? _resetButton;
        private Button? _openFolderButton;
        private VisualElement? _settingsContainer;
        private Label? _effectivePreviewLabel;

        private Toggle? _enableCompressionToggle;
        private IntegerField? _compressionThresholdField;
        private Toggle? _enableEncryptionToggle;
        private TextField? _encryptionSchemeField;
        private TextField? _keyProviderField;
        private Toggle? _enableAutosaveToggle;
        private FloatField? _autosaveIntervalField;
        private IntegerField? _autosaveRollingBackupsField;
        private Toggle? _autosaveOnSceneChangeToggle;
        private FloatField? _sceneChangeDebounceField;

        private AionSaveSettings? _settings;
        private AionSaveSettings? _fallbackSettings;
        private SerializedObject? _serializedSettings;
        private bool _hasAsset;

        public AionSaveSettingsSettingsProvider(string path, SettingsScope scope) : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            var provider = new AionSaveSettingsSettingsProvider(ProviderPath, SettingsScope.Project)
            {
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Aion",
                    "Save",
                    "Save System",
                    "Compression",
                    "Encryption",
                    "Autosave",
                    "Profile",
                    "Settings"
                }
            };

            return provider;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _root = rootElement;
            _root.style.paddingLeft = 8;
            _root.style.paddingRight = 8;
            _root.style.paddingTop = 6;
            _root.style.paddingBottom = 8;

            BuildUI();
            RefreshSettingsState();

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public override void OnDeactivate()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void BuildUI()
        {
            if (_root == null)
                return;

            _root.Clear();

            var header = new Label("Aion Save System")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4
                }
            };
            _root.Add(header);

            _assetStatusHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                style =
                {
                    marginBottom = 6
                }
            };
            _root.Add(_assetStatusHelpBox);

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 10
                }
            };

            _createOrLocateButton = new Button(OnCreateOrLocateAsset)
            {
                text = "Create/Locate Settings Asset",
                style = { marginRight = 6 }
            };
            buttonRow.Add(_createOrLocateButton);

            _resetButton = new Button(OnResetToDefaults)
            {
                text = "Reset to Defaults",
                style = { marginRight = 6 }
            };
            buttonRow.Add(_resetButton);

            _openFolderButton = new Button(OnOpenSaveFolder)
            {
                text = "Open Save Folder (Effective)"
            };
            buttonRow.Add(_openFolderButton);

            _root.Add(buttonRow);

            _settingsContainer = new VisualElement();
            _root.Add(_settingsContainer);

            var generalSection = CreateSection("General");
            generalSection.Add(new HelpBox(
                "Controls the default profile, base save folder, and streaming chunk size.",
                HelpBoxMessageType.Info));

            var defaultProfileField = CreateTextField("Default Profile Name", "_defaultProfileName");
            var usePersistentField = CreateToggle("Use Persistent Data Path", "_usePersistentDataPath");
            var relativeFolderField = CreateTextField("Relative Save Folder", "_relativeSaveFolder");
            var chunkSizeField = CreateIntegerField("Streaming Chunk Size (Bytes)", "_streamingChunkSizeBytes");

            generalSection.Add(defaultProfileField);
            generalSection.Add(usePersistentField);
            generalSection.Add(relativeFolderField);
            generalSection.Add(chunkSizeField);

            _settingsContainer.Add(generalSection);

            var compressionSection = CreateSection("Compression");
            compressionSection.Add(new HelpBox(
                "Compression reduces disk size at the cost of CPU. Threshold controls when it activates.",
                HelpBoxMessageType.Info));

            _enableCompressionToggle = CreateToggle("Enable Compression", "_enableCompression");
            _compressionThresholdField = CreateIntegerField("Compression Threshold (Bytes)", "_compressionStreamingThresholdBytes");

            compressionSection.Add(_enableCompressionToggle);
            compressionSection.Add(_compressionThresholdField);

            _settingsContainer.Add(compressionSection);

            var encryptionSection = CreateSection("Encryption");
            encryptionSection.Add(new HelpBox(
                "Encryption requires a runtime key provider. Ensure KeyProviderId matches your IKeyProvider implementation.",
                HelpBoxMessageType.Warning));

            _enableEncryptionToggle = CreateToggle("Enable Encryption", "_enableEncryption");
            _encryptionSchemeField = CreateTextField("Encryption Scheme Id", "_encryptionSchemeId");
            _keyProviderField = CreateTextField("Key Provider Id", "_keyProviderId");

            encryptionSection.Add(_enableEncryptionToggle);
            encryptionSection.Add(_encryptionSchemeField);
            encryptionSection.Add(_keyProviderField);

            _settingsContainer.Add(encryptionSection);

            var autosaveSection = CreateSection("Autosave");
            autosaveSection.Add(new HelpBox(
                "Autosave trades performance for safety. Tune interval and backups for your game.",
                HelpBoxMessageType.Info));

            _enableAutosaveToggle = CreateToggle("Enable Autosave", "_enableAutosave");
            _autosaveIntervalField = CreateFloatField("Autosave Interval (Seconds)", "_autosaveIntervalSeconds");
            _autosaveRollingBackupsField = CreateIntegerField("Max Rolling Backups", "_autosaveMaxRollingBackups");
            _autosaveOnSceneChangeToggle = CreateToggle("Autosave On Scene Change", "_autosaveOnSceneChange");
            _sceneChangeDebounceField = CreateFloatField("Scene Change Debounce (Seconds)", "_sceneChangeDebounceSeconds");

            autosaveSection.Add(_enableAutosaveToggle);
            autosaveSection.Add(_autosaveIntervalField);
            autosaveSection.Add(_autosaveRollingBackupsField);
            autosaveSection.Add(_autosaveOnSceneChangeToggle);
            autosaveSection.Add(_sceneChangeDebounceField);

            _settingsContainer.Add(autosaveSection);

            var effectiveSection = CreateSection("Effective Preview");
            _effectivePreviewLabel = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    marginTop = 4
                }
            };
            effectiveSection.Add(_effectivePreviewLabel);
            _root.Add(effectiveSection);

            RegisterFieldCallbacks(defaultProfileField, usePersistentField, relativeFolderField, chunkSizeField);
            RegisterFieldCallbacks(
                _enableCompressionToggle,
                _compressionThresholdField,
                _enableEncryptionToggle,
                _encryptionSchemeField,
                _keyProviderField,
                _enableAutosaveToggle,
                _autosaveIntervalField,
                _autosaveRollingBackupsField,
                _autosaveOnSceneChangeToggle,
                _sceneChangeDebounceField);
        }

        private static Foldout CreateSection(string title)
        {
            return new Foldout
            {
                text = title,
                value = true,
                style =
                {
                    marginTop = 6,
                    marginBottom = 6
                }
            };
        }

        private static TextField CreateTextField(string label, string bindingPath)
        {
            return new TextField(label)
            {
                bindingPath = bindingPath
            };
        }

        private static IntegerField CreateIntegerField(string label, string bindingPath)
        {
            return new IntegerField(label)
            {
                bindingPath = bindingPath
            };
        }

        private static FloatField CreateFloatField(string label, string bindingPath)
        {
            return new FloatField(label)
            {
                bindingPath = bindingPath
            };
        }

        private static Toggle CreateToggle(string label, string bindingPath)
        {
            return new Toggle(label)
            {
                bindingPath = bindingPath
            };
        }

        private void RegisterFieldCallbacks(params VisualElement?[] fields)
        {
            foreach (var field in fields)
            {
                if (field is null)
                    continue;

                switch (field)
                {
                    case Toggle toggle:
                        toggle.RegisterValueChangedCallback(_ => OnSettingsChanged());
                        break;
                    case TextField textField:
                        textField.RegisterValueChangedCallback(_ => OnSettingsChanged());
                        break;
                    case IntegerField integerField:
                        integerField.RegisterValueChangedCallback(_ => OnSettingsChanged());
                        break;
                    case FloatField floatField:
                        floatField.RegisterValueChangedCallback(_ => OnSettingsChanged());
                        break;
                }
            }
        }

        private void RefreshSettingsState()
        {
            if (_root == null || _settingsContainer == null)
                return;

            _settings = LoadSettings(out _hasAsset);
            _serializedSettings = new SerializedObject(_settings);

            _root.Unbind();
            _root.Bind(_serializedSettings);

            _settingsContainer.SetEnabled(_hasAsset);
            _resetButton?.SetEnabled(_hasAsset);

            UpdateAssetStatus();
            UpdateConditionalStates();
            UpdateEffectivePreview();
        }

        private AionSaveSettings LoadSettings(out bool hasAsset)
        {
            var settingsAsset = AssetDatabase.LoadAssetAtPath<AionSaveSettings>(SettingsAssetPath);
            if (settingsAsset != null)
            {
                hasAsset = true;
                return settingsAsset;
            }

            hasAsset = false;

            if (_fallbackSettings == null)
            {
                _fallbackSettings = ScriptableObject.CreateInstance<AionSaveSettings>();
                _fallbackSettings.ValidateAndNormalize();
                _fallbackSettings.hideFlags = HideFlags.DontSave;
            }

            return _fallbackSettings;
        }

        private void UpdateAssetStatus()
        {
            if (_assetStatusHelpBox == null)
                return;

            if (_hasAsset)
            {
                _assetStatusHelpBox.messageType = HelpBoxMessageType.Info;
                _assetStatusHelpBox.text = $"Settings asset found at {SettingsAssetPath}.";
            }
            else
            {
                _assetStatusHelpBox.messageType = HelpBoxMessageType.Warning;
                _assetStatusHelpBox.text =
                    $"Settings asset not found at {SettingsAssetPath}. Create it to persist changes.";
            }
        }

        private void OnCreateOrLocateAsset()
        {
            var settingsAsset = AssetDatabase.LoadAssetAtPath<AionSaveSettings>(SettingsAssetPath);
            if (settingsAsset != null)
            {
                Selection.activeObject = settingsAsset;
                EditorGUIUtility.PingObject(settingsAsset);
                RefreshSettingsState();
                return;
            }

            Directory.CreateDirectory(SettingsFolderPath);

            settingsAsset = ScriptableObject.CreateInstance<AionSaveSettings>();
            settingsAsset.ValidateAndNormalize();
            AssetDatabase.CreateAsset(settingsAsset, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = settingsAsset;
            EditorGUIUtility.PingObject(settingsAsset);

            RefreshSettingsState();
        }

        private void OnResetToDefaults()
        {
            if (_settings == null || !_hasAsset)
                return;

            Undo.RecordObject(_settings, "Reset Aion Save Settings");

            var defaults = ScriptableObject.CreateInstance<AionSaveSettings>();
            _settings.DefaultProfileName = defaults.DefaultProfileName;
            _settings.UsePersistentDataPath = defaults.UsePersistentDataPath;
            _settings.RelativeSaveFolder = defaults.RelativeSaveFolder;
            _settings.StreamingChunkSizeBytes = defaults.StreamingChunkSizeBytes;
            _settings.EnableCompression = defaults.EnableCompression;
            _settings.CompressionStreamingThresholdBytes = defaults.CompressionStreamingThresholdBytes;
            _settings.EnableEncryption = defaults.EnableEncryption;
            _settings.EncryptionSchemeId = defaults.EncryptionSchemeId;
            _settings.KeyProviderId = defaults.KeyProviderId;
            _settings.EnableAutosave = defaults.EnableAutosave;
            _settings.AutosaveIntervalSeconds = defaults.AutosaveIntervalSeconds;
            _settings.AutosaveMaxRollingBackups = defaults.AutosaveMaxRollingBackups;
            _settings.AutosaveOnSceneChange = defaults.AutosaveOnSceneChange;
            _settings.SceneChangeDebounceSeconds = defaults.SceneChangeDebounceSeconds;

            UnityEngine.Object.DestroyImmediate(defaults);

            _settings.ValidateAndNormalize();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            RefreshSettingsState();
        }

        private void OnOpenSaveFolder()
        {
            var effective = GetEffectiveSettings();
            if (string.IsNullOrWhiteSpace(effective.EffectiveSaveFolderPath))
                return;

            var resolvedPath = Path.GetFullPath(effective.EffectiveSaveFolderPath);

            try
            {
                if (!Directory.Exists(resolvedPath))
                    Directory.CreateDirectory(resolvedPath);

                EditorUtility.RevealInFinder(resolvedPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to open save folder '{resolvedPath}': {ex.Message}");
            }
        }

        private void OnSettingsChanged()
        {
            _serializedSettings?.ApplyModifiedProperties();
            UpdateConditionalStates();
            UpdateEffectivePreview();
        }

        private void UpdateConditionalStates()
        {
            if (_enableCompressionToggle != null && _compressionThresholdField != null)
            {
                _compressionThresholdField.SetEnabled(_enableCompressionToggle.value);
            }

            if (_enableEncryptionToggle != null && _encryptionSchemeField != null && _keyProviderField != null)
            {
                var enabled = _enableEncryptionToggle.value;
                _encryptionSchemeField.SetEnabled(enabled);
                _keyProviderField.SetEnabled(enabled);
            }

            if (_enableAutosaveToggle != null &&
                _autosaveIntervalField != null &&
                _autosaveRollingBackupsField != null &&
                _autosaveOnSceneChangeToggle != null &&
                _sceneChangeDebounceField != null)
            {
                var enabled = _enableAutosaveToggle.value;
                _autosaveIntervalField.SetEnabled(enabled);
                _autosaveRollingBackupsField.SetEnabled(enabled);
                _autosaveOnSceneChangeToggle.SetEnabled(enabled);
                _sceneChangeDebounceField.SetEnabled(enabled);
            }
        }

        private void UpdateEffectivePreview()
        {
            if (_effectivePreviewLabel == null)
                return;

            var effective = GetEffectiveSettings();

            var chunkSize = FormatBytesWithKiB(effective.ChunkSizeBytes);
            var compressionThreshold = FormatBytesWithKiB(effective.CompressionThresholdBytes);

            _effectivePreviewLabel.text =
                $"Profile: {effective.EffectiveProfileName}\n" +
                $"Save Folder: {effective.EffectiveSaveFolderPath}\n" +
                $"Chunk Size: {chunkSize}\n" +
                $"Compression: {(effective.CompressionEnabled ? "Enabled" : "Disabled")}, Threshold: {compressionThreshold}\n" +
                $"Encryption: {(effective.EncryptionEnabled ? "Enabled" : "Disabled")}, Scheme: {effective.EncryptionSchemeId}, Key Provider: {effective.KeyProviderId}\n" +
                $"Autosave: {(effective.AutosaveEnabled ? "Enabled" : "Disabled")}, " +
                $"Interval: {effective.AutosaveIntervalSeconds:0.##}s, " +
                $"Rolling Backups: {effective.AutosaveMaxRollingBackups}, " +
                $"On Scene Change: {(effective.AutosaveOnSceneChange ? "On" : "Off")}, " +
                $"Debounce: {effective.SceneChangeDebounceSeconds:0.##}s";
        }

        private AionSaveSettingsEffective GetEffectiveSettings()
        {
            if (_settings == null)
            {
                var defaults = ScriptableObject.CreateInstance<AionSaveSettings>();
                defaults.ValidateAndNormalize();
                var effective = defaults.GetEffective();
                UnityEngine.Object.DestroyImmediate(defaults);
                return effective;
            }

            return _settings.GetEffective();
        }

        private static string FormatBytesWithKiB(int bytes)
        {
            var kib = bytes / 1024f;
            return $"{bytes} bytes ({kib:0.##} KiB)";
        }

        private void OnUndoRedo()
        {
            _serializedSettings?.Update();
            UpdateConditionalStates();
            UpdateEffectivePreview();
        }
    }
}
