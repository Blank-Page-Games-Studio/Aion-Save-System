// Packages/com.bpg.aion/Editor/Windows/AionSnapshotGenerator.cs
#nullable enable
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BPG.Aion
{
    /// <summary>UI Toolkit window to view and generate snapshot code.</summary>
    public sealed class AionSnapshotGenerator : EditorWindow
    {
        [MenuItem("Window/BPG Aion/Snapshot Generator")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AionSnapshotGenerator>();
            wnd.titleContent = new GUIContent("Aion Snapshot Generator");
            wnd.minSize = new Vector2(520, 360);
        }

        private ListView _list = null!;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            var header = new Label("Generate strongly-typed snapshots for classes marked with [GenerateSaveSnapshot].")
            {
                style = { whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Bold }
            };
            root.Add(header);

            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            var btnGenAll = new Button(() => { AionSnapshotCodeEmitter.GenerateAll(); RefreshList(); }) { text = "Generate All" };
            var btnOpen = new Button(AionEditorPaths.OpenGeneratedFolder) { text = "Open Generated Folder" };
            var btnRefresh = new Button(RefreshList) { text = "Refresh List" };
            btnRow.Add(btnGenAll);
            btnRow.Add(btnOpen);
            btnRow.Add(btnRefresh);
            root.Add(btnRow);

            _list = new ListView
            {
                style = { flexGrow = 1, marginTop = 6 },
                selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            root.Add(_list);

            RefreshList();
        }

        private void RefreshList()
        {
            var types = TypeCache.GetTypesWithAttribute<GenerateSaveSnapshotAttribute>()
                        .OrderBy(t => t.FullName, StringComparer.Ordinal)
                        .ToArray();

            _list.itemsSource = types;
            _list.makeItem = () =>
            {
                var ve = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var lbl = new Label { style = { flexGrow = 1 } };
                var btn = new Button { text = "Regenerate" };
                ve.Add(lbl);
                ve.Add(btn);
                return ve;
            };
            _list.bindItem = (ve, i) =>
            {
                var t = (Type)_list.itemsSource[i]!;
                var lbl = (Label)ve.ElementAt(0);
                var btn = (Button)ve.ElementAt(1);
                lbl.text = t.FullName ?? t.Name;
                btn.clicked -= btn.userData as Action;
                Action act = () =>
                {
                    try
                    {
                        // Generate just this one type
                        var attr = t.GetCustomAttributes(typeof(GenerateSaveSnapshotAttribute), false).FirstOrDefault() as GenerateSaveSnapshotAttribute
                                   ?? new GenerateSaveSnapshotAttribute();
                        var code = AionSnapshotCodeEmitterPrivate.EmitForTypePublic(t, attr); // via friend bridge
                        var path = System.IO.Path.Combine(AionEditorPaths.EnsureGeneratedFolder(), $"{t.FullName!.Replace('.', '_').Replace('+', '_')}_Generated.cs");
                        System.IO.File.WriteAllText(path, code, new System.Text.UTF8Encoding(false));
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        Debug.Log($"[Aion] Regenerated: {t.FullName}");
                    }
                    catch (Exception ex) { Debug.LogError(ex); }
                };
                btn.userData = act;
                btn.clicked += act;
            };
        }
    }

    /// <summary>
    /// Small friend class to call the internal emitter for a single type from the window (keeps AionSnapshotCodeEmitter internal).
    /// </summary>
    internal static class AionSnapshotCodeEmitterPrivate
    {
        public static string EmitForTypePublic(Type t, GenerateSaveSnapshotAttribute cfg)
        {
            // Use reflection to access private method safely.
            var m = typeof(AionSnapshotCodeEmitter).GetMethod("EmitForType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (m == null) throw new InvalidOperationException("Emitter not available.");
            return (string)m.Invoke(null, new object[] { t, cfg })!;
        }
    }
}
