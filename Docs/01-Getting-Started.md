# 01 - Getting Started

This guide helps you integrate Aion quickly and safely in a Unity project.

---

## 1) Install the package

Use one of the following methods:

### Option A: Unity Package Manager (Git URL)

1. Open **Window → Package Manager**.
2. Select **+ → Add package from Git URL...**.
3. Paste:

```text
https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.12
```

### Option B: Add to `manifest.json`

```json
"com.bpg.aion": "https://github.com/Blank-Page-Games-Studio/Aion-Save-System.git#v0.7.12"
```

### Option C: Local/offline package

Copy the package folder into your project and add it from disk via Package Manager.

---

## 2) Create your first saveable component

Aion saves fields marked with `[SaveField]` from classes implementing `ISaveable`.

```csharp
using BPG.Aion;
using UnityEngine;

[SaveKey("PlayerStats")]
public sealed class PlayerStats : MonoBehaviour, ISaveable
{
    [SaveField] public int level = 1;
    [SaveField] public float health = 100f;

    public Component AsComponent => this;

    // Optional custom key override at runtime.
    public string? SaveKeyOverride => null;

    public void OnBeforeSave() { }
    public void OnAfterLoad() { }
}
```

### Notes

* `AsComponent` is required so the save manager can identify scene objects reliably.
* `SaveKeyOverride` is optional; use it when you need explicit key control.
* `OnBeforeSave` and `OnAfterLoad` are lifecycle hooks for prep/post logic.

---

## 3) Create and use a `SaveManager`

```csharp
using BPG.Aion;
using UnityEngine;

public sealed class SaveBootstrap : MonoBehaviour
{
    [SerializeField] private PlayerStats _stats = null!;

    private SaveManager _manager = null!;

    private void Awake()
    {
        var serializer = new JsonSerializer();
        var storage = new FileSystemStorage();

        _manager = new SaveManager(serializer, storage);
        _manager.Register(_stats);
    }

    public async void SaveSlot1()
    {
        var result = await _manager.SaveAsync(1, new SaveOptions
        {
            ProfileName = "Default",
            Summary = "Manual Save",
            UseCompression = true,
            UseEncryption = false,
            AppVersion = Application.version
        });

        Debug.Log($"Save status: {result.Status} ({result.Message})");
    }

    public async void LoadSlot1()
    {
        var result = await _manager.LoadAsync(1, new LoadOptions
        {
            ProfileName = "Default"
        });

        Debug.Log($"Load status: {result.Status} ({result.Message})");
    }
}
```

---

## 4) Verify save output location

By default, saves are written beneath:

```text
Application.persistentDataPath/AionSaves/{Profile}/slot_{N}.bpgsave
```

If you use project settings overrides, this path can change (see guide 03).

---

## 5) First integration checklist

* [ ] Saveable classes implement `ISaveable`.
* [ ] Persisted fields are explicitly marked with `[SaveField]`.
* [ ] Saveables are registered to one `SaveManager` instance.
* [ ] `SaveOptions.ProfileName` is deliberate (avoid accidental profile fragmentation).
* [ ] Save and load results are logged and surfaced in UI for debugging.

---

## 6) Next step

Continue to **[02 - Core Concepts](./02-Core-Concepts.md)** to understand how profiles, slots, checksums, and transforms work.
