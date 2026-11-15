"""Prepares the supplied frame artwork for the Advanced Forge.

    python scripts/Prepare-OrbUiArt.py [folder with the supplied PNGs]

Crops each image to what is drawn, squares the three orb slot states to the same size, shrinks everything to what the
game needs, and makes a grey copy of the orb and a purple glow. The results land in LastEpoch_Hud/Assets/Generated.
"""
import os
import sys
import numpy as np
from PIL import Image

SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser('~/Downloads')
OUT = os.path.join(os.path.dirname(__file__), '..', 'LastEpoch_Hud', 'Assets', 'Generated')
os.makedirs(OUT, exist_ok=True)


def load(name):
    return Image.open(os.path.join(SRC, name)).convert('RGBA')


def crop(im):
    return im.crop(im.getchannel('A').getbbox())


def square(im, side):
    s = max(im.size)
    canvas = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    canvas.paste(im, ((s - im.width) // 2, (s - im.height) // 2))
    return canvas.resize((side, side), Image.LANCZOS)


# the three states of an orb slot, cropped to what is drawn and squared to one size
for state in ('empty', 'hover', 'filled'):
    square(crop(load('orb_slot_%s.png' % state)), 256).save(os.path.join(OUT, 'orb_slot_%s.png' % state))

# the slot frame and the button frame, kept in their own proportions
frame = crop(load('Ornate Violet Crystal Slot Frame.png'))
frame = frame.resize((int(frame.width * 560 / frame.height), 560), Image.LANCZOS)
frame.save(os.path.join(OUT, 'forge_slot_frame.png'))
button = crop(load('Ornate Violet Crystal Button Frame.png'))
button = button.resize((600, int(button.height * 600 / button.width)), Image.LANCZOS)
button.save(os.path.join(OUT, 'forge_button_frame.png'))

# a grey orb for when there are none, and a soft purple glow for the selected one
orb = Image.open(os.path.join(OUT, 'orb_encapsulation.png')).convert('RGBA')
arr = np.array(orb).astype(float)
grey = arr[..., :3].mean(axis=2, keepdims=True) * 0.55
arr[..., :3] = grey
Image.fromarray(arr.astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'orb_encapsulation_grey.png'))

size = 128
yy, xx = np.mgrid[0:size, 0:size].astype(float)
d = np.hypot(xx - (size - 1) / 2, yy - (size - 1) / 2) / (size / 2)
a = np.clip(1 - d, 0, 1) ** 1.8
glow = np.zeros((size, size, 4))
glow[..., 0] = 0.62
glow[..., 1] = 0.32
glow[..., 2] = 1.0
glow[..., 3] = a
Image.fromarray((glow * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'glow_violet.png'))
print('done')
