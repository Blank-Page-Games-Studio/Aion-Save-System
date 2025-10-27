// Packages/com.bpg.aion/Editor/AionEditorPaths.cs
#nullable enable
using System.IO;
using UnityEditor;

namespace BPG.Aion
{
    /// <summary>Editor-only paths and helpers for code generation outputs.</summary>
    internal static class AionEditorPaths
    {
        public const string GeneratedFolder = "Assets/Aion.Generated";

        public static string EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                var parts = GeneratedFolder.Split('/');
                var path = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = $"{path}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(path, parts[i]);
                    path = next;
                }
            }
            return GeneratedFolder;
        }

        public static void OpenGeneratedFolder()
        {
            EnsureGeneratedFolder();
            EditorUtility.RevealInFinder(Path.GetFullPath(GeneratedFolder));
        }
    }
}
