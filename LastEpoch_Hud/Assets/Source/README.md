# Source artwork

The artwork exactly as it was supplied, kept so the images the mod uses can be made again. Nothing here is built into
the mod; `../Generated` holds the images that are. Never crop these: only shrink them, all by the same factor.

| File | Used for |
| --- | --- |
| `Ornate Glowing Encapsulation Orb.png` | the Encapsulation Orb (ground, inventory, Advanced Forge, tooltips) |
| `Ornate Violet Crystal Slot Frame.png` | the item slot of the Advanced Forge |
| `Ornate Violet Crystal Button Frame.png` | the Forge button |
| `orb_slot_empty.png`, `orb_slot_hover.png`, `orb_slot_filled.png` | the three states of an orb slot |
| `essence_shatter_vfx/` | the Essence Shatter VFX of the Advanced Forge: 40 frames of 512 by 768, its README and metadata (the orb at 43.6% down, the item's absorb point at 65.8% down) |

Make the built images again with `scripts/Prepare-OrbUiArt.py` and `scripts/Build-EssenceVfxAtlas.py`.
