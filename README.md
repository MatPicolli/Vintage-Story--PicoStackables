# PicoStackables

A Vintage Story 1.22.3 mod that multiplies item and block stack sizes, with a rich in-game config dialog.

## Features

- **Global multiplier** – scales every item/block stack size by a configurable factor (default ×2).
- **Flat size mode** – instead of multiplying, set *every* item/block to one fixed stack size (e.g. 500 or 1000). Toggle it in the dialog or via `UseFlatSize` in the config.
- **Prevent item loss** – a safety toggle (on by default) that stops the mod from ever setting a stack size *below* the vanilla value, so lowering the multiplier or a small flat size can't truncate and destroy existing oversized stacks.
- **Per-item/block overrides** – set an absolute stack size for specific items or blocks; the global multiplier / flat size is bypassed for them.
- **In-game config dialog** – `/picostackables` opens a searchable list of all items and blocks showing:
  - Item icon
  - Display name
  - `16 → 32` preview (new value in green, updates live as you adjust the settings)
  - Override values shown in yellow
- **Live preview** – changing the multiplier, flat size or toggles instantly updates all visible stack previews without saving or reloading.
- **Unsaved-changes indicator** – the dialog shows whether your current settings are applied, so a preview is never mistaken for a saved change.
- **Right-click an item** in the dialog to toggle a per-item override.
- **Multiplayer-aware** – stack sizes apply on both server and connected clients. Only operators with the `controlserver` privilege can save changes.
- **Smart filtering** – tools/durability items, creature & NPC spawn entries, and internal placeholders (like `item-air`) are left untouched and hidden from the list.

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

Package `bin/Release/PicoStackables.dll` together with `modinfo.json` into a `.zip` for distribution. The zip must contain only those two files — do not include any `.deps.json`.

## Config file

`VintagestoryData/ModConfig/picostackables.json` (auto-generated on first run):

```json
{
  "GlobalMultiplier": 2.0,
  "UseFlatSize": false,
  "FlatStackSize": 100,
  "PreventShrinking": true,
  "ItemOverrides": {
    "game:stick": 128
  },
  "BlockOverrides": {}
}
```

| Field | Default | Description |
|---|---|---|
| `GlobalMultiplier` | `2.0` | Multiply all stack sizes by this factor (ignored when `UseFlatSize` is true). |
| `UseFlatSize` | `false` | When true, set every managed item/block to `FlatStackSize` instead of multiplying. |
| `FlatStackSize` | `100` | The absolute stack size used when `UseFlatSize` is true. |
| `PreventShrinking` | `true` | Never set a stack size below its vanilla value, preventing item loss from truncated stacks. Overrides bypass this. |
| `ItemOverrides` | `{}` | `"item-code": absoluteStackSize` — bypasses the multiplier/flat size. |
| `BlockOverrides` | `{}` | Same, but for blocks. |

## Permissions

Only players with the `controlserver` privilege (server operators) can save config changes through the dialog or network packet. The dialog itself is readable by anyone.
