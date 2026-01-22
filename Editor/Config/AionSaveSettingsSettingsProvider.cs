// Assets/SaveSystem/Editor/Config/AionSaveSettingsSettingsProvider.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using BPG.Aion.Editor.Diagnostics;
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

        private const int KiB = 1024;
        private const int MiB = 1024 * 1024;

        private static readonly List<string> ByteUnitOptions = new List<string> { "Bytes", "KiB", "MiB" };

        private enum ByteUnit
        {
            Bytes,
            KiB,
            MiB
        }

        private sealed class ByteField
        {
            public VisualElement Root { get; }
            public IntegerField ValueField { get; }
            public PopupField<string> UnitField { get; }
            public Label PreviewLabel { get; }
            public string BindingPath { get; }
            public ByteUnit Unit { get; set; }
            public bool UnitInitialized { get; set; }
            public bool IsUpdating { get; set; }

            public ByteField(
                VisualElement root,
                IntegerField valueField,
                PopupField<string> unitField,
                Label previewLabel,
                string bindingPath)
            {
                Root = root;
                ValueField = valueField;
                UnitField = unitField;
                PreviewLabel = previewLabel;
                BindingPath = bindingPath;
            }
        }

        private VisualElement? _root;
        private HelpBox? _assetStatusHelpBox;
        private Button? _createOrLocateButton;
        private Button? _resetButton;
        private Button? _openFolderButton;
        private VisualElement? _settingsContainer;
        private Label? _effectivePreviewLabel;

        private Toggle? _enableCompressionToggle;
        private ByteField? _compressionThresholdField;
        private Toggle? _enableEncryptionToggle;
        private TextField? _encryptionSchemeField;
        private TextField? _keyProviderField;
        private Toggle? _enableAutosaveToggle;
        private FloatField? _autosaveIntervalField;
        private IntegerField? _autosaveRollingBackupsField;
        private Toggle? _autosaveOnSceneChangeToggle;
        private FloatField? _sceneChangeDebounceField;

        private ByteField? _chunkSizeField;

        private HelpBox? _defaultProfileEmptyWarning;
        private HelpBox? _defaultProfileSeparatorWarning;
        private HelpBox? _relativeFolderInvalidWarning;
        private HelpBox? _relativeFolderAbsoluteWarning;
        private HelpBox? _chunkSizeRoundingWarning;
        private HelpBox? _encryptionKeyWarning;

        private Foldout? _diagnosticsFoldout;
        private Toggle? _includePrefabsToggle;
        private Button? _runDiagnosticsButton;
        private Button? _fixCommonIssuesButton;
        private VisualElement? _diagnosticsResultsContainer;
        private List<ValidationMessage>? _lastValidationMessages;
        private ScanResult? _lastScanResult;

        private AionSaveSettings? _settings;
        private AionSaveSettings? _fallbackSettings;
        private SerializedObject? _serializedSettings;
        private bool _hasAsset;

        private ScrollView? _scrollView;
        private VisualElement? _scrollContent;


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
                    "Settings",
                    "Diagnostics"
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

            // Outer root padding kalsın, ama içerik ScrollView içinde aksın
            _scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
    {
        flexGrow = 1
    }
            };

            // ScrollView'in içerik root'u
            _scrollContent = new VisualElement
            {
                style =
                    {
                        flexGrow = 1,
                        paddingLeft = 0,
                        paddingRight = 0
                    }
            };

            _scrollView.Add(_scrollContent);
            _root.Add(_scrollView);

            var header = new Label("Aion Save System")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4
                }
            };
            _scrollContent.Add(header);

            _assetStatusHelpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                style =
                {
                    marginBottom = 6
                }
            };
            _scrollContent.Add(_assetStatusHelpBox);

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
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

            _scrollContent.Add(buttonRow);

            _settingsContainer = new VisualElement();
            _scrollContent.Add(_settingsContainer);

            var generalSection = CreateSection("General");
            generalSection.Add(new HelpBox(
                "Controls the default profile, base save folder, and streaming chunk size.",
                HelpBoxMessageType.Info));

            var defaultProfileField = CreateTextField("Default Profile Name", "_defaultProfileName");
            _defaultProfileEmptyWarning = CreateInlineHelpBox();
            _defaultProfileSeparatorWarning = CreateInlineHelpBox();

            var usePersistentField = CreateToggle("Use Persistent Data Path", "_usePersistentDataPath");
            var relativeFolderField = CreateTextField("Relative Save Folder", "_relativeSaveFolder");
            _relativeFolderInvalidWarning = CreateInlineHelpBox();
            _relativeFolderAbsoluteWarning = CreateInlineHelpBox();

            _chunkSizeField = CreateByteField("Streaming Chunk Size", "_streamingChunkSizeBytes");
            _chunkSizeRoundingWarning = CreateInlineHelpBox();

            generalSection.Add(defaultProfileField);
            generalSection.Add(_defaultProfileEmptyWarning);
            generalSection.Add(_defaultProfileSeparatorWarning);
            generalSection.Add(usePersistentField);
            generalSection.Add(relativeFolderField);
            generalSection.Add(_relativeFolderInvalidWarning);
            generalSection.Add(_relativeFolderAbsoluteWarning);
            generalSection.Add(_chunkSizeField.Root);
            generalSection.Add(_chunkSizeRoundingWarning);

            _settingsContainer.Add(generalSection);

            var compressionSection = CreateSection("Compression");
            compressionSection.Add(new HelpBox(
                "Compression reduces disk size at the cost of CPU. Threshold controls when it activates.",
                HelpBoxMessageType.Info));

            _enableCompressionToggle = CreateToggle("Enable Compression", "_enableCompression");
            _compressionThresholdField = CreateByteField("Compression Threshold", "_compressionStreamingThresholdBytes");

            compressionSection.Add(_enableCompressionToggle);
            compressionSection.Add(_compressionThresholdField.Root);

            _settingsContainer.Add(compressionSection);

            var encryptionSection = CreateSection("Encryption");
            encryptionSection.Add(new HelpBox(
                "Encryption requires a runtime key provider. Ensure KeyProviderId matches your IKeyProvider implementation.",
                HelpBoxMessageType.Warning));

            _enableEncryptionToggle = CreateToggle("Enable Encryption", "_enableEncryption");
            _encryptionSchemeField = CreateTextField("Encryption Scheme Id", "_encryptionSchemeId");
            _keyProviderField = CreateTextField("Key Provider Id", "_keyProviderId");
            _encryptionKeyWarning = CreateInlineHelpBox();

            encryptionSection.Add(_enableEncryptionToggle);
            encryptionSection.Add(_encryptionSchemeField);
            encryptionSection.Add(_keyProviderField);
            encryptionSection.Add(_encryptionKeyWarning);

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
            _scrollContent.Add(effectiveSection);

            _diagnosticsFoldout = CreateDiagnosticsSection();
            _scrollContent.Add(_diagnosticsFoldout);

            RegisterFieldCallbacks(defaultProfileField, usePersistentField, relativeFolderField);
            RegisterFieldCallbacks(
                _enableCompressionToggle,
                _enableEncryptionToggle,
                _encryptionSchemeField,
                _keyProviderField,
                _enableAutosaveToggle,
                _autosaveIntervalField,
                _autosaveRollingBackupsField,
                _autosaveOnSceneChangeToggle,
                _sceneChangeDebounceField);
        }

        private Foldout CreateDiagnosticsSection()
        {
            var section = CreateSection("Project Diagnostics");
            section.Add(new HelpBox(
                "Run diagnostics to validate project settings and scan saveables in open scenes.",
                HelpBoxMessageType.Info));

            var controlsRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 2,
                    marginBottom = 6
                }
            };

            _runDiagnosticsButton = new Button(OnRunDiagnostics)
            {
                text = "Run Diagnostics",
                style = { marginRight = 6 }
            };
            _fixCommonIssuesButton = new Button(OnFixCommonIssues)
            {
                text = "Fix Common Issues",
                style = { marginRight = 6 }
            };
            _includePrefabsToggle = new Toggle("Include Prefabs")
            {
                value = false,
                style = { marginTop = 2 }
            };

            controlsRow.Add(_runDiagnosticsButton);
            controlsRow.Add(_fixCommonIssuesButton);
            controlsRow.Add(_includePrefabsToggle);
            section.Add(controlsRow);

            _diagnosticsResultsContainer = new VisualElement
            {
                style = { marginTop = 2 }
            };
            section.Add(_diagnosticsResultsContainer);

            return section;
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

        private ByteField CreateByteField(string label, string bindingPath)
        {
            var root = new VisualElement
            {
                style =
                {
                    marginBottom = 4
                }
            };

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var valueField = new IntegerField(label)
            {
                style =
                {
                    flexGrow = 1
                }
            };
            valueField.isDelayed = true;

            var unitField = new PopupField<string>(ByteUnitOptions, 0)
            {
                style =
                {
                    width = 70,
                    marginLeft = 6
                }
            };

            row.Add(valueField);
            row.Add(unitField);

            var previewLabel = new Label
            {
                style =
                {
                    fontSize = 11,
                    marginLeft = 4,
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };

            root.Add(row);
            root.Add(previewLabel);

            var field = new ByteField(root, valueField, unitField, previewLabel, bindingPath)
            {
                Unit = ByteUnit.Bytes
            };

            valueField.RegisterValueChangedCallback(evt => OnByteFieldValueChanged(field, evt.newValue));
            unitField.RegisterValueChangedCallback(evt => OnByteFieldUnitChanged(field, evt.newValue));

            return field;
        }

        private static HelpBox CreateInlineHelpBox()
        {
            var box = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                style =
                {
                    marginTop = 2,
                    marginBottom = 2,
                    display = DisplayStyle.None
                }
            };

            return box;
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

            _scrollContent?.Unbind();
            _scrollContent?.Bind(_serializedSettings);

            _settingsContainer.SetEnabled(_hasAsset);
            _resetButton?.SetEnabled(_hasAsset);
            _fixCommonIssuesButton?.SetEnabled(_hasAsset);

            UpdateAssetStatus();
            UpdateConditionalStates();
            SyncByteFieldsFromSettings(true);
            UpdateInlineWarnings();
            UpdateEffectivePreview();
            RefreshDiagnosticsUI();
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

        private void OnFixCommonIssues()
        {
            if (_settings == null || !_hasAsset)
                return;

            Undo.RecordObject(_settings, "Fix Aion Save Settings");

            _settings.DefaultProfileName = _settings.DefaultProfileName?.Trim() ?? string.Empty;
            _settings.RelativeSaveFolder = _settings.RelativeSaveFolder?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_settings.KeyProviderId))
            {
                _settings.KeyProviderId = AionSaveSettings.DefaultKeyProviderId;
            }

            _settings.ValidateAndNormalize();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();

            OnSettingsChanged(false);
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

        private void OnRunDiagnostics()
        {
            if (_settings == null)
                return;

            _lastValidationMessages = AionSaveSettingsEditorValidator.Validate(_settings);
            var includePrefabs = _includePrefabsToggle != null && _includePrefabsToggle.value;
            _lastScanResult = AionSaveablesScanner.Scan(includePrefabs);

            RefreshDiagnosticsUI();
        }

        private void RefreshDiagnosticsUI()
        {
            if (_diagnosticsResultsContainer == null)
                return;

            _diagnosticsResultsContainer.Clear();

            if (_lastValidationMessages == null && _lastScanResult == null)
            {
                _diagnosticsResultsContainer.Add(new HelpBox(
                    "Diagnostics have not been run yet.",
                    HelpBoxMessageType.Info));
                return;
            }

            var settingsHeader = new Label("Settings Validation")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 2
                }
            };
            _diagnosticsResultsContainer.Add(settingsHeader);

            if (_lastValidationMessages == null || _lastValidationMessages.Count == 0)
            {
                _diagnosticsResultsContainer.Add(new HelpBox(
                    "No settings issues found.",
                    HelpBoxMessageType.Info));
            }
            else
            {
                foreach (var message in _lastValidationMessages)
                {
                    _diagnosticsResultsContainer.Add(CreateHelpBox(
                        message.Message,
                        ToHelpBoxMessageType(message.Severity),
                        null));
                }
            }

            var scanHeader = new Label("Saveables Scan")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 6,
                    marginBottom = 2
                }
            };
            _diagnosticsResultsContainer.Add(scanHeader);

            if (_lastScanResult == null)
            {
                _diagnosticsResultsContainer.Add(new HelpBox(
                    "Saveables scan has not been run yet.",
                    HelpBoxMessageType.Info));
                return;
            }

            var summary = new Label(
                $"Errors: {_lastScanResult.Errors} | Warnings: {_lastScanResult.Warnings} | Infos: {_lastScanResult.Infos}")
            {
                style =
                {
                    marginBottom = 4
                }
            };
            _diagnosticsResultsContainer.Add(summary);

            if (_lastScanResult.Items.Count == 0)
            {
                _diagnosticsResultsContainer.Add(new HelpBox(
                    "No saveables issues found.",
                    HelpBoxMessageType.Info));
                return;
            }

            foreach (var item in _lastScanResult.Items)
            {
                _diagnosticsResultsContainer.Add(CreateHelpBox(
                    item.Message,
                    ToHelpBoxMessageType(item.Severity),
                    item.Context));
            }
        }

        private static HelpBox CreateHelpBox(string message, HelpBoxMessageType type, UnityEngine.Object? context)
        {
            var box = new HelpBox(message, type)
            {
                style =
                {
                    marginBottom = 2
                }
            };

            if (context != null)
            {
                box.tooltip = "Click to select/ping the related object.";
                // Note: StyleCursor requires UIElements.Cursor, not MouseCursor enum
                // We use default cursor behavior and rely on tooltip for affordance
                box.RegisterCallback<MouseUpEvent>(_ =>
                {
                    Selection.activeObject = context;
                    EditorGUIUtility.PingObject(context);
                });
            }

            return box;
        }

        private void OnSettingsChanged(bool applySerialized = true)
        {
            if (applySerialized)
            {
                _serializedSettings?.ApplyModifiedProperties();
            }

            _serializedSettings?.Update();
            UpdateConditionalStates();
            SyncByteFieldsFromSettings(false);
            UpdateInlineWarnings();
            UpdateEffectivePreview();
        }

        private void OnByteFieldValueChanged(ByteField field, int value)
        {
            if (field.IsUpdating)
                return;

            ApplyByteField(field, value, field.Unit);
        }

        private void OnByteFieldUnitChanged(ByteField field, string unitLabel)
        {
            if (field.IsUpdating)
                return;

            field.Unit = ParseUnit(unitLabel);
            field.UnitInitialized = true;
            ApplyByteField(field, field.ValueField.value, field.Unit);
        }

        private void ApplyByteField(ByteField field, int value, ByteUnit unit)
        {
            if (_serializedSettings == null)
                return;

            _serializedSettings.Update();
            var property = _serializedSettings.FindProperty(field.BindingPath);
            if (property == null)
                return;

            property.intValue = ConvertToBytes(value, unit);
            _serializedSettings.ApplyModifiedProperties();
            OnSettingsChanged(false);
        }

        private void UpdateConditionalStates()
        {
            if (_enableCompressionToggle != null && _compressionThresholdField != null)
            {
                _compressionThresholdField.Root.SetEnabled(_enableCompressionToggle.value);
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

        private void SyncByteFieldsFromSettings(bool autoSelectUnit)
        {
            if (_settings == null)
                return;

            if (_chunkSizeField != null)
            {
                UpdateByteFieldFromSettings(_chunkSizeField, _settings.StreamingChunkSizeBytes, autoSelectUnit);
            }

            if (_compressionThresholdField != null)
            {
                UpdateByteFieldFromSettings(
                    _compressionThresholdField,
                    _settings.CompressionStreamingThresholdBytes,
                    autoSelectUnit);
            }
        }

        private void UpdateByteFieldFromSettings(ByteField field, int bytes, bool autoSelectUnit)
        {
            field.IsUpdating = true;

            if (!field.UnitInitialized && autoSelectUnit)
            {
                field.Unit = ChooseUnit(bytes);
                field.UnitField.SetValueWithoutNotify(UnitToLabel(field.Unit));
                field.UnitInitialized = true;
            }

            var valueInUnit = ConvertFromBytes(bytes, field.Unit);
            field.ValueField.SetValueWithoutNotify(valueInUnit);
            field.PreviewLabel.text = FormatBytesWithUnits(bytes);

            field.IsUpdating = false;
        }

        private void UpdateInlineWarnings()
        {
            HideHelpBox(_defaultProfileEmptyWarning);
            HideHelpBox(_defaultProfileSeparatorWarning);
            HideHelpBox(_relativeFolderInvalidWarning);
            HideHelpBox(_relativeFolderAbsoluteWarning);
            HideHelpBox(_chunkSizeRoundingWarning);
            HideHelpBox(_encryptionKeyWarning);

            if (_settings == null)
                return;

            var messages = AionSaveSettingsEditorValidator.Validate(_settings);
            foreach (var message in messages)
            {
                if (message.Message == AionSaveSettingsEditorValidator.DefaultProfileNameEmptyMessage)
                {
                    ShowHelpBox(_defaultProfileEmptyWarning, message);
                }
                else if (message.Message == AionSaveSettingsEditorValidator.DefaultProfileNameSeparatorMessage)
                {
                    ShowHelpBox(_defaultProfileSeparatorWarning, message);
                }
                else if (message.Message == AionSaveSettingsEditorValidator.RelativeFolderInvalidCharsMessage)
                {
                    ShowHelpBox(_relativeFolderInvalidWarning, message);
                }
                else if (message.Message.StartsWith(
                             AionSaveSettingsEditorValidator.RelativeFolderAbsoluteMessagePrefix,
                             StringComparison.Ordinal))
                {
                    ShowHelpBox(_relativeFolderAbsoluteWarning, message);
                }
                else if (message.Message == AionSaveSettingsEditorValidator.StreamingChunkSizeNotMultipleMessage)
                {
                    ShowHelpBox(_chunkSizeRoundingWarning, message);
                }
                else if (message.Message == AionSaveSettingsEditorValidator.EncryptionKeyMissingMessage)
                {
                    ShowHelpBox(_encryptionKeyWarning, message);
                }
            }
        }

        private static void HideHelpBox(HelpBox? box)
        {
            if (box == null)
                return;

            box.text = string.Empty;
            box.style.display = DisplayStyle.None;
        }

        private static void ShowHelpBox(HelpBox? box, ValidationMessage message)
        {
            if (box == null)
                return;

            box.messageType = ToHelpBoxMessageType(message.Severity);
            box.text = message.Message;
            box.style.display = DisplayStyle.Flex;
        }

        private void UpdateEffectivePreview()
        {
            if (_effectivePreviewLabel == null)
                return;

            var effective = GetEffectiveSettings();

            var chunkSize = FormatBytesWithUnits(effective.ChunkSizeBytes);
            var compressionThreshold = FormatBytesWithUnits(effective.CompressionThresholdBytes);

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

        private static string FormatBytesWithUnits(int bytes)
        {
            var kib = bytes / (float)KiB;
            var mib = bytes / (float)MiB;
            return $"{bytes} bytes ({kib:0.##} KiB / {mib:0.##} MiB)";
        }

        private static ByteUnit ChooseUnit(int bytes)
        {
            if (bytes >= MiB)
                return ByteUnit.MiB;
            if (bytes >= KiB)
                return ByteUnit.KiB;
            return ByteUnit.Bytes;
        }

        private static string UnitToLabel(ByteUnit unit)
        {
            switch (unit)
            {
                case ByteUnit.KiB:
                    return "KiB";
                case ByteUnit.MiB:
                    return "MiB";
                default:
                    return "Bytes";
            }
        }

        private static ByteUnit ParseUnit(string label)
        {
            return label switch
            {
                "KiB" => ByteUnit.KiB,
                "MiB" => ByteUnit.MiB,
                _ => ByteUnit.Bytes
            };
        }

        private static int ConvertToBytes(int value, ByteUnit unit)
        {
            var factor = unit switch
            {
                ByteUnit.KiB => KiB,
                ByteUnit.MiB => MiB,
                _ => 1
            };

            var bytes = (long)value * factor;
            if (bytes > int.MaxValue)
                return int.MaxValue;
            if (bytes < int.MinValue)
                return int.MinValue;
            return (int)bytes;
        }

        private static int ConvertFromBytes(int bytes, ByteUnit unit)
        {
            var factor = unit switch
            {
                ByteUnit.KiB => KiB,
                ByteUnit.MiB => MiB,
                _ => 1
            };

            return Mathf.RoundToInt(bytes / (float)factor);
        }

        private static HelpBoxMessageType ToHelpBoxMessageType(ValidationSeverity severity)
        {
            return severity switch
            {
                ValidationSeverity.Warning => HelpBoxMessageType.Warning,
                ValidationSeverity.Error => HelpBoxMessageType.Error,
                _ => HelpBoxMessageType.Info
            };
        }

        private static HelpBoxMessageType ToHelpBoxMessageType(ScanSeverity severity)
        {
            return severity switch
            {
                ScanSeverity.Warning => HelpBoxMessageType.Warning,
                ScanSeverity.Error => HelpBoxMessageType.Error,
                _ => HelpBoxMessageType.Info
            };
        }

        private void OnUndoRedo()
        {
            _serializedSettings?.Update();
            UpdateConditionalStates();
            SyncByteFieldsFromSettings(false);
            UpdateInlineWarnings();
            UpdateEffectivePreview();
        }
    }
}
