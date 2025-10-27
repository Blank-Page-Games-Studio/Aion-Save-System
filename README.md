# Aion Save System

Aion Save System is a modular, deterministic save/load framework for Unity 6.2+. It provides clean JSON serialization, optional compression and encryption, multi-slot profiles, autosave, streaming writes, diagnostics, and **compile‑time snapshot generation** (Phase 7) to eliminate runtime reflection overhead.

**Namespace:** `BPG.Aion`  
**UPM Package:** `com.bpg.aion`

---

## ✨ Features
- Deterministic JSON serialization (JsonUtility by default, optional Newtonsoft if present)
- File-based saving with **atomic writes** and **checksum validation**
- Optional **GZip compression** and **AES‑GCM encryption**
- Multi-profile & multi-slot management with slot metadata
- **Autosave** with rolling backups
- **Streaming I/O** for large saves with progress & cancellation
- **Diagnostics** via `SaveProfiler` (durations, sizes, compression ratio)
- **Editor code generation (Phase 7):** Compile-time snapshot DTOs & capture/restore to skip reflection
- Extensible architecture: `ISerializer`, `IStorageProvider`, `ICompressor`, `IEncryptor`

---

## ⚙️ Installation

### Install via Git URL (Recommended)
1. Open **Unity Editor**.
2. Go to **Window → Package Manager**.
3. Click **+** → **Add package from Git URL...**
4. Paste the URL and press Enter:
   ```
   https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.2
   ```

### Add to `manifest.json` manually
```json
"com.bpg.aion": "https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.2"
```

### Manual Install (Offline)
1. Download/copy `Packages/com.bpg.aion/` into your project.
2. In Unity: **Package Manager → Add package from disk…**, select `package.json`.

---

## 🚀 Quick Start

### 1) Implement a saveable component (reflection path)
```csharp
using BPG.Aion;
using UnityEngine;

public sealed class PlayerStats : MonoBehaviour, ISaveable
{
    [SaveField] public int level;
    [SaveField] public float health;
    [SaveField] public Vector3 position;

    public string? SaveKeyOverride => "PlayerStats:Main";
    public Component AsComponent => this;
    public void OnBeforeSave() { }
    public void OnAfterLoad() { }
}
```

### 2) Save / Load
```csharp
var manager = new SaveManager(new JsonSerializer(), new FileSystemStorage());
manager.Register(playerStats);

await manager.SaveAsync(1, new SaveOptions{ ProfileName = "Default" });
await manager.LoadAsync(1, new LoadOptions{ ProfileName = "Default" });
```

### 3) Sample UI
A simple async prefab lives under `Samples~/SimpleThreeSlot` with Save/Load/Delete, progress bar, and status text.

---

## 🧠 Phase 7 — Compile‑Time Snapshot Generation

**Goal:** remove runtime reflection overhead for `[SaveField]` by generating strongly‑typed snapshot DTOs & methods **inside the Editor**.

- **Opt‑in:** mark saveable classes with `[GenerateSaveSnapshot]`.
- **Requirement:** the class must be `partial` and implement `ISaveable`.
- **Output:** a file under `Assets/Aion.Generated/{Namespace_TypeName}_Generated.cs` containing:
  - A nested `struct Snapshot` (or custom name) mirroring `[SaveField]` members
  - `object CaptureSnapshot()` and `void RestoreSnapshot(object)` partial implementations
  - Optional `string SaveKey` property if `SaveKeyOverride` is provided
- **Deterministic ordering:** first by `[SaveOrder(int)]`, then by declaration order.
- **Runtime behavior:** `SaveManager` checks for generated `CaptureSnapshot`/`RestoreSnapshot`; if found, it uses them, otherwise it falls back to reflection.

### Attributes
```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class GenerateSaveSnapshotAttribute : Attribute
{
    public bool IncludeProperties { get; set; } = true;      // include [SaveField] properties
    public bool IncludeNonPublic { get; set; } = false;       // include private/protected with [SaveField]
    public string? SnapshotName { get; set; } = null;         // defaults to "Snapshot"
    public string? SaveKeyOverride { get; set; } = null;      // emit SaveKey property with constant value
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SaveOrderAttribute : Attribute
{
    public int Order { get; }
    public SaveOrderAttribute(int order) => Order = order;
}
```

### Example
```csharp
using BPG.Aion;
using UnityEngine;

[GenerateSaveSnapshot(SnapshotName = "Snapshot", IncludeProperties = true, SaveKeyOverride = "Player")]
public partial class PlayerSave : MonoBehaviour, ISaveable
{
    [SaveField, SaveOrder(0)] public int level;
    [SaveField, SaveOrder(1)] private int _hp;
    [SaveField] public string Name { get; set; } = "Rin";

    public string? SaveKeyOverride => null; // generator emits SaveKey when SaveKeyOverride was set in attribute
    public Component AsComponent => this;
    public void OnBeforeSave() { }
    public void OnAfterLoad() { }
}
```

### Generate the code
- Open **Window → BPG Aion → Snapshot Generator**.
- Click **Generate All**. Files appear under `Assets/Aion.Generated/`.
- The generator also runs on domain reload (best-effort) to keep outputs current.

### How the runtime uses it
- When saving, `SaveManager` checks (and caches) the presence of `CaptureSnapshot()` and uses it to produce a single `__dto` JSON field for the component.
- When loading, `SaveManager` deserializes the DTO and invokes `RestoreSnapshot(object)`.
- If generated APIs are absent, reflection is used—zero changes required in your existing code.

### Troubleshooting
- **No generated file?** Ensure the class is `partial`, is a `MonoBehaviour`, implements `ISaveable`, and is annotated with `[GenerateSaveSnapshot]`.
- **Build errors in generated file?** Check that every `[SaveField]` type is serializable by your chosen serializer (JsonUtility/Newtonsoft). Adjust members or custom converters accordingly.
- **Ordering seems off?** Apply `[SaveOrder]`. Members without `[SaveOrder]` are placed after ordered items, using declaration order.

---

## 🧩 Architecture Overview (recap)
- **ISaveable:** Identifies saveable components; Phase 7 adds generated capture/restore support.
- **SaveManager:** Orchestrates save/load, prefers generated snapshot methods when present.
- **ISerializer / IStorageProvider / ICompressor / IEncryptor:** Pluggable infrastructure.
- **AutosaveController:** Interval & scene‑change driven autosaving with rolling backups.
- **SaveProfiler:** Optional diagnostics JSON under save root.

---

## 🧪 Testing
Open **Test Runner** and run the EditMode suite:
- Round‑trip integrity (reflection and generated)
- Header checksum verification
- Compression & encryption round‑trip
- Autosave rotation
- Phase 7 tests for codegen compilation, capture/restore correctness, and `[SaveOrder]` behavior

---

## ❓ FAQ (Phase 7 extras)
**Q: Do I need to change my existing saveables?**  
No. Generation is opt‑in. Unannotated classes continue to use reflection.

**Q: Can I customize the snapshot struct name?**  
Yes: `SnapshotName = "MySnapshot"`.

**Q: Can I include private fields?**  
Yes: set `IncludeNonPublic = true`, but they still require `[SaveField]`.

---

## 🧱 Roadmap (excerpt)
- ✅ Phase 3: UX polish, diagnostics, streaming writes
- ✅ Phase 7: Compile‑time snapshot generation (this release)
- ▶ DOTS/ECS support & cloud adapters in later phases

---

## 📜 License
Licensed under the **Apache License 2.0**. See `LICENSE` for details.
