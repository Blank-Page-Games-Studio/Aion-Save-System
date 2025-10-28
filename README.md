# Aion Save System

**Aion Save System** is a modular and deterministic save/load framework for **Unity 6.2+**.
It provides robust, efficient, and flexible game state persistence using clean JSON serialization, optional compression/encryption, autosaving, and strong data integrity verification.

**Namespace:** `BPG.Aion`
**UPM Package:** `com.bpg.aion`

---

## ✨ Key Features

* Deterministic JSON serialization with stable field ordering.
* Atomic file writes and built-in checksum validation.
* Optional **GZip compression** and **AES-GCM encryption**.
* Multi-profile and multi-slot save system.
* Autosave controller with rolling backups.
* Streaming I/O for large saves (progress and cancellation support).
* Lightweight diagnostics via `SaveProfiler` (timing, memory, compression ratio).
* Extensible architecture with interfaces for custom serializers, storage, and encryption.
* Editor integration for easy debugging and testing.

---

## ⚙️ Installation

### Install via Git URL (Recommended)

1. Open **Unity Editor**.
2. Go to **Window → Package Manager**.
3. Click **+ → Add package from Git URL...**
4. Paste the following URL and press Enter:

   ```
   https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.8
   ```

### Add to `manifest.json`

```json
"com.bpg.aion": "https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.8"
```

### Manual Install (Offline)

1. Download or copy the folder `Packages/com.bpg.aion/` into your project.
2. In Unity: **Package Manager → Add package from disk…** and select `package.json`.

---

## 🚀 Quick Start

### 1. Create a Saveable Component

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

    public void OnBeforeSave() { Debug.Log("Preparing save"); }
    public void OnAfterLoad() { Debug.Log("Data loaded"); }
}
```

### 2. Save and Load Example

```csharp
var manager = new SaveManager(new JsonSerializer(), new FileSystemStorage());
manager.Register(playerStats);

await manager.SaveAsync(1, new SaveOptions { ProfileName = "Default" });
await manager.LoadAsync(1, new LoadOptions { ProfileName = "Default" });
```

### 3. Use the Sample Prefab

A simple prefab is available under `Samples~/SimpleThreeSlot`. It includes:

* 3 save slots with Save, Load, and Delete buttons.
* Progress feedback and status display.

---

## 🧩 Architecture Overview

| Component                | Role                                                                                             |
| ------------------------ | ------------------------------------------------------------------------------------------------ |
| **`SaveManager`**        | Coordinates all save/load operations and manages the pipeline.                                   |
| **`ISaveable`**          | Interface implemented by any saveable component. Fields marked with `[SaveField]` are persisted. |
| **`ISerializer`**        | Handles serialization logic (JSON by default). Custom serializers can be injected.               |
| **`IStorageProvider`**   | Manages low-level file operations, atomic writes, and backups.                                   |
| **`ICompressor`**        | Optional interface for data compression. Default: `GZipCompressor`.                              |
| **`IEncryptor`**         | Optional interface for encryption. Default: `AesGcmEncryptor`.                                   |
| **`AutosaveController`** | Periodically triggers saves or responds to scene changes.                                        |
| **`SaveProfiler`**       | Measures duration, memory allocation, and I/O stats per operation.                               |

---

## 🧠 Data Flow Summary

1. **Capture:** Each registered `ISaveable` exposes its `[SaveField]` values.
2. **Serialize:** All data is serialized into JSON.
3. **Transform:** (Optional) Compress → (Optional) Encrypt → Compute checksum.
4. **Write:** Save atomically to disk with a JSON header + body.
5. **Load:** Reverse process (read, verify, decrypt, decompress, restore).

This process ensures consistent and verifiable saves with minimal overhead.

---

## 🧪 Testing

Run **Unity Test Runner → EditMode Tests** to validate system integrity. Tests cover:

* Round-trip save/load correctness.
* Header checksum and corruption detection.
* Compression and encryption validation.
* Autosave rotation and recovery.
* Performance and memory profiling.

---

## ❓ FAQ

**Q: Where are save files stored?**
`Application.persistentDataPath/BPG.Saves/{ProfileName}/slot_{N}.bpgsave`

**Q: Can I use my own serialization format?**
Yes. Implement `ISerializer` and inject it into `SaveManager`.

**Q: Is encryption required?**
No. Compression and encryption are fully optional.

**Q: How does it handle file corruption?**
Every save includes a checksum verified on load. Corrupted files will not overwrite valid data.

**Q: Can it autosave automatically?**
Yes. Add `AutosaveController` to your scene and configure interval and backup count.

---

## 🧱 Roadmap

* ✅ Deterministic JSON serialization and reflection-based saves
* ✅ Compression and encryption support
* ✅ Multi-slot profiles with autosave
* ✅ Streaming I/O with progress tracking
* ✅ Editor integration for snapshot optimization
* ▶ Cloud save, ECS/DOTS support, and remote storage (planned)

---

## 📜 License

Licensed under the **Apache License 2.0**. See `LICENSE` for details.