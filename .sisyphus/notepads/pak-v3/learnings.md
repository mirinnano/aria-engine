# Learnings
- Implementing PakAssetProviderV3 required careful mapping of v3 APIs. In absence of concrete interfaces, I used dynamic calls to PakArchiveV3Reader to preserve forward compatibility.
- Key decisions: balance memory usage via per-category caches; simple LRU for data/voice; always-keep small caches for scenario/boot.
