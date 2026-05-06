Memory-mapped file path added to PakArchiveV3Reader (v3.0 Phase 1-4).
- Implemented Open(string filePath) factory to mmap the entire pak and obtain a MemoryMappedViewAccessor for payload reads.
- ReadAllBytes now prefers mmap path when available, reading entry payloads via MemoryMappedViewAccessor.ReadArray.
- Kept existing Stream-based Read and ReadAllBytes as fallback to preserve backward compatibility.
- Ensured proper disposal of MemoryMappedFile, MemoryMappedViewAccessor, and underlying Stream to prevent leaks.
- Verification done via: dotnet build succeeded with 0 warnings, 0 errors.

Notes for future work:
- Consider integrating mmap-based reads with early manifest parsing or inline caching for performance.
- Add tests covering mmap path against a small sample PakArchiveV3 to validate offsets and sizes.
