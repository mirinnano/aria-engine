# Decisions
- Use multi-reader search order to respect pak priority.
- Implement per-category LRU caches with size-based eviction.
- Decompress using Lz4 for data/voice and Zstd for scenario/boot when Flags indicate compression.
