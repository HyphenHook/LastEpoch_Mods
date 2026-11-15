# Source artwork

The artwork exactly as it was supplied, kept so the images the mod uses can be made again. Nothing here is built into
the mod; `../Generated` holds the images that are. Never crop these: only shrink them, all by the same factor.

| File | Used for |
| --- | --- |
| `Ornate Glowing Encapsulation Orb.png` | the Encapsulation Orb (ground, inventory, Advanced Forge, tooltips) |
| `Ornate Violet Crystal Slot Frame.png` | the item slot of the Advanced Forge |
| `Ornate Violet Crystal Button Frame.png` | the Forge button |
| `orb_slot_empty.png`, `orb_slot_hover.png`, `orb_slot_filled.png` | the three states of an orb slot |
| `orb_shatter_vfx_package.zip` and `orb_shatter_vfx/` | the charge, shatter, swirl and absorb effect frames (8 each) |

Make the built images again with `scripts/Prepare-OrbUiArt.py` and `scripts/Build-OrbVfxAtlas.py`.
