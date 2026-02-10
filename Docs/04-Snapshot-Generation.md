# 04 - Snapshot Generation

Snapshot generation removes runtime reflection overhead by generating strongly typed capture/restore code.

---

## 1) When to use

Use snapshot generation when:

* You have many saveables.
* Save operations run frequently (autosave-heavy projects).
* You need predictable performance across low-end hardware.

---

## 2) Required component setup

Mark your saveable class:

```csharp
[GenerateSaveSnapshot]
public sealed partial class PlayerStats : MonoBehaviour, ISaveable
{
    [SaveField] public int level;
    [SaveField] public float health;

    public Component AsComponent => this;
}
```

Requirements:

* Class must be `partial`.
* Persisted members still use `[SaveField]`.

---

## 3) Generate from editor

Open:

**Window → BPG Aion → Snapshot Generator**

Then:

1. Scan for eligible `[GenerateSaveSnapshot]` classes.
2. Generate/refresh output files.
3. Commit generated files to source control.

Generated files are written under `Assets/Aion.Generated/`.

---

## 4) Generated pattern (conceptual)

Generated code typically includes:

* A nested serializable snapshot struct.
* `CaptureSnapshot()` method.
* `RestoreSnapshot(object snapshot)` method.

Aion automatically uses generated capture/restore paths when available.

---

## 5) Fallback behavior

If generated code is missing or stale, Aion can still use reflection-based capture/restore.

That means you can adopt snapshot generation incrementally instead of all-at-once.

---

## 6) Version-control advice

* Include generated files in Git to keep builds deterministic.
* Regenerate snapshots after changing save fields.
* Review generated diffs in PRs to catch accidental schema changes.
