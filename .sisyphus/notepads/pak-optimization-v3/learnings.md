Pak v3 Phase 3: initial scaffolding
- Added CLI flags to aria-pack: --format and --split, with default --format v2 for backward compatibility.
- Prepared integration point for PakArchiveV3-based packaging, including category-driven output scaffolding and placeholder for per-category compression logic.
- Build succeeds with the new flags present; v3 path currently acts as a scaffold and does not write v3 outputs yet (to avoid breaking existing v2 flow).

Next steps (manual plan):
- Implement DetermineCategoryFromPath logic to map file extensions and names to categories (Boot, Scenario, Data, Stream, Voice, Update).
- Implement per-category grouping and compute payloads with proper offsets and compression flags.
- Instantiate PakManifestV3 and PakManifestEntryV3 objects and call PakArchiveV3.Write for each category output file (boot.arib, scripts.aris, data.arid, arim, ariv, ariu).
- Ensure offsets reflect payload concatenation, set OriginalSize/Size correctly, and set Flags bit1 when compressed.
- Validate that the v3 outputs validate with PakArchiveV3Reader.PathHash64, and that read paths map correctly via PathHash.
- Extend tests or add a small unit to verify manifest length consistency and basic path hashing.
