# 03 - Configuration and Autosave

This guide covers project-level settings and autosave runtime behavior.

---

## 1) Create settings asset

Create a settings asset via:

**Create → BPG → Aion → Save Settings**

`AionSaveSettings` stores project defaults for:

* Profile naming
* Save folder resolution
* Streaming chunk sizing
* Compression and encryption toggles
* Autosave and scene-change behavior

---

## 2) Effective values and normalization

Runtime uses validated/clamped values (`AionSaveSettingsEffective`) computed from the asset.

Important normalization behavior:

* Empty profile/folder values are replaced by defaults.
* Chunk size is clamped and normalized.
* Compression threshold is clamped.
* Autosave interval/backup count/debounce are clamped.

This prevents invalid settings from causing runtime failures.

---

## 3) Save path behavior

When **Use Persistent Data Path** is enabled:

```text
Path.Combine(Application.persistentDataPath, RelativeSaveFolder)
```

When disabled, `RelativeSaveFolder` is used directly.

Recommendation: keep persistent path enabled for production player builds.

---

## 4) Compression and encryption

* Compression is optional and controlled by settings/options.
* Encryption support is configurable by scheme and key provider identifiers.
* Header metadata records applied transforms.

If enabled transforms are unavailable at load time, the load returns an explicit error result.

---

## 5) Autosave controller behavior

`AutosaveController` supports:

* Fixed-interval autosave
* Scene-change-triggered autosave (with debounce)
* Rolling backup index rotation

### Rolling backups

Autosave writes to rotating indices `0..max-1`, providing simple retention without unbounded file growth.

### Operational tip

Set interval and scene-change autosave according to your game pacing (fast loop games need shorter intervals).

---

## 6) Recommended baseline profile

A safe starting setup for many projects:

* Compression: enabled
* Encryption: disabled at prototype stage
* Autosave: enabled
* Interval: 60 seconds
* Rolling backups: 3
* Scene-change autosave: enabled (3 second debounce)

Then tighten security and retention after gameplay/system validation.
