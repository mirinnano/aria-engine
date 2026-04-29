

## docs/how-to-guides/custom-fonts.md Created

**File created:** `docs/how-to-guides/custom-fonts.md`

**Coverage:**
- Supported formats: TTF only (OTF unsupported, confirmed from SpriteRenderer.cs using Raylib.LoadFontEx)
- Font file placement: recommends `assets/fonts/` directory
- init.aria font configuration: `font`, `font_atlas_size`, `font_filter` commands
- Atlas size: 128/256/512 recommendations based on script length and language
- Filter modes: bilinear (default), trilinear, anisotropic (fallback to trilinear), point
- Troubleshooting: missing glyphs, blurry text, load errors, missing specific characters

**Key findings from SpriteRenderer.cs applied:**
- Font codepoints are auto-collected from script lines + ASCII + Japanese punctuation + icons
- `SelectFontAtlasSize` clamps to 8-512 range
- `SelectSmoothFontAtlasSize` doubles atlas size for smooth filters (not bilinear/point)
- UI font is separate from main game font (`LoadUiFont`)
- Failed font loads fall back to default font with warning

**Language:** Japanese only
**File length:** 181 lines

## docs/how-to-guides/debug-mode.md Created (Task 19)

**File created:** `docs/how-to-guides/debug-mode.md`

**Coverage:**
- Enabling debug mode: `debug on` in init.aria, F3 toggle at runtime
- All 8 debug display items from SpriteRenderer.DrawDebugInfo:
  - FPS (Raylib.DrawFPS)
  - PC: ProgramCounter
  - Sprites: state.Sprites.Count
  - Draw Calls: TotalDrawCalls
  - Tex Loads: TotalTextureLoads
  - Color Cache: _colorCache.Count
  - Tex Cache: _textureCache.Count
  - Tex Stats: utilization percent
- Interpretation guide for each metric
- Common debugging workflows: invisible sprites, lag/stuttering, script not advancing, memory concerns

**Key finding from source code:**
- AGENTS.md mentions "Button hit areas (red outlines)" but SpriteRenderer.cs does NOT draw button hit area outlines in debug mode. Only FPS/PC/Sprites/DrawCalls/TexLoads/ColorCache/TexCache/TexStats are displayed.
- F3 toggle is handled in InputHandler.cs line 10; `debug` command is handled in CoreCommandHandler.cs line 56-57.

**Verification:**
- File is 85 lines, Japanese language only
- All content derived from actual source code (InputHandler.cs, SpriteRenderer.cs, CoreCommandHandler.cs, GameState.cs)
- No button hit area documentation included since feature is not implemented in renderer
