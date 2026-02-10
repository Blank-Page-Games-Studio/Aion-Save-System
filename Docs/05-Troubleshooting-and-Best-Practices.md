# 05 - Troubleshooting and Best Practices

This guide helps diagnose common save/load problems and lists production-ready practices.

---

## 1) Common issues

### Issue: Save appears successful but load restores nothing

Checklist:

* Was the component registered with `SaveManager`?
* Are fields marked with `[SaveField]`?
* Are save/load using the same profile and slot?

### Issue: Load returns corruption/error

Checklist:

* Verify file integrity (unexpected external edits or partial writes).
* Confirm compression/encryption support matches saved header metadata.
* Ensure key provider/encryption configuration is consistent between sessions.

### Issue: Autosave feels too frequent or disruptive

Checklist:

* Increase interval.
* Increase scene-change debounce.
* Reduce saved payload size by excluding transient fields.

---

## 2) Production best practices

1. **Use explicit keys** for long-lived saveables.
2. **Keep save schema intentional** (avoid serializing temporary runtime state).
3. **Surface `SaveResult`/`LoadResult` in logs and diagnostics UI**.
4. **Test corrupted file handling** on target platforms.
5. **Use multiple profiles** for QA and migration testing.
6. **Backup before schema changes** in live products.

---

## 3) Performance checklist

* Prefer generated snapshots for hot save paths.
* Keep payload small and normalized.
* Use compression only where data size benefits justify CPU cost.
* Run periodic profiling with realistic data scale.

---

## 4) Security checklist

* Enable encryption for shipped titles handling sensitive data.
* Avoid hardcoded keys in gameplay scripts.
* Route key material through a dedicated key provider.
* Validate failure paths (missing/invalid keys) in build environments.

---

## 5) Release checklist

Before shipping:

* [ ] Save/load smoke tests pass across scene transitions.
* [ ] Autosave interval and retention are tuned.
* [ ] Corrupt file behavior is user-safe and non-destructive.
* [ ] Upgrade/migration path tested between content versions.
* [ ] Generated snapshots committed and in sync.
