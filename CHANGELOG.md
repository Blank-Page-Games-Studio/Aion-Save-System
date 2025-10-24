# Changelog
All notable changes to this project will be documented in this file.  
This project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2025-10-24
### Added
- Initial public release of Aion Save System.
- JSON-based serialization and reflection via `[SaveField]`.
- SaveManager with checksum validation and atomic file writes.
- 3-slot demo UI.
- EditMode unit tests for snapshot and header integrity.

## [1.1.0] - 2025-11-02
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
