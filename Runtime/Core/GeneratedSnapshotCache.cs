#nullable enable
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace BPG.Aion
{
    /// <summary>
    /// Caches generated snapshot bindings per component type
    /// to avoid repeated reflection calls at runtime.
    /// </summary>
    internal static class GeneratedSnapshotCache
    {
        internal sealed class Binding
        {
            public Type SnapshotType = null!;
            public MethodInfo? Capture;        // CaptureSnapshot(): object or Snapshot
            public MethodInfo? RestoreTyped;   // RestoreSnapshot(Snapshot)
            public MethodInfo? RestoreObject;  // RestoreSnapshot(object)
        }

        private static readonly ConcurrentDictionary<Type, Binding?> _cache = new();

        public static Binding? Get(Type componentType)
        {
            return _cache.GetOrAdd(componentType, Resolve);
        }

        private static Binding? Resolve(Type t)
        {
            // Look for nested 'Snapshot' type (default name)
            var snapshot = t.GetNestedType("Snapshot", BindingFlags.Public | BindingFlags.NonPublic);
            if (snapshot == null)
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Correct overloads: specify all arguments explicitly
            var capture = t.GetMethod(
                "CaptureSnapshot",
                flags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null
            );

            var restoreTyped = t.GetMethod(
                "RestoreSnapshot",
                flags,
                binder: null,
                types: new[] { snapshot },
                modifiers: null
            );

            var restoreObj = t.GetMethod(
                "RestoreSnapshot",
                flags,
                binder: null,
                types: new[] { typeof(object) },
                modifiers: null
            );

            return new Binding
            {
                SnapshotType = snapshot,
                Capture = capture,
                RestoreTyped = restoreTyped,
                RestoreObject = restoreObj
            };
        }
    }
}
