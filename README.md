# Aion Save System

Aion Save System is a modular, deterministic save/load framework for Unity 6.2+. It provides clean JSON serialization, optional compression and encryption, multi-slot profiles, and autosave functionality.

**Namespace:** `BPG.Aion`  
**UPM Package:** `com.bpg.aion`

---

## ✨ Features
- Deterministic JSON serialization (Newtonsoft or JsonUtility fallback)
- File-based saving with atomic writes
- Checksum-based data integrity validation
- Optional GZip compression and AES-GCM encryption
- Multi-profile and multi-slot management
- Autosave with rolling backups
- Extensible architecture via interfaces (`ISerializer`, `IStorageProvider`, `ICompressor`, `IEncryptor`)

---

## ⚙️ Installation

### Via Git URL (recommended)
Add this line to your Unity project's `manifest.json`:
```json
"com.bpg.aion": "https://github.com/your-org/aion-save-system.git#v1.0.0"
```

### Manual Install
1. Download the package folder.
2. Copy it into your project under `Packages/com.bpg.aion/`.
3. Open Unity → Package Manager → "Add package from disk…" and select the `package.json` file.

---

## 🚀 Quick Start

### 1. Implement a Saveable Component
```csharp
using BPG.Aion;
using UnityEngine;

public sealed class PlayerStats : MonoBehaviour, ISaveable
{
    [SaveField] public int level;
    [SaveField] public float health;
    [SaveField] public Vector3 position;

    public string SaveKey => "PlayerStats:Main";

    public object CaptureSnapshot() => new Snapshot
    {
        level = level,
        health = health,
        position = new Vec3(position)
    };

    public void RestoreSnapshot(object obj)
    {
        var s = (Snapshot)obj;
        level = s.level;
        health = s.health;
        transform.position = s.position.ToUnity();
    }

    [System.Serializable]
    public struct Snapshot
    {
        public int level;
        public float health;
        public Vec3 position;
    }
}
```

### 2. Save and Load with `SaveManager`
```csharp
var manager = new SaveManager(new JsonSerializer(), new FileSystemStorage());
manager.Register(playerStats);

manager.Save(1);  // Save to slot 1
manager.Load(1);  // Load from slot 1
```

### 3. Using the Sample UI
A ready-to-use prefab is included under `Samples~/Basic`.  
It includes:
- Save, Load, and Delete buttons for 3 slots.
- Status text for feedback.

Add the prefab to your scene and connect your `ISaveable` components to the `SaveManager` reference.

---

## 🧩 Architecture Overview
- **ISaveable:** Interface for saveable game objects.
- **ISerializer:** Encapsulates serialization strategy (JSON or binary).
- **IStorageProvider:** Handles file I/O and atomic writes.
- **SaveManager:** Orchestrates the save/load pipeline.
- **ICompressor / IEncryptor:** Optional transforms for compression/encryption.
- **AutosaveController:** Timer-based autosave system with rotation.
- **SaveSignals:** Event hooks for save/load analytics.

---

## 🧪 Testing
All unit tests are included in the `Tests/EditMode` folder and can be run via Unity Test Runner.

Key tests:
- Save/load round-trip integrity
- Header checksum validation
- Compression and encryption correctness
- Autosave rotation

---

## ❓ FAQ

**Q: Where are save files stored?**  
In `Application.persistentDataPath/BPG.Saves/{ProfileName}/slot_{N}.bpgsave`.

**Q: Can I use my own serializer?**  
Yes. Implement `ISerializer` and plug it into the `SaveManager` constructor.

**Q: Does it work with ScriptableObjects or ECS?**  
ScriptableObjects are supported via GUID reference; ECS support will be added in a future phase.

**Q: Is encryption mandatory?**  
No. It’s optional and configurable via `SaveOptions`.

---

## 🧱 Roadmap
- Phase 3: UX polish, diagnostics, and streaming writes.
- Phase 4: DOTS/ECS support.
- Phase 5: Source generation for `[SaveField]`.
- Phase 6: Cloud storage adapters.

---

## 📜 License
Licensed under the **Apache License 2.0**. See `LICENSE` for details.
