# 02 - Core Concepts

This guide explains how Aion models save data and runs save/load operations.

---

## 1) Primary responsibilities

| Type | Responsibility |
| --- | --- |
| `SaveManager` | Orchestrates capture, serialize, transform, write/read, and restore. |
| `ISaveable` | Opt-in contract for scene components that provide save state. |
| `ISerializer` | Serialization abstraction (JSON by default). |
| `IStorageProvider` / `FileSystemStorage` | File pathing and atomic write behavior. |
| `ICompressor` | Optional compression transform (e.g., GZip). |
| `IEncryptor` | Optional encryption transform (e.g., AES-GCM). |

---

## 2) Save pipeline (high-level)

Aion processes data in this order:

1. Capture all registered saveable snapshots.
2. Serialize snapshots to JSON.
3. Optionally compress.
4. Optionally encrypt.
5. Compute checksum over final body bytes.
6. Write file atomically (header + body).

### Why this matters

* The checksum is validated against the stored body bytes before decode transforms.
* Corruption can be detected early and safely.

---

## 3) Load pipeline (high-level)

1. Open save file and parse header line.
2. Read body bytes.
3. Validate checksum.
4. Decrypt/decompress according to header metadata.
5. Deserialize snapshots.
6. Apply values back to registered saveables.
7. Trigger post-load hooks.

---

## 4) Profiles and slots

Aion scopes save files by **profile** and **slot**.

* Example manual save: `slot_1.bpgsave`
* Example autosave slot key: `slot_autosave_0.bpgsave`

Use profiles to separate player identities, regions, or progression branches.

---

## 5) `SaveOptions` vs `LoadOptions`

`SaveOptions` controls what is written (compression/encryption flags, profile, metadata).

`LoadOptions` primarily provides context (`ProfileName`), while transform interpretation is read from the save header (header is authoritative).

---

## 6) Save file composition

Aion save file format:

1. One UTF-8 JSON header line.
2. Newline separator (`\n`).
3. Raw body bytes.

This design allows quick metadata inspection while keeping body handling stream-friendly.

---

## 7) Runtime hooks and events

`ISaveable` hooks:

* `OnBeforeSave()` before snapshot capture.
* `OnAfterLoad()` after snapshot restore.

Use hooks to synchronize transient runtime state (e.g., animator refresh, effect rebuilds).

---

## 8) Determinism guidance

To keep deterministic behavior strong:

* Use stable keys (`[SaveKey]` or `SaveKeyOverride`).
* Avoid persisting volatile/non-authoritative values.
* Keep migration strategy for renamed fields/types.
