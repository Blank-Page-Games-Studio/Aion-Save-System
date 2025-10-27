// com.bpg.aion/Editor/Windows/AionSaveInspector.cs
#nullable enable
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BPG.Aion.Editor
{
    /// <summary>
    /// UI Toolkit-based inspector to browse profiles and slots, view headers/metadata, open folders.
    /// </summary>
    public sealed class AionSaveInspector : EditorWindow
    {
        [MenuItem("Window/BPG Aion/Save Inspector")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AionSaveInspector>();
            wnd.titleContent = new GUIContent("Aion Save Inspector");
            wnd.Show();
        }

        private ListView _profileList = null!;
        private ListView _slotList = null!;
        private TextField _headerJson = null!;
        private TextField _metaJson = null!;
        private Label _details = null!;
        private Button _refresh = null!;
        private Button _openFolder = null!;
        private FileSystemStorage _storage = null!;

        private string _selectedProfile = "Default";

        public void CreateGUI()
        {
            _storage = new FileSystemStorage();

            var root = rootVisualElement;

            var toolbar = new Toolbar();
            _refresh = new ToolbarButton(Refresh) { text = "Refresh" };
            _openFolder = new ToolbarButton(OpenFolder) { text = "Open Folder" };
            toolbar.Add(_refresh);
            toolbar.Add(_openFolder);
            root.Add(toolbar);

            var columns = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(columns);

            // Left panel: profiles + slots
            var left = new VisualElement { style = { flexDirection = FlexDirection.Column, paddingLeft = 4, paddingRight = 4 } };
            columns.Add(left);

            left.Add(new Label("Profiles") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            _profileList = new ListView();
            _profileList.selectionChanged += OnProfileSelected;
            left.Add(_profileList);

            left.Add(new Label("Slots") { style = { marginTop = 6, unityFontStyleAndWeight = FontStyle.Bold } });
            _slotList = new ListView();
            _slotList.selectionChanged += OnSlotSelected;
            left.Add(_slotList);

            // Right panel: details
            var right = new ScrollView();
            columns.Add(right);

            _details = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            right.Add(_details);

            right.Add(new Label("Header JSON"));
            _headerJson = new TextField { multiline = true };
            _headerJson.style.height = 150;
            right.Add(_headerJson);

            right.Add(new Label("Metadata JSON"));
            _metaJson = new TextField { multiline = true };
            _metaJson.style.height = 120;
            right.Add(_metaJson);

            Refresh();
        }

        private void Refresh()
        {
            var profiles = new System.Collections.Generic.List<string>(_storage.GetAllProfiles());
            if (profiles.Count == 0) { _storage.CreateProfile("Default"); profiles.Add("Default"); }
            _profileList.itemsSource = profiles;
            _profileList.Rebuild();

            if (!profiles.Contains(_selectedProfile)) _selectedProfile = profiles[0];
            PopulateSlots();
        }

        private void PopulateSlots()
        {
            var slots = new System.Collections.Generic.List<string>();
            foreach (var (save, _) in _storage.GetAllSlots(_selectedProfile)) slots.Add(Path.GetFileName(save));
            _slotList.itemsSource = slots;
            _slotList.Rebuild();
        }

        private void OnProfileSelected(System.Collections.Generic.IEnumerable<object> objs)
        {
            foreach (var o in objs) { _selectedProfile = o.ToString() ?? "Default"; break; }
            PopulateSlots();
        }

        private void OnSlotSelected(System.Collections.Generic.IEnumerable<object> objs)
        {
            foreach (var o in objs)
            {
                var fileName = o.ToString()!;
                var path = Path.Combine(_storage.GetProfileDir(_selectedProfile), fileName);
                ShowSlot(path);
                break;
            }
        }

        private void ShowSlot(string savePath)
        {
            try
            {
                using var fs = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sr = new StreamReader(fs);
                var headerJson = sr.ReadLine() ?? "";
                _headerJson.value = headerJson;

                var info = new FileInfo(savePath);
                var header = new JsonSerializer().Deserialize<SaveHeader>(headerJson);
                _details.text = $"File: {savePath}\nSize: {info.Length:N0} bytes\n" +
                                $"Profile: {header.Profile}\nCreated: {header.CreatedUtc}\nModified: {header.ModifiedUtc}\n" +
                                $"Compress: {header.Compress ?? "none"}, Encrypt: {header.Encrypt ?? "none"}\nChecksum: {header.Checksum}";

                var meta = Path.ChangeExtension(savePath, ".meta.json");
                _metaJson.value = File.Exists(meta) ? File.ReadAllText(meta) : "(no metadata)";
            }
            catch (System.Exception ex)
            {
                _details.text = "Error reading slot: " + ex.Message;
                _headerJson.value = "";
                _metaJson.value = "";
            }
        }

        private void OpenFolder()
        {
            EditorUtility.RevealInFinder(_storage.RootPath);
        }
    }
}
#endif
