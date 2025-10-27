// com.bpg.aion/Runtime/Storage/FileSystemStorage.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>Profile-aware storage under persistentDataPath/BPG.Aion/{Profile}/</summary>
    public sealed class FileSystemStorage : IStorageProvider
    {
        public string RootPath => _root;

        private readonly string _root;
        public FileSystemStorage(string? rootFolder = null)
        {
            var rootName = string.IsNullOrWhiteSpace(rootFolder) ? "BPG.Aion" : rootFolder!;
            _root = Path.Combine(Application.persistentDataPath, rootName);
            Directory.CreateDirectory(_root);
        }

        public string GetProfileDir(string profile) => Path.Combine(_root, profile);

        public string PathForSlot(int slot) => PathForSlot("Default", slot);
        public string PathForSlot(string profile, int slot) => Path.Combine(GetProfileDir(profile), $"slot_{slot}.bpgsave");
        public string PathForAutosave(string profile, int index) => Path.Combine(GetProfileDir(profile), $"slot_autosave_{index}.bpgsave");
        public string MetaPathForSlot(string profile, int slot) => Path.Combine(GetProfileDir(profile), $"slot_{slot}.meta.json");
        public string MetaPathForAutosave(string profile, int index) => Path.Combine(GetProfileDir(profile), $"slot_autosave_{index}.meta.json");

        public void Write(string path, string data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public string Read(string path) => File.ReadAllText(path);
        public bool Exists(string path) => File.Exists(path);
        public void Delete(string path) { if (File.Exists(path)) File.Delete(path); }

        public string PathForSlot(int slot, string profile) => PathForSlot(profile, slot); // compat
        public string Read(string profile, int slot) => Read(PathForSlot(profile, slot));

        public System.Collections.Generic.IEnumerable<string> GetAllProfiles()
        {
            if (!Directory.Exists(_root)) yield break;
            foreach (var dir in Directory.GetDirectories(_root)) yield return Path.GetFileName(dir);
        }
        public void CreateProfile(string profile) => Directory.CreateDirectory(GetProfileDir(profile));
        public void DeleteProfile(string profile) { var d = GetProfileDir(profile); if (Directory.Exists(d)) Directory.Delete(d, true); }
        public IEnumerable<(string savePath, string metaPath)> GetAllSlots(string profile)
        {
            var dir = GetProfileDir(profile);
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir, "slot_*.bpgsave"))
            {
                var meta = Path.ChangeExtension(f, ".meta.json");
                yield return (f, meta);
            }
        }

        // Binary helpers for async readers
        public System.Threading.Tasks.Task<byte[]> ReadAllBytesAsync(string path) => System.IO.File.ReadAllBytesAsync(path);
    }
}
