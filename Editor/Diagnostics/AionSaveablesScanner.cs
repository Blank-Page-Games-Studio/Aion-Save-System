// Assets/SaveSystem/Editor/Diagnostics/AionSaveablesScanner.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BPG.Aion.Editor.Diagnostics
{
    public enum ScanSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ScanResult
    {
        public List<Item> Items { get; } = new List<Item>();
        public int Errors { get; private set; }
        public int Warnings { get; private set; }
        public int Infos { get; private set; }

        internal void Add(ScanSeverity severity, UnityEngine.Object context, string message, string? key = null)
        {
            Items.Add(new Item(severity, context, message, key));
            switch (severity)
            {
                case ScanSeverity.Error:
                    Errors++;
                    break;
                case ScanSeverity.Warning:
                    Warnings++;
                    break;
                case ScanSeverity.Info:
                    Infos++;
                    break;
            }
        }
    }

    public sealed class Item
    {
        public ScanSeverity Severity { get; }
        public UnityEngine.Object Context { get; }
        public string Message { get; }
        public string? Key { get; }

        public Item(ScanSeverity severity, UnityEngine.Object context, string message, string? key)
        {
            Severity = severity;
            Context = context;
            Message = message;
            Key = key;
        }
    }

    public static class AionSaveablesScanner
    {
        public static ScanResult Scan(bool includePrefabs)
        {
            var result = new ScanResult();
            var saveFieldCache = new Dictionary<Type, bool>();
            var saveKeyCache = new Dictionary<Type, SaveKeyAttribute?>();

            ScanOpenScenes(result, saveFieldCache, saveKeyCache);

            if (includePrefabs)
            {
                ScanPrefabs(result, saveFieldCache, saveKeyCache);
            }

            return result;
        }

        private static void ScanOpenScenes(
            ScanResult result,
            Dictionary<Type, bool> saveFieldCache,
            Dictionary<Type, SaveKeyAttribute?> saveKeyCache)
        {
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                ScanScene(scene, result, saveFieldCache, saveKeyCache);
            }
        }

        private static void ScanScene(
            Scene scene,
            ScanResult result,
            Dictionary<Type, bool> saveFieldCache,
            Dictionary<Type, SaveKeyAttribute?> saveKeyCache)
        {
            var keyMap = new Dictionary<string, List<MonoBehaviour>>(StringComparer.Ordinal);

            foreach (var root in scene.GetRootGameObjects())
            {
                var components = root.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var component in components)
                {
                    if (component == null)
                        continue;
                    if (component is not ISaveable saveable)
                        continue;

                    var type = component.GetType();
                    var explicitKey = GetExplicitKey(saveable, type, saveKeyCache);

                    if (string.IsNullOrWhiteSpace(explicitKey))
                    {
                        var message = $"Missing explicit save key on {type.Name} in scene '{scene.name}'.";
                        result.Add(ScanSeverity.Warning, component, message);
                    }
                    else
                    {
                        if (!keyMap.TryGetValue(explicitKey!, out var list))
                        {
                            list = new List<MonoBehaviour>();
                            keyMap[explicitKey!] = list;
                        }

                        list.Add(component);
                    }

                    if (!HasSaveFieldMembers(type, saveFieldCache))
                    {
                        var message = $"No [SaveField] members found on {type.Name} in scene '{scene.name}'.";
                        result.Add(ScanSeverity.Info, component, message, explicitKey);
                    }
                }
            }

            foreach (var pair in keyMap)
            {
                if (pair.Value.Count < 2)
                    continue;

                foreach (var component in pair.Value)
                {
                    var message = $"Duplicate save key '{pair.Key}' detected in scene '{scene.name}'.";
                    result.Add(ScanSeverity.Error, component, message, pair.Key);
                }
            }
        }

        private static void ScanPrefabs(
            ScanResult result,
            Dictionary<Type, bool> saveFieldCache,
            Dictionary<Type, SaveKeyAttribute?> saveKeyCache)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                    continue;

                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ScanPrefabContents(prefabRoot, prefabAsset, path, result, saveFieldCache, saveKeyCache);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void ScanPrefabContents(
            GameObject prefabRoot,
            GameObject prefabAsset,
            string path,
            ScanResult result,
            Dictionary<Type, bool> saveFieldCache,
            Dictionary<Type, SaveKeyAttribute?> saveKeyCache)
        {
            var keyMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var components = prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (var component in components)
            {
                if (component == null)
                    continue;
                if (component is not ISaveable saveable)
                    continue;

                var type = component.GetType();
                var explicitKey = GetExplicitKey(saveable, type, saveKeyCache);
                var hierarchyPath = GetHierarchyPath(component.transform, prefabRoot.transform);

                if (string.IsNullOrWhiteSpace(explicitKey))
                {
                    var message = $"Prefab '{prefabAsset.name}' ({path}): {hierarchyPath} ({type.Name}) is missing an explicit save key.";
                    result.Add(ScanSeverity.Warning, prefabAsset, message);
                }
                else
                {
                    if (!keyMap.TryGetValue(explicitKey!, out var list))
                    {
                        list = new List<string>();
                        keyMap[explicitKey!] = list;
                    }

                    list.Add(hierarchyPath + " (" + type.Name + ")");
                }

                if (!HasSaveFieldMembers(type, saveFieldCache))
                {
                    var message = $"Prefab '{prefabAsset.name}' ({path}): {hierarchyPath} ({type.Name}) has no [SaveField] members.";
                    result.Add(ScanSeverity.Info, prefabAsset, message, explicitKey);
                }
            }

            foreach (var pair in keyMap)
            {
                if (pair.Value.Count < 2)
                    continue;

                foreach (var entry in pair.Value)
                {
                    var message =
                        $"Prefab '{prefabAsset.name}' ({path}): duplicate save key '{pair.Key}' on {entry}.";
                    result.Add(ScanSeverity.Error, prefabAsset, message, pair.Key);
                }
            }
        }

        private static string? GetExplicitKey(
            ISaveable saveable,
            Type type,
            Dictionary<Type, SaveKeyAttribute?> saveKeyCache)
        {
            try
            {
                var overrideKey = saveable.SaveKeyOverride;
                if (!string.IsNullOrWhiteSpace(overrideKey))
                    return overrideKey;
            }
            catch (Exception)
            {
                // Ignore SaveKeyOverride errors and fall back to attribute-based detection.
            }

            if (!saveKeyCache.TryGetValue(type, out var attr))
            {
                attr = type.GetCustomAttribute<SaveKeyAttribute>(true);
                saveKeyCache[type] = attr;
            }

            if (attr != null && !string.IsNullOrWhiteSpace(attr.Key))
                return attr.Key;

            return null;
        }

        private static bool HasSaveFieldMembers(Type type, Dictionary<Type, bool> cache)
        {
            if (cache.TryGetValue(type, out var hasMembers))
                return hasMembers;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var hasFields = false;
            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<SaveFieldAttribute>(true) == null)
                    continue;
                hasFields = true;
                break;
            }

            var hasProps = false;
            if (!hasFields)
            {
                foreach (var prop in type.GetProperties(flags))
                {
                    if (prop.GetIndexParameters().Length > 0)
                        continue;
                    if (prop.GetCustomAttribute<SaveFieldAttribute>(true) == null)
                        continue;
                    hasProps = true;
                    break;
                }
            }

            hasMembers = hasFields || hasProps;
            cache[type] = hasMembers;
            return hasMembers;
        }

        private static string GetHierarchyPath(Transform leaf, Transform root)
        {
            var stack = new Stack<string>();
            var current = leaf;
            while (current != null)
            {
                stack.Push(current.name);
                if (current == root)
                    break;
                current = current.parent;
            }

            return string.Join("/", stack);
        }
    }
}
