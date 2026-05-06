Decision: Extend PakArchiveV3Reader with memory-mapped file support for phase 1-4 (v3.0).
- Add Open(string filePath) factory to map the entire file using MemoryMappedFile and provide a ViewAccessor for payload reads.
- Add MemoryMappedFile and MemoryMappedViewAccessor fields to PakArchiveV3Reader; dispose pattern updated to release mmap resources.
- Maintain legacy Read(Stream) path and ReadAllBytes(string) fallback to preserve backward compatibility.
- Use ReadArray to extract payload data from mmap when available; fall back to stream-based reads otherwise.
- Build verification: dotnet build completed with 0 issues (0 warnings, 0 errors).
