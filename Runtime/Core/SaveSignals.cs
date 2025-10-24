// com.bpg.aion/Runtime/Core/SaveSignals.cs
#nullable enable
using System;

namespace BPG.Aion
{
    /// <summary>
    /// Simple lifecycle signals for diagnostics and hooks.
    /// </summary>
    public static class SaveSignals
    {
        public static event Action<string, string, SaveOptions>? BeforeSave; // profile, slotKey
        public static event Action<string, string, long, SaveResult>? AfterSave; // profile, slotKey, durationMs, result
        public static event Action<string, string, LoadOptions>? BeforeLoad;
        public static event Action<string, string, long, LoadResult>? AfterLoad;

        internal static void EmitBeforeSave(string profile, string slotKey, SaveOptions opts) => BeforeSave?.Invoke(profile, slotKey, opts);
        internal static void EmitAfterSave(string profile, string slotKey, long durationMs, SaveResult res) => AfterSave?.Invoke(profile, slotKey, durationMs, res);
        internal static void EmitBeforeLoad(string profile, string slotKey, LoadOptions opts) => BeforeLoad?.Invoke(profile, slotKey, opts);
        internal static void EmitAfterLoad(string profile, string slotKey, long durationMs, LoadResult res) => AfterLoad?.Invoke(profile, slotKey, durationMs, res);
    }
}
