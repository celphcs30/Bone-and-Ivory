# Bone and Ivory

RimWorld mod adding ivory crafting and bone/skull decorative items.

## Original Mod

Updated continuation of **"SWR's Skulls and Ivory"** by Homeless Emperor (She Wants Revenge).

**Original**: https://steamcommunity.com/sharedfiles/filedetails/?id=3169742181

Abandoned after 1.4, updated for 1.6.

## Features

- **Ivory resource**: Craft from elephant tusks (8 ivory) or thrumbo horns (80 ivory) at stonecutter
- **Wild boar butchering**: Yields 2 ivory
- **Buildings**:
  - Bone wall (500 HP, paintable, drag placement)
  - Long skullspike torch (perpetual flame, 4 skulls)
  - Double skullspike (decorative, 2 skulls)
- **Floors** (all paintable):
  - Skull floor
  - Skull fine floor
  - Skull pathway
  - Ivory tile
- **Ideology integration**: All items contribute to Morbid style dominance
- **Vanilla enhancement**: Makes Skullspike rotatable

### Mod Settings

Customize crafting costs and materials via Mod Settings:

- **Walls**: Toggle between skulls or stone blocks
  - Adjustable cost (1-20) for both materials
  - Stone block mode supports material selection menu (like vanilla walls)
- **Floors**: Toggle between skulls or stone blocks
  - Separate cost controls (1-20) for:
    - Bone Floor
    - Skull Fine Floor
    - Skull Pathway
  - Stone block floors support material selection menu (like vanilla stone tiles)
  - All stone types available (Sandstone, Granite, Limestone, Slate, Marble)
- **Skull Spikes**: Unaffected by settings (always use skulls + stuff)
- **Note**: Floor changes require exiting to main menu or restarting the game to take effect

## Compatibility

- RimWorld 1.6, 1.5
- Ideology DLC recommended (for style features)
- Combat Extended compatible (500 HP walls)

## Installation

Drop `CelphIvoryAndSkulls` folder into RimWorld `Mods` directory.

## Changelog

### 1.6 Update (celphcs30)
- Updated for RimWorld 1.6
- Fixed XML syntax errors
- Added Morbid style dominance to all items
- Made all walls/floors paintable
- Bone wall: drag placement, 500 HP, Beauty 4
- Fixed deprecated properties
- Updated packageId to `celphcs30.BoneAndIvory`

### Mod Settings Update
- Added mod settings to toggle between skulls and stone blocks for walls and floors
- Adjustable costs (1-20) for all materials via sliders
- Separate cost controls for walls, bone floor, fine floor, and pathway
- Stone block floors support material selection menu (like vanilla stone tiles)
- All stone types available (Sandstone, Granite, Limestone, Slate, Marble)
- Skull spikes remain unchanged (always use skulls + stuff)

## Technical

- **Package ID**: `celphcs30.BoneAndIvory`
- **Type**: XML + C# mod (Harmony patches for dynamic cost/material switching)
- **License**: CC0-1.0 (Public Domain)

## Credits

- Original: Homeless Emperor (She Wants Revenge)
- Updated: celphcs30
