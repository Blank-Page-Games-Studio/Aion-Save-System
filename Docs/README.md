# Aion Save System Documentation

This folder contains organized guides for setting up, extending, and operating the **Aion Save System** package.

## Documentation Map

1. **[01 - Getting Started](./01-Getting-Started.md)**
   * Installation and first save/load integration.
   * Minimum scene setup and component registration.

2. **[02 - Core Concepts](./02-Core-Concepts.md)**
   * Data model, save pipeline, and key runtime interfaces.
   * Save slots, profiles, checksums, and file format behavior.

3. **[03 - Configuration and Autosave](./03-Configuration-and-Autosave.md)**
   * `AionSaveSettings` guide.
   * Effective value normalization and autosave behavior.

4. **[04 - Snapshot Generation](./04-Snapshot-Generation.md)**
   * Reflection-free snapshots with `[GenerateSaveSnapshot]`.
   * Editor workflow and generated-code expectations.

5. **[05 - Troubleshooting and Best Practices](./05-Troubleshooting-and-Best-Practices.md)**
   * Common failure cases and practical fixes.
   * Recommended production patterns.

## Suggested Reading Order

If you are new to Aion:

1. Start with **01 - Getting Started**.
2. Read **02 - Core Concepts** to understand architecture decisions.
3. Configure project defaults with **03 - Configuration and Autosave**.
4. Add performance optimizations from **04 - Snapshot Generation**.
5. Keep **05 - Troubleshooting and Best Practices** as your operational checklist.

## Audience

* **Gameplay programmers:** Focus on saveable components, lifecycle hooks, and registration.
* **Tech designers / tools engineers:** Focus on settings, autosave, and editor tooling.
* **Technical leads:** Focus on architecture and extension points for custom serializer/storage/encryption pipelines.
