Learnings from PakArchiveV3 implementation:
- Implemented PakManifestV3 and PakArchiveV3 with binary manifest layout including EntryTable (PathHash, Offset, Size, OriginalSize, Flags) and PathStringPool.
- Implemented 36-byte-structured header (ARIA, version, category, pakVersion, flags, entryCount, manifestOffset, manifestSize, payloadOffset, reserved).
- Writer writes header + manifest + payloads in a single pass; manifest is constructed with a sorted entry list by PathHash.
- Reader scaffold added: PakArchiveV3Reader with binary search FindEntry(path) using PathHash64 and a simple placeholder PathHash64 (Fnv-like).
- PathHash64 currently uses a deterministic FNV-1a 64-bit placeholder (to be replaced with xxHash64 in Phase 1-3).
- Build verified: dotnet build src/AriaEngine/AriaEngine.csproj -> success.
