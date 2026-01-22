// com.bpg.aion/Runtime/Core/SaveManager.cs
#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Save/Load orchestrator with streaming writes, async APIs, progress and diagnostics.
    ///
    /// File format:
    ///   - First line: Header JSON (UTF-8, plaintext)
    ///   - Newline (0x0A)
    ///   - Body bytes: serialized Components[] possibly compressed and/or encrypted
    ///
    /// Transform order:
    ///   Serialize → (optional) Compress → (optional) Encrypt → Checksum(over FINAL BODY BYTES).
    ///
    /// Notes for Unity JsonUtility:
    ///   - JsonUtility cannot serialize top-level arrays or primitives/strings.
    ///   - We wrap the components array in { Items:[...] } and each field value in Box<T> { Value: ... }.
    /// </summary>
    public sealed class SaveManager
    {
        private readonly List<ISaveable> _saveables = new();
        private readonly ISerializer _serializer;
        private readonly FileSystemStorage _storage;
        private readonly ICompressor? _compressor;
        private readonly IEncryptor? _encryptor;

        // Streaming settings
        private const int CHUNK_SIZE = 256 * 1024;               // 256 KiB per chunk
        private const long STREAM_THRESHOLD = 32L * 1024 * 1024; // 32 MiB body to enable streaming pipeline

        public SaveManager(ISerializer serializer, FileSystemStorage storage, ICompressor? compressor = null, IEncryptor? encryptor = null)
        {
            _serializer = serializer;
            _storage = storage;
            _compressor = compressor;
            _encryptor = encryptor;
        }

        public void Register(ISaveable s) { if (s != null && !_saveables.Contains(s)) _saveables.Add(s); }
        public void Unregister(ISaveable s) { if (s != null) _saveables.Remove(s); }

        // --------- Async APIs ---------

        public Task<SaveResult> SaveAsync(int slot, SaveOptions options, IProgress<float>? progress = null, CancellationToken token = default)
            => SaveInternalAsync(slot, options, autosaveIndex: null, progress, token);

        public Task<SaveResult> SaveAutosaveAsync(int index, SaveOptions options, IProgress<float>? progress = null, CancellationToken token = default)
            => SaveInternalAsync(slot: -1, options, autosaveIndex: index, progress, token);

        public async Task<LoadResult> LoadAsync(int slot, LoadOptions options, IProgress<float>? progress = null, CancellationToken token = default)
        {
            var profile = options.ProfileName ?? "Default";
            var slotKey = $"slot_{slot}";
            SaveSignals.EmitBeforeLoad(profile, slotKey, options);

            var path = _storage.PathForSlot(profile, slot);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!_storage.Exists(path))
                {
                    var nf = new LoadResult(ResultStatus.NotFound, SaveErrorTranslator.Friendly(ResultStatus.NotFound), path, durationMs: 0, bytesRead: 0, recoveredFromBackup: false);
                    SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, nf);
                    return nf;
                }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE, useAsync: true);

                // Read the header line as raw bytes to avoid StreamReader prefetching into the body.
                var headerLineBytes = await ReadHeaderLineBytesAsync(fs, token);
                var headerJson = Encoding.UTF8.GetString(headerLineBytes);
                var header = _serializer.Deserialize<SaveHeader>(headerJson);

                // Read the remainder as the body, from the exact position after '\n'
                var bodyBytes = await ReadToEndAsync(fs, progress, token);

                // Verify checksum BEFORE any transform (checksum is over stored body bytes)
                var checksum = ChecksumHex(bodyBytes);
                if (!string.Equals(checksum, header.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    var corrupt = new LoadResult(ResultStatus.Corrupt, SaveErrorTranslator.Friendly(ResultStatus.Corrupt), path, sw.ElapsedMilliseconds, bodyBytes.LongLength, false);
                    SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, corrupt);
                    return corrupt;
                }

                // Encryption path is disabled (bail out if encountered)
                if (!string.IsNullOrEmpty(header.Encrypt))
                {
                    var err = new LoadResult(ResultStatus.Corrupt, "Encrypted save but decryptor is disabled.", path, sw.ElapsedMilliseconds, bodyBytes.LongLength, false);
                    SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, err);
                    return err;
                }

                // Decompress if needed
                if (!string.IsNullOrEmpty(header.Compress))
                {
                    if (_compressor == null || !string.Equals(_compressor.Name, header.Compress, StringComparison.Ordinal))
                    {
                        var err = new LoadResult(ResultStatus.Error, "Compressed save but compressor not available.", path, sw.ElapsedMilliseconds, 0, false);
                        SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, err);
                        return err;
                    }
                    bodyBytes = _compressor.Decompress(bodyBytes);
                }

                // Deserialize Components and apply (JsonUtility can't handle top-level arrays → tolerant fallback)
                var bodyJson = Encoding.UTF8.GetString(bodyBytes);
                ComponentSnapshot[] components;
                try
                {
                    components = _serializer.Deserialize<ComponentSnapshot[]>(bodyJson);
                }
                catch
                {
                    var itemsWrap = _serializer.Deserialize<ArrayItemsWrapper<ComponentSnapshot>>(bodyJson);
                    if (itemsWrap.Items != null && itemsWrap.Items.Length > 0)
                        components = itemsWrap.Items;
                    else
                    {
                        var compsWrap = _serializer.Deserialize<ArrayComponentsWrapper<ComponentSnapshot>>(bodyJson);
                        components = compsWrap.Components ?? Array.Empty<ComponentSnapshot>();
                    }
                }

                foreach (var s in _saveables) s.OnBeforeSave(); // parity hook (safe no-op)
                ApplyAll(components);
                foreach (var s in _saveables) s.OnAfterLoad();

                var ok = new LoadResult(ResultStatus.Ok, SaveErrorTranslator.Friendly(ResultStatus.Ok), path, sw.ElapsedMilliseconds, bodyBytes.LongLength, false);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, ok);
                return ok;
            }
            catch (OperationCanceledException)
            {
                var res = new LoadResult(ResultStatus.Error, "Load canceled.", path, sw.ElapsedMilliseconds, 0, false);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
            catch (UnauthorizedAccessException uae)
            {
                var res = new LoadResult(ResultStatus.Unauthorized, SaveErrorTranslator.Friendly(uae), path, sw.ElapsedMilliseconds, 0, false);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
            catch (Exception ex)
            {
                var res = new LoadResult(ResultStatus.Error, SaveErrorTranslator.Friendly(ex), path, sw.ElapsedMilliseconds, 0, false);
                SaveSignals.EmitAfterLoad(profile, slotKey, sw.ElapsedMilliseconds, res);
                return res;
            }
        }

        // --------- Streaming Save (internal) ---------

        private async Task<SaveResult> SaveInternalAsync(int slot, SaveOptions options, int? autosaveIndex, IProgress<float>? progress, CancellationToken token)
        {
            var profile = options.ProfileName ?? "Default";
            var slotKey = autosaveIndex.HasValue ? $"slot_autosave_{autosaveIndex.Value}" : $"slot_{slot}";
            SaveSignals.EmitBeforeSave(profile, slotKey, options);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            SaveProfiler.Begin();

            // Hooks
            foreach (var s in _saveables) s.OnBeforeSave();

            var header = new SaveHeader
            {
                FormatId = "BPG.SAVE_v1",
                Version = new SemVer { Major = 0, Minor = 1, Patch = 5 },
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                ModifiedUtc = DateTime.UtcNow.ToString("o"),
                AppVersion = options.AppVersion ?? Application.version,
                ContentType = options.ContentType,
                Profile = profile,
                Summary = options.Summary,
                Compress = null,
                Encrypt = null,
                Checksum = string.Empty,
            };

            try
            {
                // 1) Serialize components (JSON body) — wrap array for JsonUtility
                var components = CaptureAll();
                var bodyJsonWrapped = _serializer.Serialize(new ArrayItemsWrapper<ComponentSnapshot> { Items = components });
                var plainBytes = Encoding.UTF8.GetBytes(bodyJsonWrapped);

                // 2) Produce FINAL BODY BYTES into a temp file (streaming gzip if enabled)
                var bodyTmpPath = Path.Combine(_storage.GetProfileDir(profile), $".__body_{Guid.NewGuid():N}.tmp");
                Directory.CreateDirectory(Path.GetDirectoryName(bodyTmpPath)!);

                long bytesForChecksum;
                if (options.UseEncryption && _encryptor != null)
                {
                    // (Usually disabled in your build) — compress first (in-memory) if requested, then encrypt, then stream ciphertext to temp
                    if (options.UseCompression && _compressor != null)
                    {
                        plainBytes = _compressor.Compress(plainBytes);
                        header.Compress = _compressor.Name;
                    }

                    var nonce = RandomNonce(12);
                    header.Encrypt = _encryptor.Name;
                    header.NonceB64 = Convert.ToBase64String(nonce);
                    header.TagB64 = null;

                    var aad = Encoding.UTF8.GetBytes(_serializer.Serialize(header)); // header with empty checksum/tag
                    var ciphertext = _encryptor.Encrypt(plainBytes, nonce, aad, out var tag);
                    header.TagB64 = Convert.ToBase64String(tag);

                    using (var outFs = new FileStream(bodyTmpPath, FileMode.Create, FileAccess.Write, FileShare.None, CHUNK_SIZE, true))
                    {
                        int written = 0;
                        for (int offset = 0; offset < ciphertext.Length; offset += CHUNK_SIZE)
                        {
                            token.ThrowIfCancellationRequested();
                            var count = Math.Min(CHUNK_SIZE, ciphertext.Length - offset);
                            await outFs.WriteAsync(ciphertext.AsMemory(offset, count), token);
                            written += count;
                            progress?.Report((float)written / Math.Max(1, ciphertext.Length) * 0.5f);
                        }
                    }

                    bytesForChecksum = new FileInfo(bodyTmpPath).Length;
                }
                else
                {
                    // No encryption: stream either gzip or plain into the temp file
                    if (options.UseCompression && _compressor != null)
                    {
                        header.Compress = _compressor.Name;
                        using var outFs = new FileStream(bodyTmpPath, FileMode.Create, FileAccess.Write, FileShare.None, CHUNK_SIZE, true);
                        using (var gz = new System.IO.Compression.GZipStream(outFs, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                        {
                            for (int offset = 0; offset < plainBytes.Length; offset += CHUNK_SIZE)
                            {
                                token.ThrowIfCancellationRequested();
                                var count = Math.Min(CHUNK_SIZE, plainBytes.Length - offset);
                                await gz.WriteAsync(plainBytes.AsMemory(offset, count), token);
                                if (plainBytes.Length >= STREAM_THRESHOLD)
                                {
                                    var pct = (float)offset / Math.Max(1, plainBytes.Length);
                                    progress?.Report(0.1f + pct * 0.6f);
                                }
                            }
                        }
                    }
                    else
                    {
                        using var outFs = new FileStream(bodyTmpPath, FileMode.Create, FileAccess.Write, FileShare.None, CHUNK_SIZE, true);
                        for (int offset = 0; offset < plainBytes.Length; offset += CHUNK_SIZE)
                        {
                            token.ThrowIfCancellationRequested();
                            var count = Math.Min(CHUNK_SIZE, plainBytes.Length - offset);
                            await outFs.WriteAsync(plainBytes.AsMemory(offset, count), token);
                            if (plainBytes.Length >= STREAM_THRESHOLD)
                            {
                                var pct = (float)offset / Math.Max(1, plainBytes.Length);
                                progress?.Report(0.1f + pct * 0.6f);
                            }
                        }
                    }

                    bytesForChecksum = new FileInfo(bodyTmpPath).Length;
                }

                // 3) Compute checksum over FINAL BODY BYTES (from temp file, streaming)
                using (var sha = SHA256.Create())
                using (var inFs = new FileStream(bodyTmpPath, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE, true))
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(CHUNK_SIZE);
                    try
                    {
                        int read;
                        while ((read = await inFs.ReadAsync(buffer, 0, CHUNK_SIZE, token)) > 0)
                            sha.TransformBlock(buffer, 0, read, null, 0);
                        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    header.Checksum = ToHex(sha.Hash!);
                }

                // 4) Compose: write header + newline + stream-copy body temp into final file atomically
                var finalPath = autosaveIndex.HasValue ? _storage.PathForAutosave(profile, autosaveIndex.Value) : _storage.PathForSlot(profile, slot);
                using (var writer = new FileStreamWriter(finalPath))
                {
                    var headerBytes = Encoding.UTF8.GetBytes(_serializer.Serialize(header));
                    await writer.WriteAsync(headerBytes, token);
                    await writer.WriteAsync(new byte[] { (byte)'\n' }, token);

                    using var tmpFs = new FileStream(bodyTmpPath, FileMode.Open, FileAccess.Read, FileShare.Read, CHUNK_SIZE, true);
                    var buf = ArrayPool<byte>.Shared.Rent(CHUNK_SIZE);
                    try
                    {
                        int read;
                        long copied = 0;
                        while ((read = await tmpFs.ReadAsync(buf, 0, CHUNK_SIZE, token)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            await writer.WriteAsync(new ReadOnlyMemory<byte>(buf, 0, read), token);
                            copied += read;
                            progress?.Report(0.7f + (float)copied / Math.Max(1, bytesForChecksum) * 0.3f);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buf);
                    }

                    writer.Commit();
                }

                // 5) Clean temp
                try { if (File.Exists(bodyTmpPath)) File.Delete(bodyTmpPath); } catch { /* best-effort */ }

                var approxBytes = new FileInfo(finalPath).Length;

                // 6) Diagnostics + metadata
                var duration = sw.ElapsedMilliseconds;
                var compressionRatio = (plainBytes.LongLength > 0) ? Math.Round(bytesForChecksum / (double)plainBytes.LongLength, 4) : 1.0;
                var pipeline =
                    !string.IsNullOrEmpty(header.Encrypt) && !string.IsNullOrEmpty(header.Compress) ? "gzip+aes-gcm" :
                    !string.IsNullOrEmpty(header.Encrypt) ? "aes-gcm" :
                    !string.IsNullOrEmpty(header.Compress) ? "gzip" : "plain";

                var diag = SaveProfiler.End(bytesForChecksum, 0, compressionRatio, pipeline);
                SaveProfiler.WriteJson(_storage.RootPath, diag, _serializer);

                if (autosaveIndex.HasValue)
                    _storage.Write(_storage.MetaPathForAutosave(profile, autosaveIndex.Value),
                        _serializer.Serialize(MetadataUtility.CreateForAutosave(autosaveIndex.Value, profile, duration, options.Summary, approxBytes)));
                else
                    _storage.Write(_storage.MetaPathForSlot(profile, slot),
                        _serializer.Serialize(MetadataUtility.CreateForManual(slot, profile, duration, options.Summary, approxBytes)));

                foreach (var s in _saveables) s.OnAfterLoad(); // optional parity hook

                var result = new SaveResult(ResultStatus.Ok, SaveErrorTranslator.Friendly(ResultStatus.Ok),
                                            finalPath, duration, bytesForChecksum, compressionRatio);

                SaveSignals.EmitAfterSave(profile, slotKey, duration, diag);
                progress?.Report(1f);
                return result;
            }
            catch (OperationCanceledException)
            {
                return new SaveResult(ResultStatus.Error, "Save canceled.", autosaveIndex.HasValue ? _storage.PathForAutosave(profile, autosaveIndex.Value) : _storage.PathForSlot(profile, slot), 0, 0, 1.0);
            }
            catch (Exception ex)
            {
                var msg = SaveErrorTranslator.Friendly(ex);
                return new SaveResult(ResultStatus.Error, msg, autosaveIndex.HasValue ? _storage.PathForAutosave(profile, autosaveIndex.Value) : _storage.PathForSlot(profile, slot), 0, 0, 1.0);
            }
        }


        // --------- Helpers ---------

        /// <summary>
        /// Reads bytes up to and including the first '\n' (0x0A) and returns the bytes *without* the newline.
        /// Leaves the stream positioned immediately after the newline. Handles optional '\r' before '\n'.
        /// </summary>
        private static async Task<byte[]> ReadHeaderLineBytesAsync(FileStream fs, CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(CHUNK_SIZE);
            try
            {
                using var headerMs = new MemoryStream(256);
                while (true)
                {
                    int read = await fs.ReadAsync(buffer, 0, CHUNK_SIZE, token);
                    if (read <= 0)
                        break;

                    for (int i = 0; i < read; i++)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            // Write everything before '\n', but drop an optional preceding '\r'
                            int segmentLen = i;
                            if (segmentLen > 0 && buffer[i - 1] == (byte)'\r')
                                segmentLen--;

                            headerMs.Write(buffer, 0, segmentLen);

                            // Reposition stream to byte right after '\n'
                            long overshoot = read - (i + 1);
                            if (overshoot > 0)
                                fs.Seek(-overshoot, SeekOrigin.Current);

                            return headerMs.ToArray();
                        }
                    }

                    // No newline in this chunk; write all and continue
                    headerMs.Write(buffer, 0, read);
                }

                // If we get here, no newline was found; return entire file as header (error case)
                return headerMs.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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
            var target = s.AsComponent;
            var t = target.GetType();

            // Prefer generated capture if available.
            var gen = GeneratedSnapshotCache.Get(t);
            if (gen != null && gen.Capture != null && gen.SnapshotType != null)
            {
                // Capture → DTO object (typed), then serialize using generic method with the DTO runtime type.
                var dtoObj = gen.Capture.Invoke(target, Array.Empty<object?>());
                var dtoJson = SerializeToType(dtoObj, gen.SnapshotType);

                // Store a single synthetic entry named "__dto" with the DTO JSON and its Type.
                var entry = new FieldEntry
                {
                    Name = "__dto",
                    Type = gen.SnapshotType.AssemblyQualifiedName ?? gen.SnapshotType.FullName ?? "Snapshot",
                    JsonValue = dtoJson
                };

                return new ComponentSnapshot { Key = key, Fields = new[] { entry } };
            }

            // Fallback: reflection path over [SaveField] members (existing behavior)
            var fields = new List<FieldEntry>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var f in t.GetFields(flags))
            {
                if (f.GetCustomAttribute<SaveFieldAttribute>(true) == null) continue;
                var val = f.GetValue(target);
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
                var t = target.GetType();

                // If generated binding exists AND the snapshot contains a "__dto" entry,
                // deserialize it into the exact generated DTO type and call RestoreSnapshot.
                var gen = GeneratedSnapshotCache.Get(t);
                if (gen != null && gen.SnapshotType != null)
                {
                    var dtoEntry = snap.Fields.FirstOrDefault(f => f.Name == "__dto");
                    if (!string.IsNullOrEmpty(dtoEntry.JsonValue))
                    {
                        // Deserialize DTO as the precise generated type (NOT as object)
                        var dtoObj = DeserializeToType(dtoEntry.JsonValue!, gen.SnapshotType);

                        if (gen.RestoreTyped != null)
                        {
                            gen.RestoreTyped.Invoke(target, new[] { dtoObj });
                            continue; // done with this component
                        }
                        if (gen.RestoreObject != null)
                        {
                            gen.RestoreObject.Invoke(target, new[] { dtoObj });
                            continue; // done with this component
                        }

                        // If we had a DTO but neither restore method existed, fall back to reflection
                        // (unlikely, but keeps things resilient).
                    }
                }

                // Reflection fallback: assign individual [SaveField] members.
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fieldMap = t
                    .GetFields(flags)
                    .Where(f => f.GetCustomAttribute<SaveFieldAttribute>(true) != null)
                    .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

                foreach (var entry in snap.Fields)
                {
                    if (!fieldMap.TryGetValue(entry.Name, out var field)) continue;
                    var value = DeserializeToType(entry.JsonValue, field.FieldType);
                    field.SetValue(target, value);
                }
            }
        }


        private static string GetHierarchyPath(GameObject go)
        {
            var stack = new Stack<string>();
            var t = go.transform;
            while (t != null) { stack.Push(t.name); t = t.parent; }
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

        private string SerializeGeneric(object? value, Type type)
        {
            var m = typeof(ISerializer).GetMethod(nameof(ISerializer.Serialize))!;
            var g = m.MakeGenericMethod(type);
            return (string)g.Invoke(_serializer, new object?[] { value })!;
        }

        private object? DeserializeGeneric(string json, Type type)
        {
            var m = typeof(ISerializer).GetMethod(nameof(ISerializer.Deserialize))!;
            var g = m.MakeGenericMethod(type);
            return g.Invoke(_serializer, new object?[] { json });
        }

        // ====== CRITICAL FIX: Box primitive/string values so JsonUtility writes real data ======

        /// <summary>
        /// Serialize any value of 'type' by wrapping it in Box&lt;T&gt; so JsonUtility will emit the data.
        /// Produces JSON like: {"Value":42} or {"Value":"Alice"}.
        /// </summary>
        private string SerializeToType(object? value, Type type)
        {
            var boxType = typeof(Box<>).MakeGenericType(type);
            var box = Activator.CreateInstance(boxType);
            var valField = boxType.GetField("Value");
            if (valField != null) valField.SetValue(box, value);

            var m = typeof(ISerializer).GetMethod(nameof(ISerializer.Serialize))!;
            var g = m.MakeGenericMethod(boxType);
            return (string)g.Invoke(_serializer, new object?[] { box })!;
        }

        /// <summary>
        /// Deserialize JSON produced by SerializeToType back into the requested type by reading Box&lt;T&gt;.Value.
        /// </summary>
        private object? DeserializeToType(string json, Type type)
        {
            var boxType = typeof(Box<>).MakeGenericType(type);
            var m = typeof(ISerializer).GetMethod(nameof(ISerializer.Deserialize))!;
            var g = m.MakeGenericMethod(boxType);
            var box = g.Invoke(_serializer, new object?[] { json });
            var valField = boxType.GetField("Value");
            return valField != null && box != null ? valField.GetValue(box) : null;
        }

        private static string ChecksumHex(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return ToHex(hash);
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static byte[] RandomNonce(int len)
        {
            var b = new byte[len];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(b);
            return b;
        }

        // Wrappers to satisfy JsonUtility (top-level arrays)
        [Serializable]
        private struct ArrayItemsWrapper<T> { public T[] Items; }

        [Serializable]
        private struct ArrayComponentsWrapper<T> { public T[] Components; }

        // Box used to serialize individual field values
        [Serializable]
        private struct Box<T> { public T Value; }

        private static async Task<byte[]> ReadToEndAsync(Stream stream, IProgress<float>? progress, CancellationToken token)
        {
            const int BufSize = CHUNK_SIZE;
            var ms = new MemoryStream(capacity: 64 * 1024);
            var buffer = ArrayPool<byte>.Shared.Rent(BufSize);
            try
            {
                long totalRead = 0;
                long remainingKnown = -1;

                if (stream is FileStream fs)
                {
                    try { remainingKnown = Math.Max(0, fs.Length - fs.Position); }
                    catch { remainingKnown = -1; }
                }

                int read;
                while ((read = await stream.ReadAsync(buffer, 0, BufSize, token)) > 0)
                {
                    await ms.WriteAsync(buffer, 0, read, token);
                    totalRead += read;

                    if (remainingKnown > 0 && progress != null)
                    {
                        var frac = (float)totalRead / Math.Max(1, remainingKnown);
                        progress.Report(frac);
                    }
                }

                return ms.ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
