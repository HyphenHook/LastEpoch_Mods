"""Packs the supplied orb shatter frames into one atlas for the Advanced Forge.

    python scripts/Build-OrbVfxAtlas.py <folder with charge, shatter, swirl and absorb sub-folders>

Eight columns, one row per phase: charge, shatter, swirl, absorb. 256 by 256 each.
"""
import os
import sys
from PIL import Image

SRC = sys.argv[1]
OUT = os.path.join(os.path.dirname(__file__), '..', 'LastEpoch_Hud', 'Assets', 'Generated')
os.makedirs(OUT, exist_ok=True)

phases = ['charge', 'shatter', 'swirl', 'absorb']
atlas = Image.new('RGBA', (8 * 256, len(phases) * 256), (0, 0, 0, 0))
for row, name in enumerate(phases):
    for col in range(8):
        frame = Image.open(os.path.join(SRC, name, '%s_%02d.png' % (name, col + 1))).convert('RGBA')
        assert frame.size == (256, 256), frame.size
        atlas.paste(frame, (col * 256, row * 256))
atlas.save(os.path.join(OUT, 'orb_vfx_atlas.png'), optimize=True)
print('done')
