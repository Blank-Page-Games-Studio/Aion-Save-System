// com.bpg.aion/Runtime/Core/SaveManager.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Orchestrates snapshot capture, transform pipeline, file IO, metadata, and integrity checks.
    /// 
    /// Transform order (documented & enforced):
    ///   1) Serialize body (Components) to JSON bytes using ISerializer.
    ///   2) If UseCompression: Compress(body) -> compressedBody.
    ///   3) If UseEncryption: Encrypt(bodyOrCompressed) with AES-GCM using:
    ///        - Nonce: 12 random bytes per save.
    ///        - AAD: UTF-8 bytes of the header JSON with Encrypt fields set but Checksum empty (Checksum = "").
    ///        - Output: ciphertext + tag (store nonce & tag in header Base64).
    ///   4) Compute SHA-256 hex checksum over the FINAL BODY BYTES (ciphertext if encrypted, else compressed/plain).
    ///   5) Write file: Header JSON (plaintext) + newline + FINAL BODY BYTES (raw).
    /// </summary>
    public sealed class SaveManager
    {
        private readonly List<ISaveable> _saveables = new();
        private readonly ISerializer _serializer;
        private readonly FileSystemStorage _storage;
        private readonly ICompressor? _compressor;
        private readonly IEncryptor? _encryptor;

        public SaveManager(ISerializer serializer, FileSystemStorage storage, ICompressor? compressor = null, IEncryptor? encryptor = null)
        {
            _serializer = serializer;
            _storage = storage;
            _compressor = compressor;
            _encryptor = encryptor;
        }

        /// <summary>Register a save participant.</summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;
            if (!_saveables.Contains(saveable)) _saveables.Add(saveable);
        }

        /// <summary>Unregister a save participant.</summary>
        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            _saveables.Remove(saveable);
        }

        // ---------------- Public API ----------------

        public SaveResult Save(int slot, SaveOptions? options = null)
        {
            var opts = options ?? new SaveOptions();
            var profile = opts.ProfileName ?? "Default";
            var slotKey = $"slot_{slot}";
            SaveSignals.EmitBeforeSave(profile, slotKey, opts);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                foreach (var s in _saveables) s.OnBeforeSave();

                var file = BuildLogicalFile(profile, opts);
                // Serialize body (Components) deterministically
                var bodyJson = _serializer.Serialize(file.Components);
                var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

                // Compression
                byte[] transformed = bodyBytes;
                string? compressName = null;
                if (opts.UseCompression && _compressor != null)
                {
                    transformed = _compressor.Compress(transformed);
                    compressName = _compressor.Name;
                }

                // Encryption
                string? encryptName = null;
                byte[]? nonce = null;
                byte[]? tag = null;
                try
                {
                    if (opts.UseEncryption && _encryptor != null)
                    {
                        encryptName = _encryptor.Name;
                        nonce = RandomNonce(12);
                        // Prepare header prior to checksum & encryption: set transforms but empty checksum/tag
                        file.Header.Encrypt = encryptName;
                        file.Header.NonceB64 = Convert.ToBase64String(nonce);
                        file.Header.TagB64 = null; // not yet
                        file.Header.Compress = compressName;
                        file.Header.Checksum = string.Empty;
                        file.Header.AppVersion = opts.AppVersion;
                        file.Header.ContentType = opts.ContentType;
                        file.Header.Summary = opts.Summary;
                        // AAD: header JSON with empty checksum
                        var aad = Encoding.UTF8.GetBytes(_serializer.Serialize(file.Header));
                        var ct = _encryptor.Encrypt(transformed, nonce, aad, out var tagOut);
                        tag = tagOut;
                        transformed = ct;
                    }
                    else
                    {
                        // Set transforms without encryption
                        file.Header.Encrypt = null;
                        file.Header.NonceB64 = null;
                        file.Header.TagB64 = null;
                        file.Header.Compress = compressName;
                        file.Header.Checksum = string.Empty;
                        file.Header.AppVersion = opts.AppVersion;
                        file.Header.ContentType = opts.ContentType;
                        file.Header.Summary = opts.Summary;
                    }
                }
                catch (NotSupportedException nse)
                {
                    return new SaveResult(ResultStatus.Error, $"Encryption not supported: {nse.Message}", _storage.PathForSlot(profile, slot));
                }

                // Final checksum over transformed body bytes
                var checksum = Checksum.Sha256HexBytes(transformed);
                file.Header.Checksum = checksum;
                if (encryptName != null && tag != null)
                    file.Header.TagB64 = Convert.ToBase64String(tag);

                // Header JSON plaintext
                var headerJson = _serializer.Serialize(file.Header);
                var headerBytes = Encoding.UTF8.GetBytes(headerJson);

                // Compose on-disk format: header JSON + '\n' + body bytes
                var outPath = _storage.PathForSlot(profile, slot);
                var bytes = new byte[headerBytes.Length + 1 + transformed.Length];
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                bytes[headerBytes.Length] = (byte)'\n';
                Buffer.BlockCopy(transformed, 0, bytes, headerBytes.Length + 1, transformed.Length);

                _storage.WriteBytes(outPath, bytes);

                var duration = sw.ElapsedMilliseconds;
                WriteMetadataManual(slot, profile, duration, opts.Summary, bytes.Length);

                foreach (var s in _saveables) s.OnAfterLoad(); // optional post-save hook; can be OnAfterSave if you add it

                var res = new SaveResult(ResultStatus.Ok, "Saved.", outPath);
                SaveSignals.EmitAfterSave(profile, slotKey, duration, res);
                return res;
            }
            catch (UnauthorizedAccessException uae)
            {
                var res = new SaveResult(ResultStatus.Unauthorized, $"Unauthorized: {uae.Message}", _storage.PathForSlot(profile, slot));
                SaveSignals.EmitAfterSave(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
            catch (Exception ex)
            {
                var res = new SaveResult(ResultStatus.Error, $"Save failed: {ex.Message}", _storage.PathForSlot(profile, slot));
                SaveSignals.EmitAfterSave(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
        }

        public LoadResult Load(int slot, LoadOptions? options = null)
        {
            var opts = options ?? new LoadOptions();
            var profile = opts.ProfileName ?? "Default";
            var slotKey = $"slot_{slot}";
            SaveSignals.EmitBeforeLoad(profile, slotKey, opts);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var path = _storage.PathForSlot(profile, slot);
            try
            {
                if (!_storage.Exists(path))
                {
                    var nf = new LoadResult(ResultStatus.NotFound, "Slot not found.", path);
                    SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, nf);
                    return nf;
                }

                var all = _storage.ReadBytes(path);
                // Split header and body at first '\n'
                var idx = Array.IndexOf(all, (byte)'\n');
                if (idx <= 0) return FinalCorrupt("Malformed save (no header separator).", profile, slotKey, sw, path);

                var headerBytes = new byte[idx];
                Buffer.BlockCopy(all, 0, headerBytes, 0, idx);
                var bodyBytes = new byte[all.Length - idx - 1];
                Buffer.BlockCopy(all, idx + 1, bodyBytes, 0, bodyBytes.Length);

                var headerJson = Encoding.UTF8.GetString(headerBytes);
                var header = _serializer.Deserialize<SaveHeader>(headerJson);

                // Verify checksum (over stored body bytes)
                var computed = Checksum.Sha256HexBytes(bodyBytes);
                if (!string.Equals(computed, header.Checksum, StringComparison.OrdinalIgnoreCase))
                    return FinalCorrupt("Checksum mismatch.", profile, slotKey, sw, path);

                // If encrypted, decrypt using header AAD (header with same checksum value as stored)
                if (!string.IsNullOrEmpty(header.Encrypt))
                {
                    if (_encryptor == null || string.IsNullOrEmpty(header.NonceB64) || string.IsNullOrEmpty(header.TagB64))
                        return FinalCorrupt("Encrypted save but decryptor parameters missing.", profile, slotKey, sw, path);

                    var nonce = Convert.FromBase64String(header.NonceB64);
                    var tag = Convert.FromBase64String(header.TagB64);
                    var aad = Encoding.UTF8.GetBytes(headerJson); // AAD exactly as header on disk
                    try
                    {
                        bodyBytes = _encryptor.Decrypt(bodyBytes, nonce, aad, tag);
                    }
                    catch (Exception)
                    {
                        return FinalCorrupt("Decryption failed (AAD/tag mismatch).", profile, slotKey, sw, path);
                    }
                }

                // If compressed, decompress
                if (!string.IsNullOrEmpty(header.Compress))
                {
                    if (_compressor == null || !string.Equals(_compressor.Name, header.Compress, StringComparison.Ordinal))
                        return FinalCorrupt("Compressed save but compressor not available.", profile, slotKey, sw, path);

                    bodyBytes = _compressor.Decompress(bodyBytes);
                }

                // Body is JSON for ComponentSnapshot[]
                var bodyJson = Encoding.UTF8.GetString(bodyBytes);
                var components = _serializer.Deserialize<ComponentSnapshot[]>(bodyJson);

                ApplyAll(components);
                foreach (var s in _saveables) s.OnAfterLoad();

                var res = new LoadResult(ResultStatus.Ok, "Loaded.", path);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
            catch (UnauthorizedAccessException uae)
            {
                var res = new LoadResult(ResultStatus.Unauthorized, $"Unauthorized: {uae.Message}", path);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
            catch (Exception ex)
            {
                var res = new LoadResult(ResultStatus.Error, $"Load failed: {ex.Message}", path);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
        }

        public DeleteResult Delete(int slot, string profile = "Default")
        {
            var path = _storage.PathForSlot(profile, slot);
            try
            {
                if (_storage.Exists(path)) _storage.Delete(path);
                var meta = _storage.MetaPathForSlot(profile, slot);
                if (_storage.Exists(meta)) _storage.Delete(meta);
                return new DeleteResult(ResultStatus.Ok, "Deleted.", path);
            }
            catch (Exception ex)
            {
                return new DeleteResult(ResultStatus.Error, $"Delete failed: {ex.Message}", path);
            }
        }

        // Autosave API
        public SaveResult SaveAutosave(int index, SaveOptions options)
        {
            var profile = options.ProfileName ?? "Default";
            var slotKey = $"slot_autosave_{index}";
            SaveSignals.EmitBeforeSave(profile, slotKey, options);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                foreach (var s in _saveables) s.OnBeforeSave();

                var file = BuildLogicalFile(profile, options);
                var bodyJson = _serializer.Serialize(file.Components);
                var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

                byte[] transformed = bodyBytes;
                string? compressName = null;
                if (options.UseCompression && _compressor != null)
                {
                    transformed = _compressor.Compress(transformed);
                    compressName = _compressor.Name;
                }

                string? encryptName = null;
                byte[]? nonce = null;
                byte[]? tag = null;
                if (options.UseEncryption && _encryptor != null)
                {
                    encryptName = _encryptor.Name;
                    nonce = RandomNonce(12);
                    file.Header.Encrypt = encryptName;
                    file.Header.NonceB64 = Convert.ToBase64String(nonce);
                    file.Header.TagB64 = null;
                    file.Header.Compress = compressName;
                    file.Header.Checksum = string.Empty;
                    file.Header.AppVersion = options.AppVersion;
                    file.Header.ContentType = options.ContentType;
                    file.Header.Summary = options.Summary;
                    var aad = Encoding.UTF8.GetBytes(_serializer.Serialize(file.Header));
                    var ct = _encryptor.Encrypt(transformed, nonce, aad, out var tagOut);
                    tag = tagOut;
                    transformed = ct;
                }
                else
                {
                    file.Header.Encrypt = null;
                    file.Header.NonceB64 = null;
                    file.Header.TagB64 = null;
                    file.Header.Compress = compressName;
                    file.Header.Checksum = string.Empty;
                    file.Header.AppVersion = options.AppVersion;
                    file.Header.ContentType = options.ContentType;
                    file.Header.Summary = options.Summary;
                }

                var checksum = Checksum.Sha256HexBytes(transformed);
                file.Header.Checksum = checksum;
                if (encryptName != null && tag != null)
                    file.Header.TagB64 = Convert.ToBase64String(tag);

                var headerJson = _serializer.Serialize(file.Header);
                var headerBytes = Encoding.UTF8.GetBytes(headerJson);

                var outPath = _storage.PathForAutosave(profile, index);
                var bytes = new byte[headerBytes.Length + 1 + transformed.Length];
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                bytes[headerBytes.Length] = (byte)'\n';
                Buffer.BlockCopy(transformed, 0, bytes, headerBytes.Length + 1, transformed.Length);

                _storage.WriteBytes(outPath, bytes);

                var duration = sw.ElapsedMilliseconds;
                WriteMetadataAutosave(index, profile, duration, options.Summary, bytes.Length);

                var res = new SaveResult(ResultStatus.Ok, "Autosaved.", outPath);
                SaveSignals.EmitAfterSave(profile, slotKey, duration, res);
                return res;
            }
            catch (Exception ex)
            {
                var res = new SaveResult(ResultStatus.Error, $"Autosave failed: {ex.Message}", _storage.PathForAutosave(profile, index));
                SaveSignals.EmitAfterSave(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
        }

        // Profiles API convenience
        public IEnumerable<string> GetAllProfiles() => _storage.GetAllProfiles();
        public void CreateProfile(string name) => _storage.CreateProfile(name);
        public void DeleteProfile(string name) => _storage.DeleteProfile(name);
        public IEnumerable<(string savePath, string metaPath)> GetAllSlots(string profile) => _storage.GetAllSlots(profile);

        // ---------------- Internals ----------------

        private SaveFile BuildLogicalFile(string profile, SaveOptions opts)
        {
            var snapshots = CaptureAll();
            var header = new SaveHeader
            {
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                ModifiedUtc = DateTime.UtcNow.ToString("o"),
                AppVersion = opts.AppVersion,
                ContentType = opts.ContentType,
                Profile = profile,
                Summary = opts.Summary,
                Checksum = string.Empty
            };
            return new SaveFile { Header = header, Components = snapshots };
        }

        private ComponentSnapshot[] CaptureAll()
        {
            return _saveables
                .Select(s => (s, Key: GetStableKeyFor(s)))
                .OrderBy(t => t.Key, StringComparer.Ordinal)
                .Select(t => CaptureOne(t.s, t.Key))
                .ToArray();
        }

        private ComponentSnapshot CaptureOne(ISaveable s, string key)
        {
            var fields = new List<FieldEntry>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var target = s.AsComponent;
            foreach (var f in target.GetType().GetFields(flags))
            {
                if (f.GetCustomAttribute<SaveFieldAttribute>(true) == null) continue;

                var val = f.GetValue(target);
                if (!IsSupportedType(f.FieldType))
                    throw new InvalidOperationException($"Unsupported [SaveField] type '{f.FieldType}' on {target.GetType().Name}.{f.Name}");

                var jsonValue = SerializeToType(val, f.FieldType);
                fields.Add(new FieldEntry
                {
                    Name = f.Name,
                    Type = f.FieldType.AssemblyQualifiedName ?? f.FieldType.FullName ?? f.FieldType.Name,
                    JsonValue = jsonValue
                });
            }
            var ordered = fields.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            return new ComponentSnapshot { Key = key, Fields = ordered };
        }

        private void ApplyAll(IEnumerable<ComponentSnapshot> snapshots)
        {
            var map = _saveables.ToDictionary(GetStableKeyFor, s => s, StringComparer.Ordinal);
            foreach (var snap in snapshots)
            {
                if (!map.TryGetValue(snap.Key, out var s)) continue;
                var target = s.AsComponent;
                var type = target.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var fieldMap = type
                    .GetFields(flags)
                    .Where(f => f.GetCustomAttribute<SaveFieldAttribute>(true) != null)
                    .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

                foreach (var entry in snap.Fields)
                {
                    if (!fieldMap.TryGetValue(entry.Name, out var field)) continue;
                    var value = DeserializeToType(entry.JsonValue, field.FieldType);
                    if (!IsSupportedType(field.FieldType))
                        throw new InvalidOperationException($"Unsupported [SaveField] type '{field.FieldType}' during load.");
                    field.SetValue(target, value);
                }
            }
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var stack = new Stack<string>();
            var current = go.transform;
            while (current != null) { stack.Push(current.name); current = current.parent; }
            return string.Join("/", stack);
        }

        private static string GetStableKeyFor(ISaveable s)
        {
            if (!string.IsNullOrWhiteSpace(s.SaveKeyOverride)) return s.SaveKeyOverride!;
            var comp = s.AsComponent;
            var attr = comp.GetType().GetCustomAttribute<SaveKeyAttribute>(true);
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Key)) return attr.Key;

            var path = GetHierarchyPath(comp.gameObject);
            var typeName = comp.GetType().FullName;
            var siblings = comp.GetComponents(comp.GetType());
            var index = Array.IndexOf(siblings, comp);
            return $"{path}|{typeName}|{index}";
        }

        private string SerializeToType(object? value, Type type)
        {
            var method = typeof(ISerializer).GetMethod(nameof(ISerializer.Serialize))!;
            var generic = method.MakeGenericMethod(type);
            return (string)generic.Invoke(_serializer, new object?[] { value })!;
        }

        private object? DeserializeToType(string json, Type type)
        {
            var method = typeof(ISerializer).GetMethod(nameof(ISerializer.Deserialize))!;
            var generic = method.MakeGenericMethod(type);
            return generic.Invoke(_serializer, new object?[] { json });
        }

        private static bool IsSupportedType(Type t)
        {
            return t == typeof(string)
                || t == typeof(bool)
                || t == typeof(byte) || t == typeof(sbyte)
                || t == typeof(short) || t == typeof(ushort)
                || t == typeof(int) || t == typeof(uint)
                || t == typeof(long) || t == typeof(ulong)
                || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
        }

        private static byte[] RandomNonce(int len)
        {
            var b = new byte[len];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(b);
            return b;
        }

        private LoadResult FinalCorrupt(string msg, string profile, string slotKey, System.Diagnostics.Stopwatch sw, string path)
        {
            var res = new LoadResult(ResultStatus.Corrupt, msg, path);
            SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
            return res;
        }

        private void WriteMetadataManual(int slot, string profile, long durationMs, string? summary, long approxBytes)
        {
            var meta = MetadataUtility.CreateForManual(slot, profile, durationMs, summary, approxBytes);
            var path = _storage.MetaPathForSlot(profile, slot);
            var json = _serializer.Serialize(meta);
            _storage.Write(path, json);
        }

        private void WriteMetadataAutosave(int index, string profile, long durationMs, string? summary, long approxBytes)
        {
            var meta = MetadataUtility.CreateForAutosave(index, profile, durationMs, summary, approxBytes);
            var path = _storage.MetaPathForAutosave(profile, index);
            var json = _serializer.Serialize(meta);
            _storage.Write(path, json);
        }
    }
}
