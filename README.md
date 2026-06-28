# PicoStackables

A Vintage Story 1.22.3 server-side mod that multiplies item and block stack sizes, with a rich in-game config dialog and optional [ConfigLib](https://mods.vintagestory.at/configlib) integration.

## Features

- **Global multiplier** – scales every item/block stack size by a configurable factor (default ×2).
- **Per-item/block overrides** – set an absolute stack size for specific items or blocks; the global multiplier is bypassed for them.
- **In-game config dialog** – `/picostackables` opens a searchable list of all items and blocks showing:
  - Item icon
  - Display name
  - `16 → 32` preview (new value in green, updates live as you adjust the multiplier)
  - Override values shown in yellow
- **Live multiplier preview** – changing the multiplier input instantly updates all visible stack previews without saving or reloading.
- **Right-click an item** in the dialog to toggle a per-item override.
- **ConfigLib integration** – if ConfigLib is installed, the config is also accessible from its config panel for server operators who prefer that workflow.

## Dialog

```
┌─ PicoStackables – Stack Sizes ──────────────────────────[×]─┐
│  Stack Multiplier:  [2.0      ]                             │
│  Search:            [________________________]              │
├─────────────────────────────────────────────────────────    │
│  [🪵] Log Oak                               [scrollbar]    │
│       4 → 8                                                 │
│  [🪨] Stone Granite                                         │
│       64 → 128                                              │
│  [🌿] Stick                                                 │
│       128  (override, shown in yellow)                      │
│  ...                                                        │
├─────────────────────────────────────────────────────────    │
│                            [  Save  ]  [  Close  ]          │
└─────────────────────────────────────────────────────────────┘
```

Right-clicking an item sets a per-item override equal to the current computed value, letting you fine-tune it. Right-clicking again clears the override.

## Building

```bash
dotnet build -c Release
```

Package `bin/Release/PicoStackables.dll` together with `modinfo.json` into a `.zip` for distribution.

### Enabling ConfigLib support

1. Download ConfigLib and extract `ConfigLib.dll` into `lib/`.
2. Uncomment the `<Reference>` block in `PicoStackables.csproj`.
3. Rebuild.

## Config file

`VintagestoryData/ModConfig/picostackables.json` (auto-generated on first run):

```json
{
  "GlobalMultiplier": 2.0,
  "ItemOverrides": {
    "game:stick": 128
  },
  "BlockOverrides": {}
}
```

| Field | Default | Description |
|---|---|---|
| `GlobalMultiplier` | `2.0` | Multiply all stack sizes by this factor. |
| `ItemOverrides` | `{}` | `"item-code": absoluteStackSize` — bypasses the global multiplier. |
| `BlockOverrides` | `{}` | Same, but for blocks. |

## Permissions

Only players with the `controlserver` privilege (server operators) can save config changes through the dialog or network packet. The dialog itself is readable by anyone.
