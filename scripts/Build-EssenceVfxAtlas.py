"""Builds the Advanced Forge effect atlas from the supplied Essence Shatter VFX, and a ring for the shatter.

    python scripts/Build-EssenceVfxAtlas.py [the supplied atlas; default LastEpoch_Hud/Assets/Source/essence_shatter_vfx/ForgeEssence_Atlas_8x5.png]

The supplied atlas is 8 by 5 frames of 512 by 768. It is shrunk to half size as a whole (nothing is cut), which is
plenty for the size it is shown at, and keeps the texture small. Frames 1 to 8 charge, 9 to 16 shatter, 17 to 28 absorb
and 29 to 40 are the residual wisps.
"""
import os
import sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(__file__)
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Source', 'essence_shatter_vfx', 'ForgeEssence_Atlas_8x5.png')
OUT = os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Generated')
os.makedirs(OUT, exist_ok=True)

atlas = Image.open(SRC).convert('RGBA')
assert atlas.size == (8 * 512, 5 * 768), atlas.size
atlas.resize((atlas.width // 2, atlas.height // 2), Image.LANCZOS).save(os.path.join(OUT, 'essence_vfx_atlas.png'), optimize=True)

# a thin violet ring with a soft glow, which spreads out from the orb as it breaks
size = 256
yy, xx = np.mgrid[0:size, 0:size].astype(float)
d = np.hypot(xx - (size - 1) / 2, yy - (size - 1) / 2) / (size / 2)
core = np.exp(-((d - 0.82) / 0.025) ** 2)
halo = np.exp(-((d - 0.82) / 0.09) ** 2) * 0.45
a = np.clip(core + halo, 0, 1) * (d < 1.0)
ring = np.zeros((size, size, 4))
ring[..., 0] = 0.78 + 0.22 * core
ring[..., 1] = 0.55 + 0.40 * core
ring[..., 2] = 1.0
ring[..., 3] = a
Image.fromarray((np.clip(ring, 0, 1) * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'ring_violet.png'))
print('done')
