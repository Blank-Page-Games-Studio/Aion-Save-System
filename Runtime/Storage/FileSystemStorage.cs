// com.bpg.aion/Runtime/Storage/FileSystemStorage.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Profile-aware file storage under Application.persistentDataPath/BPG.Saves/{Profile}/
    /// </summary>
    public sealed class FileSystemStorage : IStorageProvider
    {
        private readonly string _root;

        public FileSystemStorage(string? rootFolder = null)
        {
            var rootName = string.IsNullOrWhiteSpace(rootFolder) ? "BPG.Aion" : rootFolder!;
            _root = Path.Combine(Application.persistentDataPath, rootName);
            Directory.CreateDirectory(_root);
        }

        public string PathForSlot(int slot) => PathForSlot("Default", slot);
        public string PathForSlot(string profile, int slot)
        {
            var dir = ProfileDir(profile);
            return Path.Combine(dir, $"slot_{slot}.bpgsave");
        }

        public string PathForAutosave(string profile, int index)
        {
            var dir = ProfileDir(profile);
            return Path.Combine(dir, $"slot_autosave_{index}.bpgsave");
        }

        public string MetaPathForSlot(string profile, int slot)
        {
            var dir = ProfileDir(profile);
            return Path.Combine(dir, $"slot_{slot}.meta.json");
        }

        public string MetaPathForAutosave(string profile, int index)
        {
            var dir = ProfileDir(profile);
            return Path.Combine(dir, $"slot_autosave_{index}.meta.json");
        }

        public bool Exists(string path) => File.Exists(path);

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public string Read(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        public void Write(string path, string data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(data);
                sw.Flush();
                fs.Flush(true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // Binary body helpers for transformed payload
        public void WriteBytes(string path, byte[] data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public IEnumerable<string> GetAllProfiles()
        {
            if (!Directory.Exists(_root)) yield break;
            foreach (var dir in Directory.GetDirectories(_root))
                yield return Path.GetFileName(dir);
        }

        public void CreateProfile(string profile)
        {
            Directory.CreateDirectory(ProfileDir(profile));
        }

        public void DeleteProfile(string profile)
        {
            var dir = ProfileDir(profile);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public IEnumerable<(string savePath, string metaPath)> GetAllSlots(string profile)
        {
            var dir = ProfileDir(profile);
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir, "slot_*.bpgsave"))
            {
                var meta = Path.ChangeExtension(f, ".meta.json");
                yield return (f, meta);
            }
        }

        private string ProfileDir(string profile) => Path.Combine(_root, profile);
    }
}
