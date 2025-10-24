// com.bpg.aion/Runtime/Serialization/JsonSerializer.cs
#nullable enable
using System;
using UnityEngine;

namespace BPG.Aion
{
    /// <summary>
    /// Unity-safe JSON serializer backed by <see cref="JsonUtility"/>.
    /// Notes:
    /// - JsonUtility cannot serialize top-level primitives; we box primitives/strings into a wrapper.
    /// - Complex DTOs (marked [Serializable] with fields) are serialized directly.
    /// - Output is compact (non-indented) and deterministic given fixed field order.
    /// </summary>
    public sealed class JsonSerializer : ISerializer
    {
        /// <summary>
        /// Serialize a value to JSON. Primitives/strings are boxed into { "v": ... } for safety.
        /// </summary>
        public string Serialize<T>(T value)
        {
            var t = typeof(T);

            // Handle null explicitly when T is a reference/nullable type
            if (value is null)
            {
                // JsonUtility doesn't emit "null"; we keep a stable representation by boxing.
                if (NeedsBoxing(t))
                    return JsonUtility.ToJson(new Box<object?> { v = null }, false);

                // For complex DTOs, an empty object keeps shape, but FromJson will yield default(T).
                // Still prefer boxing for determinism; fall back to {} only if T is a class/struct we don't box.
                return "{}";
            }

            if (NeedsBoxing(t))
            {
                var boxed = new Box<T> { v = value };
                return JsonUtility.ToJson(boxed, false);
            }

            return JsonUtility.ToJson(value, false);
        }

        /// <summary>
        /// Deserialize a value from JSON. Accepts either boxed form ({ "v": ... }) or attempts a best-effort
        /// auto-wrap for primitive tokens.
        /// </summary>
        public T Deserialize<T>(string json)
        {
            var t = typeof(T);

            if (NeedsBoxing(t))
            {
                // Fast path: already boxed
                if (LooksBoxed(json))
                    return JsonUtility.FromJson<Box<T>>(json).v;

                // Fallback: wrap a raw token (e.g., 123, "hello", true) into { "v": ... }.
                var wrapped = WrapAsBoxJson(json, t);
                return JsonUtility.FromJson<Box<T>>(wrapped).v;
            }

            // Complex DTO path
            return JsonUtility.FromJson<T>(json);
        }

        // ----- Helpers -----

        /// <summary>
        /// Unity's JsonUtility can't handle top-level primitives/strings. We box those.
        /// </summary>
        private static bool NeedsBoxing(Type t)
        {
            if (t == typeof(string)) return true;
            if (t.IsEnum) return true;

            // Common primitive/value types JsonUtility can’t safely do at top-level
            if (t == typeof(bool) ||
                t == typeof(byte) || t == typeof(sbyte) ||
                t == typeof(short) || t == typeof(ushort) ||
                t == typeof(int) || t == typeof(uint) ||
                t == typeof(long) || t == typeof(ulong) ||
                t == typeof(float) ||
                t == typeof(double)) // double is supported when boxed
            {
                return true;
            }

            // Everything else (DTOs with [Serializable] + fields) we serialize directly
            return false;
        }

        private static bool LooksBoxed(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var trimmed = json.TrimStart();
            // very lightweight check to avoid allocations
            return trimmed.Length >= 5 && trimmed[0] == '{' && trimmed.IndexOf("\"v\"", StringComparison.Ordinal) >= 0;
        }

        private static string WrapAsBoxJson(string raw, Type targetType)
        {
            // If target is string and the token isn't quoted, quote it.
            var trimmed = raw?.Trim() ?? string.Empty;

            if (targetType == typeof(string))
            {
                if (!IsQuoted(trimmed))
                {
                    trimmed = Quote(trimmed);
                }
                return "{\"v\":" + trimmed + "}";
            }

            // For non-strings, trust the token form (e.g., 123, true, 1.5)
            // If it's empty, use null to stay valid JSON.
            if (string.IsNullOrEmpty(trimmed))
                trimmed = "null";

            return "{\"v\":" + trimmed + "}";
        }

        private static bool IsQuoted(string s)
        {
            return s.Length >= 2 && s[0] == '\"' && s[^1] == '\"';
        }

        private static string Quote(string s)
        {
            // Minimal quoting for deterministic output. JsonUtility will parse this fine.
            // Escape backslashes and quotes.
            var escaped = s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }

        /// <summary>
        /// Serializable wrapper for boxing primitives/strings.
        /// </summary>
        [Serializable]
        private sealed class Box<TValue>
        {
            // Must be a field for JsonUtility.
            public TValue v = default!;
        }
    }
}
