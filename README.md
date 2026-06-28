# PicoStackables

A Vintage Story 1.22.3 server-side mod that multiplies item and block stack sizes, with per-item overrides and optional live-reload via [ConfigLib](https://mods.vintagestory.at/configlib).

## Features

- **Global multiplier** – scales every item/block stack size by a configurable factor (default ×2).
- **Per-item overrides** – set an absolute stack size for specific items; the global multiplier is ignored for them.
- **Per-block overrides** – same, but for placeable blocks.
- **ConfigLib integration** – if ConfigLib is present, values can be changed in-game and take effect immediately without a server restart.

## Building

```bash
dotnet build -c Release
```

The compiled `PicoStackables.dll` ends up in `bin/Release/`. Package it with `modinfo.json` into a zip for distribution.

### Enabling ConfigLib support

1. Download ConfigLib and extract `ConfigLib.dll` into the `lib/` folder.
2. Uncomment the `<Reference>` block in `PicoStackables.csproj`.
3. Rebuild.

## Config file

Generated on first run at `VintagestoryData/ModConfig/picostackables.json`:

```json
{
  "GlobalMultiplier": 2.0,
  "ItemOverrides": {
    "game:stick": 128,
    "game:stone-granite": 64
  },
  "BlockOverrides": {
    "game:log-oak-ud": 32
  }
}
```

| Field | Default | Description |
|---|---|---|
| `GlobalMultiplier` | `2.0` | Multiply all stack sizes by this factor. |
| `ItemOverrides` | `{}` | `"item-code": absoluteStackSize`. Bypasses the global multiplier. |
| `BlockOverrides` | `{}` | Same, but for blocks. |

Items/blocks listed in overrides are **excluded** from the global multiplier – only the explicit value applies.
