# Background Time Filter

Background time filters let a script reuse one daytime background image as evening, night, or midnight by applying an engine-side tone filter at render time.

## Commands

```aria
bgtime 2
bg "assets/bg/room.png"

bg "assets/bg/room.png", 3
bgfade "assets/bg/room.png", 800, 4, "midnight_room"
```

Time values:

- `0` or `1`: no filter / day
- `2`: evening, default preset `evening_cinematic`
- `3`: night, default preset `night_moon`
- `4`: midnight, default preset `midnight_room`

## Compatibility Map

```aria
bgtime_map "bg0003b", 2
bg "bg0003b"
```

If `bg0003b` is mapped and no matching file exists, the renderer also tries suffix fallback such as `bg0003.png`, `bg0003.bmp`, and `assets/bg/bg0003.png`.

## Scope

- The filter is applied only to background sprite `0`.
- Character sprites, UI, text, and buttons are not modified.
- The original image file is never changed.
- Filtered textures are cached by image path, time value, and preset.
