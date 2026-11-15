"""Prepares the Recombinator VFX kit for the mod.

    python scripts/Build-RecombinatorArt.py [the kit folder; default LastEpoch_Hud/Assets/Source/Recombinator_Unity_VFX_Kit]

Every image stays a single PNG in a folder of its own: one folder per component (the three slot frames and the pipe
pieces that are used), the dormant base in it, and a folder for each effect. Nothing is cut and nothing is resized.
The two corner pipes are turned to the way they are laid in the window: the left one a quarter turn to the left, the
right one a quarter turn to the right (the frames of their effects are turned the same way). The result goes in
LastEpoch_Hud/Assets/Generated/Recombinator, and is built into the mod.
"""
import os
import shutil
import sys
from PIL import Image

HERE = os.path.dirname(__file__)
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Source', 'Recombinator_Unity_VFX_Kit')
OUT = os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Generated', 'Recombinator')

# component folder -> degrees to turn it (counter-clockwise), for those that are laid turned
USED = {
    'ItemSlot_Left': 0,
    'ItemSlot_Right': 0,
    'ItemSlot_Result': 0,
    'Pipe_LeftCorner': 90,
    'Pipe_RightCorner': -90,
    'Pipe_MiddleCenter': 0,
}

if os.path.isdir(OUT):
    shutil.rmtree(OUT)
count = 0
for component, turn in USED.items():
    for folder, _, files in os.walk(os.path.join(SRC, component)):
        for name in files:
            if not name.lower().endswith('.png'):
                continue
            relative = os.path.relpath(folder, SRC)
            target = os.path.join(OUT, relative)
            os.makedirs(target, exist_ok=True)
            image = Image.open(os.path.join(folder, name)).convert('RGBA')
            if turn:
                image = image.rotate(turn, expand=True)
            image.save(os.path.join(target, name), optimize=True)
            count += 1
print('done', count, 'images')
