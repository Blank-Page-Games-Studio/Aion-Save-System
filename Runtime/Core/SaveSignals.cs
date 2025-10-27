// com.bpg.aion/Runtime/Core/SaveSignals.cs
#nullable enable
using System;

namespace BPG.Aion
{
    public static class SaveSignals
    {
        public static event Action<string, string, SaveOptions>? BeforeSave;
        public static event Action<string, string, long, SaveDiagnostics>? AfterSave;
        public static event Action<string, string, LoadOptions>? BeforeLoad;
        public static event Action<string, string, long, LoadResult>? AfterLoad;

        internal static void EmitBeforeSave(string profile, string slotKey, SaveOptions opts) => BeforeSave?.Invoke(profile, slotKey, opts);
        internal static void EmitAfterSave(string profile, string slotKey, long durationMs, SaveDiagnostics diag) => AfterSave?.Invoke(profile, slotKey, durationMs, diag);
        internal static void EmitBeforeLoad(string profile, string slotKey, LoadOptions opts) => BeforeLoad?.Invoke(profile, slotKey, opts);
        internal static void EmitAfterLoad(string profile, string slotKey, long durationMs, LoadResult res) => AfterLoad?.Invoke(profile, slotKey, durationMs, res);
    }
}
