# Changelog
All notable changes to this project will be documented in this file.  
This project adheres to [Semantic Versioning](https://semver.org/).

## [0.7.0] - 2025-10-27
### Added
- **Phase 7 — Compile‑Time Snapshot Generation.**
  - `[GenerateSaveSnapshot]` attribute for `partial` `ISaveable` classes.
  - Editor code emitter producing `Assets/Aion.Generated/{Type}_Generated.cs` with `Snapshot` DTO and generated `CaptureSnapshot`/`RestoreSnapshot`.
  - `[SaveOrder]` attribute for deterministic member ordering.
  - **Aion Snapshot Generator** window: `Window/BPG Aion/Snapshot Generator`.
  - Runtime preference for generated snapshot methods; reflection fallback preserved.

### Changed
- `SaveManager` now checks a runtime cache to use generated snapshot methods when available.

### Fixed
- Minor stability improvements in code emission and domain‑reload behavior.

## [0.2.0] - 2025-10-25
### Added
- Compression layer (`GZipCompressor`).
- Encryption layer (`AesGcmEncryptor`).
- Profile-based directory structure.
- Autosave controller with rolling backups.
- Slot metadata and duration tracking.

### Changed
- `SaveHeader` extended with compression/encryption fields.
- `SaveOptions` and `LoadOptions` now accept compression/encryption flags.

### Fixed
- Improved deterministic JSON ordering.
- Safer atomic write and recovery path.

## [0.1.0] - 2025-10-24
### Added
- Initial public release of Aion Save System.
- JSON-based serialization and reflection via `[SaveField]`.
- SaveManager with checksum validation and atomic file writes.
- 3-slot demo UI.
- EditMode unit tests for snapshot and header integrity.