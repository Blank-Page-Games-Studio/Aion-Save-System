#if UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BPG.Aion
{
    /// <summary>
    /// UI Toolkit-based inspector window for viewing save slot details.
    /// Part 3: header parsing + cached slot details.
    /// </summary>
    public sealed class AionSaveInspector : EditorWindow
    {
        private const string WindowTitle = "Aion Save Inspector";
        private const string DefaultRootFolderName = "BPG.Aion";
        private const int MaxNotesLength = 500;

        // NOTE: Unity only compiles assets under Assets/ or Packages/.
        // These paths mirror the requested com.bpg.aion structure under Assets/.
        private const string UxmlPath = "Assets/SaveSystem/Editor/AionSaveInspector/AionSaveInspector.uxml";
        private const string UssPath = "Assets/SaveSystem/Editor/AionSaveInspector/AionSaveInspector.uss";

        private readonly JsonSerializer _headerSerializer = new();
        private readonly Dictionary<string, SlotParsedData> _slotParsedCache = new(StringComparer.OrdinalIgnoreCase);

        private FileSystemStorage? _storage;

        private readonly List<SlotInfo> _allSlots = new();
        private readonly List<SlotInfo> _filteredSlots = new();
        private SlotInfo? _selectedSlot;

        private DropdownField _profileDropdown = null!;
        private ToolbarSearchField _searchField = null!;
        private ToolbarButton _refreshButton = null!;
        private ToolbarButton _openFolderButton = null!;

        private Label _slotPlaceholderLabel = null!;
        private ListView _slotList = null!;

        private Label _summaryPathValue = null!;
        private Label _summarySizeValue = null!;
        private Label _summaryProfileValue = null!;
        private Label _summaryCreatedValue = null!;
        private Label _summaryModifiedValue = null!;
        private Label _summaryCompressionValue = null!;
        private Label _summaryEncryptionValue = null!;
        private Label _summaryChecksumValue = null!;
        private Label _summarySlotNameValue = null!;
        private Label _summarySceneValue = null!;
        private Label _summaryPlaytimeValue = null!;
        private Label _summaryBuildValue = null!;
        private Label _summaryNotesValue = null!;

        private Button _actionRevealButton = null!;
        private Button _actionCopyPathButton = null!;
        private Label _statusLabel = null!;

        private Foldout _advancedFoldout = null!;
        private TextField _headerJsonField = null!;
        private TextField _metadataJsonField = null!;

        private int _statusMessageToken;

        [MenuItem("Window/BPG Aion/Save Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<AionSaveInspector>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(900f, 540f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("aion-root");

            // Try fixed paths first, then fall back to FindAssets by name.
            var tree = TryLoadUxml(UxmlPath, "AionSaveInspector");
            if (tree == null)
            {
                root.Add(new Label($"Unable to load UXML.\nTried path: {UxmlPath}\nAlso searched by name: AionSaveInspector"));
                Debug.LogError($"[Aion] Missing UXML. Tried path '{UxmlPath}' and FindAssets name 'AionSaveInspector'.");
                return;
            }

            var styleSheet = TryLoadUss(UssPath, "AionSaveInspector");
            if (styleSheet != null)
            {
                if (!root.styleSheets.Contains(styleSheet))
                    root.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[Aion] Missing USS. Tried path '{UssPath}' and FindAssets name 'AionSaveInspector'.");
            }

            tree.CloneTree(root);

            if (!CacheReferences(root))
                return;

            ConfigureSlotList();
            ConfigureActions();
            ConfigureAdvancedSection();

            InitializeStorage();
            ConfigureToolbar();
        }

        private static VisualTreeAsset? TryLoadUxml(string preferredPath, string assetBaseName)
        {
            // 1) Preferred path
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(preferredPath);
            if (asset != null) return asset;

            // 2) FindAssets fallback
            return FindSingleAssetByName<VisualTreeAsset>(assetBaseName);
        }

        private static StyleSheet? TryLoadUss(string preferredPath, string assetBaseName)
        {
            // 1) Preferred path
            var asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(preferredPath);
            if (asset != null) return asset;

            // 2) FindAssets fallback
            return FindSingleAssetByName<StyleSheet>(assetBaseName);
        }

        private static T? FindSingleAssetByName<T>(string assetBaseName) where T : UnityEngine.Object
        {
            // For UXML: VisualTreeAsset, for USS: StyleSheet
            var filter = $"t:{typeof(T).Name} {assetBaseName}";
            var guids = AssetDatabase.FindAssets(filter);

            if (guids == null || guids.Length == 0)
                return null;

            // Prefer exact filename match: "<assetBaseName>.<ext>"
            // If multiple matches exist, we pick the first "best" candidate.
            string? bestPath = null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, assetBaseName, StringComparison.OrdinalIgnoreCase))
                {
                    bestPath = path;
                    break;
                }

                // fallback candidate if no exact match found
                if (bestPath == null)
                    bestPath = path;
            }

            if (bestPath == null)
                return null;

            return AssetDatabase.LoadAssetAtPath<T>(bestPath);
        }

        private bool CacheReferences(VisualElement root)
        {
            var profileDropdown = root.Q<DropdownField>("profileDropdown");
            var searchField = root.Q<ToolbarSearchField>("searchField");
            var refreshButton = root.Q<ToolbarButton>("refreshButton");
            var openFolderButton = root.Q<ToolbarButton>("openFolderButton");

            var slotPlaceholderLabel = root.Q<Label>("slotPlaceholderLabel");
            var slotList = root.Q<ListView>("slotList");

            var summaryPathValue = root.Q<Label>("summaryPathValue");
            var summarySizeValue = root.Q<Label>("summarySizeValue");
            var summaryProfileValue = root.Q<Label>("summaryProfileValue");
            var summaryCreatedValue = root.Q<Label>("summaryCreatedValue");
            var summaryModifiedValue = root.Q<Label>("summaryModifiedValue");
            var summaryCompressionValue = root.Q<Label>("summaryCompressionValue");
            var summaryEncryptionValue = root.Q<Label>("summaryEncryptionValue");
            var summaryChecksumValue = root.Q<Label>("summaryChecksumValue");
            var summarySlotNameValue = root.Q<Label>("summarySlotNameValue");
            var summarySceneValue = root.Q<Label>("summarySceneValue");
            var summaryPlaytimeValue = root.Q<Label>("summaryPlaytimeValue");
            var summaryBuildValue = root.Q<Label>("summaryBuildValue");
            var summaryNotesValue = root.Q<Label>("summaryNotesValue");

            var actionRevealButton = root.Q<Button>("actionRevealButton");
            var actionCopyPathButton = root.Q<Button>("actionCopyPathButton");
            var statusLabel = root.Q<Label>("statusLabel");

            var advancedFoldout = root.Q<Foldout>("advancedFoldout");
            var headerJsonField = root.Q<TextField>("headerJsonField");
            var metadataJsonField = root.Q<TextField>("metadataJsonField");

            if (profileDropdown == null ||
                searchField == null ||
                refreshButton == null ||
                openFolderButton == null ||
                slotPlaceholderLabel == null ||
                slotList == null ||
                summaryPathValue == null ||
                summarySizeValue == null ||
                summaryProfileValue == null ||
                summaryCreatedValue == null ||
                summaryModifiedValue == null ||
                summaryCompressionValue == null ||
                summaryEncryptionValue == null ||
                summaryChecksumValue == null ||
                summarySlotNameValue == null ||
                summarySceneValue == null ||
                summaryPlaytimeValue == null ||
                summaryBuildValue == null ||
                summaryNotesValue == null ||
                actionRevealButton == null ||
                actionCopyPathButton == null ||
                statusLabel == null ||
                advancedFoldout == null ||
                headerJsonField == null ||
                metadataJsonField == null)
            {
                root.Add(new Label("AionSaveInspector UI is missing required elements."));
                Debug.LogError("[Aion] AionSaveInspector UXML is missing required elements.");
                return false;
            }

            _profileDropdown = profileDropdown;
            _searchField = searchField;
            _refreshButton = refreshButton;
            _openFolderButton = openFolderButton;

            _slotPlaceholderLabel = slotPlaceholderLabel;
            _slotList = slotList;

            _summaryPathValue = summaryPathValue;
            _summarySizeValue = summarySizeValue;
            _summaryProfileValue = summaryProfileValue;
            _summaryCreatedValue = summaryCreatedValue;
            _summaryModifiedValue = summaryModifiedValue;
            _summaryCompressionValue = summaryCompressionValue;
            _summaryEncryptionValue = summaryEncryptionValue;
            _summaryChecksumValue = summaryChecksumValue;
            _summarySlotNameValue = summarySlotNameValue;
            _summarySceneValue = summarySceneValue;
            _summaryPlaytimeValue = summaryPlaytimeValue;
            _summaryBuildValue = summaryBuildValue;
            _summaryNotesValue = summaryNotesValue;

            _actionRevealButton = actionRevealButton;
            _actionCopyPathButton = actionCopyPathButton;
            _statusLabel = statusLabel;

            _advancedFoldout = advancedFoldout;
            _headerJsonField = headerJsonField;
            _metadataJsonField = metadataJsonField;

            return true;
        }

        private void InitializeStorage()
        {
            try
            {
                var rootFolderName = ResolveRootFolderName();
                _storage = new FileSystemStorage(rootFolderName);
            }
            catch (Exception ex)
            {
                _storage = null;
                SetSummaryError($"Unable to initialize storage. {ex.Message}");
                Debug.LogError($"[Aion] Failed to initialize FileSystemStorage. {ex}");
            }
        }

        private static string ResolveRootFolderName()
        {
            try
            {
                var effective = AionSaveSettingsProvider.Effective;
                var folderName = Path.GetFileName(effective.EffectiveSaveFolderPath);
                return string.IsNullOrWhiteSpace(folderName) ? DefaultRootFolderName : folderName;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Aion] Failed to resolve save root folder from settings. Using '{DefaultRootFolderName}'. {ex.Message}");
                return DefaultRootFolderName;
            }
        }

        private void ConfigureToolbar()
        {
            _profileDropdown.RegisterValueChangedCallback(evt =>
            {
                Debug.Log($"[Aion] Profile selected: {evt.newValue}");
                RefreshSlotsForSelectedProfile();
            });

            _searchField.RegisterValueChangedCallback(evt =>
            {
                ApplySearchFilter(evt.newValue);
            });

            _refreshButton.clicked += () =>
            {
                Debug.Log("[Aion] Refresh clicked");
                RefreshSlotsForSelectedProfile();
            };

            _openFolderButton.clicked += HandleOpenFolderClicked;

            PopulateProfilesFromStorage();
        }

        private void PopulateProfilesFromStorage()
        {
            if (_storage == null)
            {
                _profileDropdown.choices = new List<string> { "Default" };
                _profileDropdown.SetValueWithoutNotify("Default");
                RefreshSlotsForSelectedProfile();
                return;
            }

            try
            {
                var profiles = _storage
                    .GetAllProfiles()
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (profiles.Count == 0)
                {
                    _storage.CreateProfile("Default");
                    profiles.Add("Default");
                }

                _profileDropdown.choices = profiles;

                var selectedProfile = profiles.Contains(_profileDropdown.value)
                    ? _profileDropdown.value
                    : profiles[0];

                _profileDropdown.SetValueWithoutNotify(selectedProfile);
                _searchField.SetValueWithoutNotify(string.Empty);

                RefreshSlotsForSelectedProfile();
            }
            catch (Exception ex)
            {
                SetSummaryError($"Failed to load profiles. {ex.Message}");
                Debug.LogError($"[Aion] Failed to populate profiles. {ex}");

                _profileDropdown.choices = new List<string> { "Default" };
                _profileDropdown.SetValueWithoutNotify("Default");
                RefreshSlotsForSelectedProfile();
            }
        }

        private void HandleOpenFolderClicked()
        {
            try
            {
                if (_storage == null)
                {
                    SetSummaryError("Storage is not available.");
                    return;
                }

                var profile = GetSelectedProfile();
                var profileDir = _storage.GetProfileDir(profile);
                Directory.CreateDirectory(profileDir);
                EditorUtility.RevealInFinder(profileDir);
            }
            catch (Exception ex)
            {
                SetSummaryError($"Unable to open folder. {ex.Message}");
                Debug.LogError($"[Aion] Open Folder failed. {ex}");
            }
        }

        private void ConfigureSlotList()
        {
            _slotList.selectionType = SelectionType.Single;
            _slotList.fixedItemHeight = 80f;
            _slotList.itemsSource = _filteredSlots;

            _slotList.makeItem = MakeSlotItem;
            _slotList.bindItem = BindSlotItem;

            _slotList.onSelectionChange += OnSlotSelectionChange;
        }

        private VisualElement MakeSlotItem()
        {
            var root = new VisualElement();
            root.AddToClassList("slot-card");

            var thumb = new VisualElement();
            thumb.AddToClassList("slot-thumb");
            root.Add(thumb);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("slot-text");

            var title = new Label { name = "slotTitle" };
            title.AddToClassList("slot-title");
            textColumn.Add(title);

            var subtitle = new Label { name = "slotSubtitle" };
            subtitle.AddToClassList("slot-subtitle");
            textColumn.Add(subtitle);

            root.Add(textColumn);

            var badgesColumn = new VisualElement();
            badgesColumn.AddToClassList("slot-badges");

            var badge1 = CreateBadgeLabel("badge1");
            var badge2 = CreateBadgeLabel("badge2");
            var badge3 = CreateBadgeLabel("badge3");
            var badge4 = CreateBadgeLabel("badge4");

            badgesColumn.Add(badge1);
            badgesColumn.Add(badge2);
            badgesColumn.Add(badge3);
            badgesColumn.Add(badge4);

            root.Add(badgesColumn);

            root.userData = new SlotItemRefs(title, subtitle, new[] { badge1, badge2, badge3, badge4 });
            return root;
        }

        private static Label CreateBadgeLabel(string name)
        {
            var badge = new Label { name = name };
            badge.AddToClassList("slot-badge");
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        private void BindSlotItem(VisualElement element, int index)
        {
            if (element.userData is not SlotItemRefs refs)
            {
                return;
            }

            var isSelected = _slotList.selectedIndex == index;
            element.EnableInClassList("slot-card--selected", isSelected);

            if (index < 0 || index >= _filteredSlots.Count)
            {
                refs.Title.text = string.Empty;
                refs.Subtitle.text = string.Empty;
                HideAllBadges(refs.Badges);
                return;
            }

            var slot = _filteredSlots[index];
            _slotParsedCache.TryGetValue(slot.FullPath, out var parsed);

            refs.Title.text = GetSlotTitle(slot, parsed);
            refs.Subtitle.text = BuildSlotSubtitle(slot, parsed, refs.SubtitleBuilder);

            BindSlotBadges(slot, parsed, refs.Badges);
        }

        private static string GetSlotTitle(SlotInfo slot, SlotParsedData? parsed)
        {
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.MetaSlotName))
            {
                return parsed.MetaSlotName!;
            }

            return slot.FileName;
        }

        private static string BuildSlotSubtitle(SlotInfo slot, SlotParsedData? parsed, StringBuilder builder)
        {
            builder.Clear();

            var hasPart = false;

            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.MetaSceneOrChapter))
            {
                builder.Append(parsed.MetaSceneOrChapter);
                hasPart = true;
            }

            if (parsed?.MetaPlaytime.HasValue == true)
            {
                if (hasPart)
                {
                    builder.Append(" • ");
                }

                builder.Append(FormatPlaytime(parsed.MetaPlaytime.Value));
                hasPart = true;
            }

            if (hasPart)
            {
                builder.Append(" • ");
            }

            builder.Append(FormatDateShort(slot.LastWriteTimeUtc));

            return builder.ToString();
        }

        private void BindSlotBadges(SlotInfo slot, SlotParsedData? parsed, IReadOnlyList<Label> badges)
        {
            HideAllBadges(badges);

            var badgeIndex = 0;

            void AddBadge(string text, bool isError = false)
            {
                if (badgeIndex >= badges.Count)
                {
                    return;
                }

                var badge = badges[badgeIndex++];
                badge.text = text;
                badge.style.display = DisplayStyle.Flex;

                ApplyBadgeStyle(badge, isError);
            }

            if (slot.HasMeta)
            {
                AddBadge("META");
            }

            if (parsed?.HeaderReadOk == true)
            {
                var compressBadge = FormatBadgeText(parsed.Compress);
                if (!string.IsNullOrEmpty(compressBadge))
                {
                    AddBadge(compressBadge);
                }

                var encryptBadge = FormatBadgeText(parsed.Encrypt);
                if (!string.IsNullOrEmpty(encryptBadge))
                {
                    AddBadge(encryptBadge);
                }
            }

            var hasError = parsed != null &&
                           (!string.IsNullOrWhiteSpace(parsed.ErrorMessage) ||
                            !parsed.HeaderReadOk ||
                            (slot.HasMeta && !parsed.MetaReadOk));
            if (hasError)
            {
                AddBadge("!", isError: true);
            }
        }

        private static void HideAllBadges(IReadOnlyList<Label> badges)
        {
            foreach (var badge in badges)
            {
                badge.text = string.Empty;
                badge.style.display = DisplayStyle.None;
                ApplyBadgeStyle(badge, isError: false);
            }
        }

        private static void ApplyBadgeStyle(Label badge, bool isError)
        {
            if (isError)
            {
                var border = new Color(0.85f, 0.32f, 0.32f, 0.95f);
                var background = new Color(0.45f, 0.18f, 0.18f, 0.85f);
                var text = new Color(1f, 0.92f, 0.92f, 0.98f);

                badge.style.borderTopColor = border;
                badge.style.borderRightColor = border;
                badge.style.borderBottomColor = border;
                badge.style.borderLeftColor = border;
                badge.style.backgroundColor = background;
                badge.style.color = text;
                return;
            }

            // Reset to the USS baseline for non-error badges.
            var normalBorder = new Color(0.63f, 0.63f, 0.63f, 0.4f);
            var normalBackground = new Color(0.31f, 0.31f, 0.31f, 0.28f);
            var normalText = new Color(0.92f, 0.92f, 0.92f, 0.95f);

            badge.style.borderTopColor = normalBorder;
            badge.style.borderRightColor = normalBorder;
            badge.style.borderBottomColor = normalBorder;
            badge.style.borderLeftColor = normalBorder;
            badge.style.backgroundColor = normalBackground;
            badge.style.color = normalText;
        }

        private static string FormatBadgeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            const int maxLen = 6;
            if (trimmed.Length > maxLen)
            {
                trimmed = trimmed.Substring(0, maxLen);
            }

            return trimmed.ToUpperInvariant();
        }

        private static string FormatDateShort(DateTime utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void ConfigureActions()
        {
            _actionRevealButton.clicked += RevealSelectedSlotInFinder;
            _actionCopyPathButton.clicked += CopySelectedSlotPath;

            SetActionsEnabled(false);
            ShowStatusInfo(string.Empty);
        }

        private void SetActionsEnabled(bool enabled)
        {
            _actionRevealButton.SetEnabled(enabled);
            _actionCopyPathButton.SetEnabled(enabled);
        }

        private void RevealSelectedSlotInFinder()
        {
            try
            {
                var slot = _selectedSlot;
                if (slot == null)
                {
                    ShowStatusError("Select a slot first.");
                    return;
                }

                if (!File.Exists(slot.FullPath))
                {
                    ShowStatusError("Slot file is missing on disk.");
                    return;
                }

                EditorUtility.RevealInFinder(slot.FullPath);
                ShowStatusInfo("Revealed in Finder.");
            }
            catch (Exception ex)
            {
                ShowStatusError("Failed to reveal slot.");
                Debug.LogError($"[Aion] Reveal in Finder failed for '{_selectedSlot?.FullPath}'. {ex}");
            }
        }

        private void CopySelectedSlotPath()
        {
            try
            {
                var slot = _selectedSlot;
                if (slot == null)
                {
                    ShowStatusError("Select a slot first.");
                    return;
                }

                EditorGUIUtility.systemCopyBuffer = slot.FullPath;
                ShowStatusInfo("Copied path to clipboard.");
            }
            catch (Exception ex)
            {
                ShowStatusError("Failed to copy path.");
                Debug.LogError($"[Aion] Copy path failed for '{_selectedSlot?.FullPath}'. {ex}");
            }
        }

        private void ShowStatusInfo(string message)
        {
            ShowStatus(message, isError: false);
        }

        private void ShowStatusError(string message)
        {
            ShowStatus(message, isError: true);
        }

        private void ShowStatus(string message, bool isError)
        {
            _statusMessageToken++;

            if (string.IsNullOrWhiteSpace(message))
            {
                _statusLabel.text = string.Empty;
                _statusLabel.style.display = DisplayStyle.None;
                _statusLabel.EnableInClassList("status-error", false);
                _statusLabel.EnableInClassList("status-info", true);
                return;
            }

            _statusLabel.text = message.Trim();
            _statusLabel.style.display = DisplayStyle.Flex;
            _statusLabel.EnableInClassList("status-error", isError);
            _statusLabel.EnableInClassList("status-info", !isError);
        }

        private void ConfigureAdvancedSection()
        {
            _advancedFoldout.value = false;

            ConfigureJsonField(_headerJsonField);
            ConfigureJsonField(_metadataJsonField);

            _headerJsonField.value = "{}";
            _metadataJsonField.value = "{}";
        }

        private static void ConfigureJsonField(TextField field)
        {
            field.multiline = true;
            field.isReadOnly = true;
            field.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }

        private void RefreshSlotsForSelectedProfile()
        {
            try
            {
                var previousPath = _selectedSlot?.FullPath;

                _selectedSlot = null;
                _slotList.ClearSelection();
                UpdateSelection(null);

                _allSlots.Clear();

                if (_storage == null)
                {
                    ApplySearchFilter(_searchField.value);
                    SetSummaryError("Storage is not available.");
                    return;
                }

                var profile = GetSelectedProfile();
                if (string.IsNullOrWhiteSpace(profile))
                {
                    ApplySearchFilter(_searchField.value);
                    return;
                }

                var discoveredSlots = DiscoverSlots(profile);
                _allSlots.AddRange(discoveredSlots);

                PruneSlotCacheForProfile(profile, _allSlots);
                ApplySearchFilter(_searchField.value);
                RestoreSelection(previousPath);
            }
            catch (Exception ex)
            {
                _allSlots.Clear();
                ApplySearchFilter(_searchField.value);
                SetSummaryError($"Failed to refresh slots. {ex.Message}");
                SetActionsEnabled(false);
                ShowStatusError("Refresh failed.");
                Debug.LogError($"[Aion] RefreshSlotsForSelectedProfile failed. {ex}");
            }
        }

        private void RestoreSelection(string? previousPath)
        {
            if (string.IsNullOrWhiteSpace(previousPath))
            {
                _slotList.RefreshItems();
                return;
            }

            var index = _filteredSlots.FindIndex(slot =>
                string.Equals(slot.FullPath, previousPath, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                _slotList.RefreshItems();
                return;
            }

            _slotList.SetSelection(index);
            _slotList.ScrollToItem(index);
            _slotList.RefreshItems();
        }

        private List<SlotInfo> DiscoverSlots(string profile)
        {
            var slots = new List<SlotInfo>();
            if (_storage == null)
            {
                return slots;
            }

            foreach (var (savePath, metaPath) in _storage.GetAllSlots(profile))
            {
                try
                {
                    var fileInfo = new FileInfo(savePath);
                    if (!fileInfo.Exists)
                    {
                        continue;
                    }

                    var slot = new SlotInfo(
                        profile: profile,
                        fileName: fileInfo.Name,
                        fullPath: fileInfo.FullName,
                        sizeBytes: fileInfo.Length,
                        lastWriteTimeUtc: fileInfo.LastWriteTimeUtc,
                        hasMeta: File.Exists(metaPath),
                        metaPath: metaPath);

                    slots.Add(slot);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Aion] Skipping slot due to error. Path='{savePath}'. {ex.Message}");
                }
            }

            slots.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return slots;
        }

        private void PruneSlotCacheForProfile(string profile, IReadOnlyCollection<SlotInfo> currentSlots)
        {
            if (_storage == null || currentSlots.Count == 0 && _slotParsedCache.Count == 0)
            {
                return;
            }

            string profileDir;
            try
            {
                profileDir = _storage.GetProfileDir(profile);
            }
            catch
            {
                return;
            }

            var validPaths = new HashSet<string>(currentSlots.Select(s => s.FullPath), StringComparer.OrdinalIgnoreCase);
            var keysToRemove = new List<string>();

            foreach (var key in _slotParsedCache.Keys)
            {
                if (IsPathUnderDirectory(key, profileDir) && !validPaths.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _slotParsedCache.Remove(key);
            }
        }

        private static bool IsPathUnderDirectory(string path, string directory)
        {
            try
            {
                var fullPath = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var fullDir = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return path.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ApplySearchFilter(string? rawSearchText)
        {
            var searchText = (rawSearchText ?? string.Empty).Trim();

            _filteredSlots.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                _filteredSlots.AddRange(_allSlots);
            }
            else
            {
                foreach (var slot in _allSlots)
                {
                    if (slot.FileName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _filteredSlots.Add(slot);
                    }
                }
            }

            _slotList.itemsSource = _filteredSlots;
            _slotList.Rebuild();

            if (_selectedSlot != null && !_filteredSlots.Contains(_selectedSlot))
            {
                _selectedSlot = null;
                _slotList.ClearSelection();
                UpdateSelection(null);
            }

            UpdateSlotListVisibility();
        }

        private void UpdateSlotListVisibility()
        {
            var hasItems = _filteredSlots.Count > 0;
            _slotPlaceholderLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            _slotList.style.display = hasItems ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private string GetSelectedProfile()
        {
            var profile = _profileDropdown.value;
            return string.IsNullOrWhiteSpace(profile) ? "Default" : profile.Trim();
        }

        private void OnSlotSelectionChange(IEnumerable<object> selection)
        {
            try
            {
                var slot = selection.OfType<SlotInfo>().FirstOrDefault();
                _selectedSlot = slot;
                UpdateSelection(slot);
                _slotList.RefreshItems();
            }
            catch (Exception ex)
            {
                SetSummaryError($"Selection error. {ex.Message}");
                SetActionsEnabled(false);
                ShowStatusError("Selection error.");
                _slotList.RefreshItems();
                Debug.LogError($"[Aion] Slot selection failed. {ex}");
            }
        }

        private void UpdateSelection(SlotInfo? slot)
        {
            if (slot == null)
            {
                SetSummaryPlaceholder();
                _headerJsonField.value = "{}";
                _metadataJsonField.value = "{}";
                SetActionsEnabled(false);
                ShowStatusInfo(string.Empty);
                _slotList.RefreshItems();
                return;
            }

            SetActionsEnabled(true);

            var parsed = GetOrParseSlot(slot);

            var profileText = !string.IsNullOrWhiteSpace(parsed.ProfileFromHeader)
                ? parsed.ProfileFromHeader!
                : slot.Profile;

            var createdText = parsed.CreatedUtc.HasValue
                ? FormatDate(parsed.CreatedUtc.Value)
                : "-";

            var modifiedUtc = parsed.ModifiedUtc ?? slot.LastWriteTimeUtc;
            var modifiedText = FormatDate(modifiedUtc);

            var slotNameText = !string.IsNullOrWhiteSpace(parsed.MetaSlotName)
                ? parsed.MetaSlotName!
                : slot.FileName;

            var sceneText = !string.IsNullOrWhiteSpace(parsed.MetaSceneOrChapter)
                ? parsed.MetaSceneOrChapter!
                : "-";

            var playtimeText = parsed.MetaPlaytime.HasValue
                ? FormatPlaytime(parsed.MetaPlaytime.Value)
                : "-";

            var buildText = !string.IsNullOrWhiteSpace(parsed.MetaBuild)
                ? parsed.MetaBuild!
                : "-";

            var notesText = BuildNotesSummary(slot, parsed);

            _summaryPathValue.text = slot.FullPath;
            _summarySizeValue.text = FormatBytes(slot.SizeBytes);
            _summaryProfileValue.text = profileText;
            _summaryCreatedValue.text = createdText;
            _summaryModifiedValue.text = modifiedText;
            _summaryCompressionValue.text = parsed.HeaderReadOk ? parsed.Compress : "unknown";
            _summaryEncryptionValue.text = parsed.HeaderReadOk ? parsed.Encrypt : "unknown";
            _summaryChecksumValue.text = parsed.Checksum ?? "-";
            _summarySlotNameValue.text = slotNameText;
            _summarySceneValue.text = sceneText;
            _summaryPlaytimeValue.text = playtimeText;
            _summaryBuildValue.text = buildText;
            _summaryNotesValue.text = notesText;

            _headerJsonField.value = BuildHeaderDisplayText(parsed);
            _metadataJsonField.value = parsed.MetadataJsonRaw;

            if (!string.IsNullOrWhiteSpace(parsed.ErrorMessage))
            {
                ShowStatusInfo("Slot parsed with warnings.");
            }
            else
            {
                ShowStatusInfo(string.Empty);
            }

            // Refresh visible items so cached header/meta info can enrich slot cards.
            _slotList.RefreshItems();
        }

        private SlotParsedData GetOrParseSlot(SlotInfo slot)
        {
            if (_slotParsedCache.TryGetValue(slot.FullPath, out var cached))
            {
                return cached;
            }

            var parsed = ParseSlot(slot);
            _slotParsedCache[slot.FullPath] = parsed;
            return parsed;
        }

        private SlotParsedData ParseSlot(SlotInfo slot)
        {
            var parsed = SlotParsedData.CreateDefault();
            var errors = new List<string>();

            if (TryReadHeaderLine(slot.FullPath, out var headerLine, out var headerError))
            {
                parsed.HeaderJsonRaw = headerLine;
                var populated = TryPopulateFromHeaderJson(headerLine, parsed, out var parseError);
                if (!populated)
                {
                    parsed.HeaderReadOk = false;
                    if (!string.IsNullOrWhiteSpace(parseError))
                    {
                        errors.Add(parseError!);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(parseError))
                {
                    // Fallback parse can succeed even if structured deserialization fails.
                    errors.Add(parseError!);
                }
            }
            else
            {
                parsed.HeaderReadOk = false;
                if (!string.IsNullOrWhiteSpace(headerError))
                {
                    errors.Add(headerError!);
                }
            }

            ReadMetadata(slot, parsed, errors);

            if (parsed.MetaReadOk && !string.Equals(parsed.MetadataJsonRaw, "(no metadata)", StringComparison.Ordinal))
            {
                var metaParsed = TryPopulateFromMetadataJson(parsed.MetadataJsonRaw, parsed, out var metaError);
                parsed.MetaParsedOk = metaParsed;
                if (!metaParsed && !string.IsNullOrWhiteSpace(metaError))
                {
                    errors.Add(metaError!);
                }
            }
            else
            {
                parsed.MetaParsedOk = false;
            }

            if (errors.Count > 0)
            {
                parsed.ErrorMessage = string.Join(Environment.NewLine, errors);
                Debug.LogWarning($"[Aion] Slot parse issue for '{slot.FullPath}': {parsed.ErrorMessage}");
            }

            return parsed;
        }

        private static bool TryReadHeaderLine(string savePath, out string headerLine, out string? error)
        {
            headerLine = string.Empty;
            error = null;

            try
            {
                if (!File.Exists(savePath))
                {
                    error = "Save file not found.";
                    return false;
                }

                using var stream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    error = "Header line is empty.";
                    return false;
                }

                headerLine = line;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryPopulateFromHeaderJson(string headerJson, SlotParsedData parsed, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(headerJson))
            {
                error = "Header JSON is empty.";
                return false;
            }

            if (TryDeserializeHeader(headerJson, parsed, out var deserializeError))
            {
                return true;
            }

            var fallbackWorked = TryPopulateFromHeaderFallback(headerJson, parsed, out var fallbackError);
            if (!fallbackWorked)
            {
                error = fallbackError ?? deserializeError ?? "Header parse failed.";
                return false;
            }

            // Fallback succeeded but we still surface deserialize issues for diagnostics.
            error = deserializeError;
            parsed.HeaderReadOk = true;
            return true;
        }

        private bool TryDeserializeHeader(string headerJson, SlotParsedData parsed, out string? error)
        {
            error = null;

            try
            {
                if (!LooksLikeHeaderJson(headerJson))
                {
                    error = "Header JSON is missing expected keys.";
                    return false;
                }

                var header = _headerSerializer.Deserialize<SaveHeader>(headerJson);
                MapFromSaveHeader(header, parsed);
                parsed.HeaderReadOk = true;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Deserialize failed: {ex.Message}";
                return false;
            }
        }

        private static bool LooksLikeHeaderJson(string headerJson)
        {
            return headerJson.IndexOf("\"CreatedUtc\"", StringComparison.Ordinal) >= 0 ||
                   headerJson.IndexOf("\"ModifiedUtc\"", StringComparison.Ordinal) >= 0 ||
                   headerJson.IndexOf("\"FormatId\"", StringComparison.Ordinal) >= 0;
        }

        private static void MapFromSaveHeader(SaveHeader header, SlotParsedData parsed)
        {
            parsed.ProfileFromHeader = string.IsNullOrWhiteSpace(header.Profile) ? null : header.Profile;
            parsed.CreatedUtc = TryParseUtc(header.CreatedUtc);
            parsed.ModifiedUtc = TryParseUtc(header.ModifiedUtc);
            parsed.Compress = string.IsNullOrWhiteSpace(header.Compress) ? "none" : header.Compress!;
            parsed.Encrypt = string.IsNullOrWhiteSpace(header.Encrypt) ? "none" : header.Encrypt!;
            parsed.Checksum = string.IsNullOrWhiteSpace(header.Checksum) ? null : header.Checksum;
        }

        private static bool TryPopulateFromHeaderFallback(string headerJson, SlotParsedData parsed, out string? error)
        {
            var errors = new List<string>();
            var extractedAny = false;

            var profileRaw = TryExtractJsonStringValue(headerJson, "Profile");
            if (!string.IsNullOrWhiteSpace(profileRaw))
            {
                parsed.ProfileFromHeader = profileRaw;
                extractedAny = true;
            }

            var createdRaw = TryExtractJsonStringValue(headerJson, "CreatedUtc");
            if (!string.IsNullOrWhiteSpace(createdRaw))
            {
                var createdUtc = TryParseUtc(createdRaw);
                if (createdUtc.HasValue)
                {
                    parsed.CreatedUtc = createdUtc;
                    extractedAny = true;
                }
                else
                {
                    errors.Add("Could not parse CreatedUtc.");
                }
            }

            var modifiedRaw = TryExtractJsonStringValue(headerJson, "ModifiedUtc");
            if (!string.IsNullOrWhiteSpace(modifiedRaw))
            {
                var modifiedUtc = TryParseUtc(modifiedRaw);
                if (modifiedUtc.HasValue)
                {
                    parsed.ModifiedUtc = modifiedUtc;
                    extractedAny = true;
                }
                else
                {
                    errors.Add("Could not parse ModifiedUtc.");
                }
            }

            var compressRaw = TryExtractJsonStringValue(headerJson, "Compress");
            if (!string.IsNullOrWhiteSpace(compressRaw))
            {
                parsed.Compress = compressRaw;
                extractedAny = true;
            }

            var encryptRaw = TryExtractJsonStringValue(headerJson, "Encrypt");
            if (!string.IsNullOrWhiteSpace(encryptRaw))
            {
                parsed.Encrypt = encryptRaw;
                extractedAny = true;
            }

            var checksumRaw = TryExtractJsonStringValue(headerJson, "Checksum");
            if (!string.IsNullOrWhiteSpace(checksumRaw))
            {
                parsed.Checksum = checksumRaw;
                extractedAny = true;
            }

            if (!extractedAny && errors.Count == 0)
            {
                errors.Add("Fallback parse could not extract known fields.");
            }

            error = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : null;
            return extractedAny;
        }

        private void ReadMetadata(SlotInfo slot, SlotParsedData parsed, List<string> errors)
        {
            if (!slot.HasMeta)
            {
                parsed.MetadataJsonRaw = "(no metadata)";
                parsed.MetaReadOk = true;
                return;
            }

            try
            {
                if (!File.Exists(slot.MetaPath))
                {
                    parsed.MetadataJsonRaw = "(no metadata)";
                    parsed.MetaReadOk = false;
                    errors.Add("Metadata file not found.");
                    return;
                }

                parsed.MetadataJsonRaw = File.ReadAllText(slot.MetaPath);
                parsed.MetaReadOk = true;
            }
            catch (Exception ex)
            {
                parsed.MetadataJsonRaw = $"Could not read metadata: {ex.Message}";
                parsed.MetaReadOk = false;
                errors.Add($"Metadata read failed: {ex.Message}");
            }
        }

        private bool TryPopulateFromMetadataJson(string rawMetaJson, SlotParsedData parsed, out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(rawMetaJson))
            {
                error = "Metadata JSON is empty.";
                return false;
            }

            if (TryPopulateFromMetadataJsonWithSystemTextJson(rawMetaJson, parsed, out var systemTextJsonError))
            {
                parsed.MetaParsedOk = true;
                return true;
            }

            var fallbackOk = TryPopulateFromMetadataJsonFallback(rawMetaJson, parsed, out var fallbackError);
            if (!fallbackOk)
            {
                error = fallbackError ?? systemTextJsonError ?? "Metadata parse failed.";
                parsed.MetaParsedOk = false;
                return false;
            }

            parsed.MetaParsedOk = true;
            return true;
        }

        private static bool TryPopulateFromMetadataJsonWithSystemTextJson(string rawMetaJson, SlotParsedData parsed, out string? error)
        {
            error = null;

            try
            {
                var jsonDocumentType = Type.GetType("System.Text.Json.JsonDocument, System.Text.Json");
                var jsonElementType = Type.GetType("System.Text.Json.JsonElement, System.Text.Json");
                if (jsonDocumentType == null || jsonElementType == null)
                {
                    return false;
                }

                var parseMethod = jsonDocumentType.GetMethod("Parse", new[] { typeof(string) });
                var rootElementProperty = jsonDocumentType.GetProperty("RootElement");
                var tryGetPropertyMethod = jsonElementType.GetMethod("TryGetProperty", new[] { typeof(string), jsonElementType.MakeByRefType() });
                var valueKindProperty = jsonElementType.GetProperty("ValueKind");
                var getStringMethod = jsonElementType.GetMethod("GetString", Type.EmptyTypes);
                var getRawTextMethod = jsonElementType.GetMethod("GetRawText", Type.EmptyTypes);
                var tryGetInt64Method = jsonElementType.GetMethod("TryGetInt64", new[] { typeof(long).MakeByRefType() });
                var tryGetDoubleMethod = jsonElementType.GetMethod("TryGetDouble", new[] { typeof(double).MakeByRefType() });

                if (parseMethod == null ||
                    rootElementProperty == null ||
                    tryGetPropertyMethod == null ||
                    valueKindProperty == null ||
                    getStringMethod == null ||
                    getRawTextMethod == null)
                {
                    return false;
                }

                var document = parseMethod.Invoke(null, new object[] { rawMetaJson });
                if (document == null)
                {
                    error = "System.Text.Json returned null document.";
                    return false;
                }

                try
                {
                    var rootElement = rootElementProperty.GetValue(document);
                    if (rootElement == null)
                    {
                        error = "Metadata root element is null.";
                        return false;
                    }

                    if (!IsJsonElementObject(rootElement, valueKindProperty))
                    {
                        error = "Metadata root element is not a JSON object.";
                        return false;
                    }

                    var extractedAny = false;

                    if (TryGetJsonElementString(rootElement, tryGetPropertyMethod, valueKindProperty, getStringMethod, getRawTextMethod,
                        out var slotName, "displayName", "slotName", "name", "title"))
                    {
                        parsed.MetaSlotName = slotName;
                        extractedAny = true;
                    }

                    if (TryGetJsonElementString(rootElement, tryGetPropertyMethod, valueKindProperty, getStringMethod, getRawTextMethod,
                        out var scene, "scene", "chapter", "level", "checkpoint"))
                    {
                        parsed.MetaSceneOrChapter = scene;
                        extractedAny = true;
                    }

                    if (TryGetJsonElementNumber(rootElement, tryGetPropertyMethod, valueKindProperty, tryGetInt64Method, tryGetDoubleMethod, getRawTextMethod,
                        out var seconds, "playtimeSeconds", "playtime", "playTimeSeconds", "secondsPlayed"))
                    {
                        if (seconds >= 0d && seconds <= TimeSpan.MaxValue.TotalSeconds)
                        {
                            parsed.MetaPlaytime = TimeSpan.FromSeconds(seconds);
                            extractedAny = true;
                        }
                    }
                    else if (TryGetJsonElementString(rootElement, tryGetPropertyMethod, valueKindProperty, getStringMethod, getRawTextMethod,
                        out var playtimeRaw, "playtimeSeconds", "playtime", "playTimeSeconds", "secondsPlayed"))
                    {
                        if (TryParsePlaytime(playtimeRaw, out var playtime))
                        {
                            parsed.MetaPlaytime = playtime;
                            extractedAny = true;
                        }
                    }

                    if (TryGetJsonElementString(rootElement, tryGetPropertyMethod, valueKindProperty, getStringMethod, getRawTextMethod,
                        out var build, "build", "buildVersion", "version", "appVersion"))
                    {
                        parsed.MetaBuild = build;
                        extractedAny = true;
                    }

                    if (TryGetJsonElementString(rootElement, tryGetPropertyMethod, valueKindProperty, getStringMethod, getRawTextMethod,
                        out var notes, "notes", "comment", "description"))
                    {
                        parsed.MetaNotes = notes;
                        extractedAny = true;
                    }

                    if (!extractedAny)
                    {
                        error = "System.Text.Json did not find recognized metadata fields.";
                        return false;
                    }

                    return true;
                }
                finally
                {
                    if (document is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                error = $"System.Text.Json parse failed: {ex.Message}";
                return false;
            }
        }

        private static bool IsJsonElementObject(object element, System.Reflection.PropertyInfo valueKindProperty)
        {
            var valueKind = valueKindProperty.GetValue(element);
            return string.Equals(valueKind?.ToString(), "Object", StringComparison.Ordinal);
        }

        private static bool TryGetJsonElementString(
            object rootElement,
            System.Reflection.MethodInfo tryGetPropertyMethod,
            System.Reflection.PropertyInfo valueKindProperty,
            System.Reflection.MethodInfo getStringMethod,
            System.Reflection.MethodInfo getRawTextMethod,
            out string value,
            params string[] keys)
        {
            value = string.Empty;

            foreach (var key in keys)
            {
                if (!TryGetJsonElementProperty(rootElement, tryGetPropertyMethod, key, out var propertyElement))
                {
                    continue;
                }

                var kind = valueKindProperty.GetValue(propertyElement)?.ToString();
                if (string.Equals(kind, "String", StringComparison.Ordinal))
                {
                    var str = getStringMethod.Invoke(propertyElement, null) as string;
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        value = str.Trim();
                        return true;
                    }
                }
                else if (string.Equals(kind, "Number", StringComparison.Ordinal) ||
                         string.Equals(kind, "True", StringComparison.Ordinal) ||
                         string.Equals(kind, "False", StringComparison.Ordinal))
                {
                    var raw = getRawTextMethod.Invoke(propertyElement, null) as string;
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        value = raw.Trim().Trim('"');
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetJsonElementNumber(
            object rootElement,
            System.Reflection.MethodInfo tryGetPropertyMethod,
            System.Reflection.PropertyInfo valueKindProperty,
            System.Reflection.MethodInfo? tryGetInt64Method,
            System.Reflection.MethodInfo? tryGetDoubleMethod,
            System.Reflection.MethodInfo getRawTextMethod,
            out double value,
            params string[] keys)
        {
            value = 0d;

            foreach (var key in keys)
            {
                if (!TryGetJsonElementProperty(rootElement, tryGetPropertyMethod, key, out var propertyElement))
                {
                    continue;
                }

                var kind = valueKindProperty.GetValue(propertyElement)?.ToString();
                if (!string.Equals(kind, "Number", StringComparison.Ordinal))
                {
                    continue;
                }

                if (tryGetInt64Method != null)
                {
                    var intArgs = new object[] { 0L };
                    var intOk = (bool)tryGetInt64Method.Invoke(propertyElement, intArgs);
                    if (intOk)
                    {
                        value = (double)(long)intArgs[0];
                        return true;
                    }
                }

                if (tryGetDoubleMethod != null)
                {
                    var dblArgs = new object[] { 0d };
                    var dblOk = (bool)tryGetDoubleMethod.Invoke(propertyElement, dblArgs);
                    if (dblOk)
                    {
                        value = (double)dblArgs[0];
                        return true;
                    }
                }

                var raw = getRawTextMethod.Invoke(propertyElement, null) as string;
                if (!string.IsNullOrWhiteSpace(raw) &&
                    double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetJsonElementProperty(object rootElement, System.Reflection.MethodInfo tryGetPropertyMethod, string key, out object propertyElement)
        {
            var args = new object?[] { key, null };
            var ok = (bool)tryGetPropertyMethod.Invoke(rootElement, args);
            if (ok && args[1] != null)
            {
                propertyElement = args[1]!;
                return true;
            }

            propertyElement = rootElement;
            return false;
        }

        private static bool TryPopulateFromMetadataJsonFallback(string rawMetaJson, SlotParsedData parsed, out string? error)
        {
            var extractedAny = false;

            if (TryExtractFirstJsonValue(rawMetaJson, out var slotName, "displayName", "slotName", "name", "title"))
            {
                parsed.MetaSlotName = slotName;
                extractedAny = true;
            }

            if (TryExtractFirstJsonValue(rawMetaJson, out var scene, "scene", "chapter", "level", "checkpoint"))
            {
                parsed.MetaSceneOrChapter = scene;
                extractedAny = true;
            }

            if (TryExtractFirstJsonValue(rawMetaJson, out var playtimeRaw, "playtimeSeconds", "playtime", "playTimeSeconds", "secondsPlayed"))
            {
                if (TryParsePlaytime(playtimeRaw, out var playtime))
                {
                    parsed.MetaPlaytime = playtime;
                    extractedAny = true;
                }
            }

            if (TryExtractFirstJsonValue(rawMetaJson, out var build, "build", "buildVersion", "version", "appVersion"))
            {
                parsed.MetaBuild = build;
                extractedAny = true;
            }

            if (TryExtractFirstJsonValue(rawMetaJson, out var notes, "notes", "comment", "description"))
            {
                parsed.MetaNotes = notes;
                extractedAny = true;
            }

            if (!extractedAny)
            {
                error = "No recognized metadata fields were found.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryExtractFirstJsonValue(string json, out string value, params string[] keys)
        {
            foreach (var key in keys)
            {
                var candidate = TryExtractJsonStringValue(json, key);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    value = candidate.Trim();
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool TryParsePlaytime(string rawValue, out TimeSpan playtime)
        {
            if (TimeSpan.TryParse(rawValue, CultureInfo.InvariantCulture, out playtime))
            {
                return true;
            }

            if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                if (seconds >= 0d && seconds <= TimeSpan.MaxValue.TotalSeconds)
                {
                    playtime = TimeSpan.FromSeconds(seconds);
                    return true;
                }
            }

            playtime = default;
            return false;
        }

        private static DateTime? TryParseUtc(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            }

            return null;
        }

        private static string? TryExtractJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            var keyToken = $"\"{key}\"";
            var keyIndex = json.IndexOf(keyToken, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return null;
            }

            var colonIndex = json.IndexOf(':', keyIndex + keyToken.Length);
            if (colonIndex < 0)
            {
                return null;
            }

            var index = colonIndex + 1;
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            if (index >= json.Length)
            {
                return null;
            }

            if (json[index] == '"')
            {
                index++;
                var builder = new StringBuilder();
                var escaped = false;

                for (; index < json.Length; index++)
                {
                    var c = json[index];
                    if (escaped)
                    {
                        builder.Append(UnescapeJsonChar(c));
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        break;
                    }

                    builder.Append(c);
                }

                return builder.ToString();
            }

            var start = index;
            while (index < json.Length && json[index] != ',' && json[index] != '}' && !char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            if (index <= start)
            {
                return null;
            }

            return json.Substring(start, index - start).Trim().Trim('"');
        }

        private static char UnescapeJsonChar(char c)
        {
            return c switch
            {
                '"' => '"',
                '\\' => '\\',
                '/' => '/',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => c
            };
        }

        private static string BuildHeaderDisplayText(SlotParsedData parsed)
        {
            if (parsed.HeaderReadOk)
            {
                return parsed.HeaderJsonRaw;
            }

            if (!string.IsNullOrWhiteSpace(parsed.HeaderJsonRaw))
            {
                if (string.IsNullOrWhiteSpace(parsed.ErrorMessage))
                {
                    return parsed.HeaderJsonRaw;
                }

                return parsed.HeaderJsonRaw + Environment.NewLine + Environment.NewLine +
                       $"Could not fully parse header: {parsed.ErrorMessage}";
            }

            var error = string.IsNullOrWhiteSpace(parsed.ErrorMessage) ? "unknown error" : parsed.ErrorMessage;
            return $"Could not read header: {error}";
        }

        private static string BuildNotesSummary(SlotInfo slot, SlotParsedData parsed)
        {
            var notes = parsed.MetaNotes;
            if (string.IsNullOrWhiteSpace(notes))
            {
                if (slot.HasMeta && !parsed.MetaReadOk)
                {
                    notes = "Metadata unreadable.";
                }
                else if (slot.HasMeta && parsed.MetaReadOk && !parsed.MetaParsedOk)
                {
                    notes = "Metadata parse failed.";
                }
                else
                {
                    notes = "-";
                }
            }

            return ClampSummaryText(notes, MaxNotesLength);
        }

        private static string ClampSummaryText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            var safeLength = Math.Max(0, maxLength - 3);
            return text.Substring(0, safeLength).TrimEnd() + "...";
        }

        private void SetSummaryPlaceholder()
        {
            const string placeholder = "-";
            _summaryPathValue.text = placeholder;
            _summarySizeValue.text = placeholder;
            _summaryProfileValue.text = placeholder;
            _summaryCreatedValue.text = placeholder;
            _summaryModifiedValue.text = placeholder;
            _summaryCompressionValue.text = placeholder;
            _summaryEncryptionValue.text = placeholder;
            _summaryChecksumValue.text = placeholder;
            _summarySlotNameValue.text = placeholder;
            _summarySceneValue.text = placeholder;
            _summaryPlaytimeValue.text = placeholder;
            _summaryBuildValue.text = placeholder;
            _summaryNotesValue.text = placeholder;
        }

        private void SetSummaryError(string message)
        {
            var errorText = $"ERROR: {message}";
            _summaryPathValue.text = errorText;
            _summarySizeValue.text = "-";
            _summaryProfileValue.text = GetSelectedProfile();
            _summaryCreatedValue.text = "-";
            _summaryModifiedValue.text = "-";
            _summaryCompressionValue.text = "-";
            _summaryEncryptionValue.text = "-";
            _summaryChecksumValue.text = "-";
            _summarySlotNameValue.text = "-";
            _summarySceneValue.text = "-";
            _summaryPlaytimeValue.text = "-";
            _summaryBuildValue.text = "-";
            _summaryNotesValue.text = "-";

            _headerJsonField.value = "{}";
            _metadataJsonField.value = "{}";
        }

        private static string FormatPlaytime(TimeSpan playtime)
        {
            var totalHours = (long)playtime.TotalHours;
            return $"{totalHours:00}:{playtime.Minutes:00}:{playtime.Seconds:00}";
        }

        private static string FormatDate(DateTime utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static string FormatBytes(long sizeBytes)
        {
            if (sizeBytes < 1024) return $"{sizeBytes} B";
            if (sizeBytes < 1024 * 1024) return $"{sizeBytes / 1024f:0.0} KB";
            if (sizeBytes < 1024L * 1024L * 1024L) return $"{sizeBytes / (1024f * 1024f):0.00} MB";
            return $"{sizeBytes / (1024f * 1024f * 1024f):0.00} GB";
        }

        private sealed class SlotItemRefs
        {
            public Label Title { get; }
            public Label Subtitle { get; }
            public IReadOnlyList<Label> Badges { get; }
            public StringBuilder SubtitleBuilder { get; } = new(128);

            public SlotItemRefs(Label title, Label subtitle, IReadOnlyList<Label> badges)
            {
                Title = title;
                Subtitle = subtitle;
                Badges = badges;
            }
        }

        private sealed class SlotInfo
        {
            public string Profile { get; }
            public string FileName { get; }
            public string FullPath { get; }
            public long SizeBytes { get; }
            public DateTime LastWriteTimeUtc { get; }
            public bool HasMeta { get; }
            public string MetaPath { get; }

            public string DisplayName => FileName;

            public SlotInfo(
                string profile,
                string fileName,
                string fullPath,
                long sizeBytes,
                DateTime lastWriteTimeUtc,
                bool hasMeta,
                string metaPath)
            {
                Profile = profile;
                FileName = fileName;
                FullPath = fullPath;
                SizeBytes = sizeBytes;
                LastWriteTimeUtc = lastWriteTimeUtc;
                HasMeta = hasMeta;
                MetaPath = metaPath;
            }
        }

        private sealed class SlotParsedData
        {
            public string HeaderJsonRaw { get; set; } = string.Empty;
            public bool HeaderReadOk { get; set; }
            public string? ProfileFromHeader { get; set; }
            public DateTime? CreatedUtc { get; set; }
            public DateTime? ModifiedUtc { get; set; }
            public string Compress { get; set; } = "none";
            public string Encrypt { get; set; } = "none";
            public string? Checksum { get; set; }
            public string MetadataJsonRaw { get; set; } = "(no metadata)";
            public bool MetaReadOk { get; set; }
            public string? MetaSlotName { get; set; }
            public string? MetaSceneOrChapter { get; set; }
            public TimeSpan? MetaPlaytime { get; set; }
            public string? MetaBuild { get; set; }
            public string? MetaNotes { get; set; }
            public bool MetaParsedOk { get; set; }
            public string? ErrorMessage { get; set; }

            public static SlotParsedData CreateDefault()
            {
                return new SlotParsedData
                {
                    HeaderJsonRaw = string.Empty,
                    HeaderReadOk = false,
                    ProfileFromHeader = null,
                    CreatedUtc = null,
                    ModifiedUtc = null,
                    Compress = "none",
                    Encrypt = "none",
                    Checksum = null,
                    MetadataJsonRaw = "(no metadata)",
                    MetaReadOk = false,
                    MetaSlotName = null,
                    MetaSceneOrChapter = null,
                    MetaPlaytime = null,
                    MetaBuild = null,
                    MetaNotes = null,
                    MetaParsedOk = false,
                    ErrorMessage = null
                };
            }
        }
    }
}
#endif
