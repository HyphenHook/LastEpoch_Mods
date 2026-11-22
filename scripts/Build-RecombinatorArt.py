"""Prepares the Recombinator VFX kit for the mod.

    python scripts/Build-RecombinatorArt.py [the kit folder; default LastEpoch_Hud/Assets/Source/Recombinator_Unity_VFX_Kit]

Every image stays a single PNG in a folder of its own: one folder per component, the dormant base in it, and a folder for
each effect. The frames and the pipe bases are the kit's own and are not cut or resized. Three things are made here:

  * The kit's flow frames are one straight stream whatever the pipe, so the corner and the cross pieces get flow frames in
    which that same stream is bent along the pipe's own run, with flame running in the pipe (Pipe_*/VFX_Flow_Orange and _Green).
  * The two corner pipes are turned the way they are laid in the window: the left one a quarter turn to the left, the
    right one a quarter turn to the right.
  * ItemSlot_Left/VFX_Devour_Orange and ItemSlot_Right/VFX_Devour_Green: a flame that climbs the slot and swallows the
    item, burns, and sinks again, over the kit's own ember ring.

The result goes in LastEpoch_Hud/Assets/Generated/Recombinator, and is built into the mod.
"""
import glob
import os
import shutil
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(__file__)
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Source', 'Recombinator_Unity_VFX_Kit')
OUT = os.path.join(HERE, '..', 'LastEpoch_Hud', 'Assets', 'Generated', 'Recombinator')

# component folder -> degrees to turn it (counter-clockwise); the base and the kit's own effects are copied
COPIED = {
    'ItemSlot_Left': (0, ['Base']),
    'ItemSlot_Right': (0, ['Base']),
    'ItemSlot_Result': (0, None),
    'Pipe_LeftCorner': (90, ['Base']),
    'Pipe_RightCorner': (-90, ['Base']),
    'Pipe_MiddleCenter': (0, ['Base']),
}


def save(image, folder, name):
    os.makedirs(folder, exist_ok=True)
    image.save(os.path.join(folder, name), optimize=True)


def frames_of(folder):
    return [Image.open(p).convert('RGBA') for p in sorted(glob.glob(os.path.join(folder, '*.png')))]


# ---------------------------------------------------------------- the flow, bent along a pipe

STREAM_BOX = (68, 22, 118, 188)  # where the kit's stream is in its (184 x 202) frames
STREAM_HALF = 25.0


def polyline(*parts):
    """parts: ('line', (x0, y0), (x1, y1)) or ('curve', (x0, y0), (cx, cy), (x1, y1)); one dense run of points."""
    pts = []
    for part in parts:
        if part[0] == 'line':
            a, b = np.array(part[1], float), np.array(part[2], float)
            n = max(2, int(np.linalg.norm(b - a)))
            t = np.linspace(0, 1, n)[:, None]
            pts.append(a + (b - a) * t)
        else:
            a, c, b = (np.array(p, float) for p in part[1:])
            t = np.linspace(0, 1, 60)[:, None]
            pts.append((1 - t) ** 2 * a + 2 * (1 - t) * t * c + t ** 2 * b)
    pts = np.concatenate(pts)
    keep = [0]
    for i in range(1, len(pts)):
        if np.linalg.norm(pts[i] - pts[keep[-1]]) >= 0.5:
            keep.append(i)
    return pts[keep]


def bend_stream(stream_frames, path, size, colour):
    """The stream, laid along the path (its tail at the start, its head at the end), for each frame."""
    w, h = size
    seg = np.diff(path, axis=0)
    seglen = np.linalg.norm(seg, axis=1)
    arc = np.concatenate([[0], np.cumsum(seglen)])
    total = arc[-1]
    ys, xs = np.mgrid[0:h, 0:w]
    px = np.stack([xs.ravel() + 0.5, ys.ravel() + 0.5], axis=1)
    # nearest point of the path to every pixel
    best = np.full(len(px), 1e9)
    s = np.zeros(len(px))
    d = np.zeros(len(px))
    for i in range(len(seg)):
        a = path[i]
        v = seg[i]
        t = np.clip(((px - a) @ v) / (v @ v), 0, 1)
        proj = a + t[:, None] * v
        dist = np.linalg.norm(px - proj, axis=1)
        closer = dist < best
        if closer.any():
            sign = np.sign(v[0] * (px[:, 1] - a[1]) - v[1] * (px[:, 0] - a[0]))
            best[closer] = dist[closer]
            s[closer] = arc[i] + t[closer] * seglen[i]
            d[closer] = (dist * sign)[closer]
    inside = np.abs(d) < STREAM_HALF
    taper = np.clip(s / 10.0, 0, 1) * np.clip((total - s) / 10.0, 0, 1)
    sh = stream_frames[0].shape[0]
    sx = np.clip(STREAM_HALF + d, 0, stream_frames[0].shape[1] - 1.001)
    sy = np.clip(s / total * (sh - 1), 0, sh - 1.001)
    x0 = sx.astype(int)
    y0 = sy.astype(int)
    fx = (sx - x0)[:, None]
    fy = (sy - y0)[:, None]
    # the flame that runs in the pipe: tongues stretched along the run, which move one period over the frames
    n_frames = len(stream_frames)
    period = 240
    tongue_a = tile_noise(period, 48, 7, 5, 21)[period:2 * period]
    tongue_b = tile_noise(period, 48, 16, 9, 22)[period:2 * period]
    col = np.clip((d + 24).astype(int), 0, 47)
    profile = np.clip(1 - (np.abs(d) / 24.0) ** 1.6, 0, 1)
    ends = np.clip(s / 14.0, 0, 1) * np.clip((total - s) / 14.0, 0, 1)
    out = []
    for fi, f in enumerate(stream_frames):
        row = (s.astype(int) - fi * (period // n_frames)) % period
        n = 0.62 * tongue_a[row, col] + 0.38 * tongue_b[row, col]
        heat = np.clip(profile * 0.95 + (n - 0.5) * 1.2, 0, 1) * (np.abs(d) < 24) * ends
        hue = np.clip(heat * (0.45 + 0.55 * n), 0, 1)
        flame_rgb = ramp(hue, RAMPS[colour])
        flame_a = np.clip(heat * 1.25, 0, 1) * 0.7
        c = (f[y0, x0] * (1 - fx) * (1 - fy) + f[y0, x0 + 1] * fx * (1 - fy)
             + f[y0 + 1, x0] * (1 - fx) * fy + f[y0 + 1, x0 + 1] * fx * fy)
        c[:, 3] = c[:, 3] * inside * taper
        c[:, :3] = np.where(c[:, 3:4] > 0, c[:, :3], 0)
        # the stream over the flame
        a_dots = c[:, 3:4] / 255.0
        a_fl = flame_a.reshape(-1, 1)
        rgb = (c[:, :3] * a_dots + flame_rgb.reshape(-1, 3) * 255 * a_fl * (1 - a_dots))
        alpha = a_dots + a_fl * (1 - a_dots)
        rgb = np.where(alpha > 0, rgb / np.maximum(alpha, 1e-6), 0)
        c = np.concatenate([rgb, alpha * 255], axis=1)
        out.append(Image.fromarray(np.clip(c, 0, 255).astype(np.uint8).reshape(h, w, 4), 'RGBA'))
    return out


# the run of each piece, in the piece's own image (after it is turned)
LEFT_CORNER = (193, 168, polyline(('line', (42.5, 10), (42.5, 62)), ('curve', (42.5, 62), (42.5, 116), (101, 116)), ('line', (101, 116), (181, 116))))
RIGHT_CORNER = (192, 169, polyline(('line', (144.5, 10), (144.5, 63)), ('curve', (144.5, 63), (144.5, 117), (94, 117)), ('line', (94, 117), (13, 117))))
CROSS_ORANGE = (185, 211, polyline(('line', (12, 96), (66, 96)), ('curve', (66, 96), (92.5, 96), (92.5, 122)), ('line', (92.5, 122), (92.5, 168))))
CROSS_GREEN = (185, 211, polyline(('line', (173, 96), (119, 96)), ('curve', (119, 96), (92.5, 96), (92.5, 122)), ('line', (92.5, 122), (92.5, 168))))

# ---------------------------------------------------------------- the flame that swallows an item

DEVOUR_FRAMES = 32
DEVOUR_FPS = 32.0
# the opening of each frame the item box sits in: x0, x1, y0, y1 (in the frame's own pixels)
HOLE = {'ItemSlot_Left': (128, 275, 117, 354), 'ItemSlot_Right': (71, 218, 117, 354)}
RAMPS = {
    'Orange': [(0.0, (0.30, 0.02, 0.0)), (0.35, (0.85, 0.17, 0.02)), (0.68, (1.0, 0.52, 0.06)), (1.0, (1.0, 0.93, 0.58))],
    'Green': [(0.0, (0.02, 0.22, 0.02)), (0.35, (0.10, 0.62, 0.07)), (0.68, (0.42, 0.95, 0.20)), (1.0, (0.86, 1.0, 0.70))],
}


def noise(h, w, cells_y, cells_x, rng):
    grid = rng.random((cells_y, cells_x)).astype(np.float32)
    return np.asarray(Image.fromarray(grid, 'F').resize((w, h), Image.BICUBIC))


def tile_noise(h, w, cells_y, cells_x, seed):
    """Noise that repeats down the picture, so it can scroll without a seam."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells_y, cells_x)).astype(np.float32)
    big = np.tile(grid, (3, 1))
    img = Image.fromarray(big, 'F').resize((w, h * 3), Image.BICUBIC)
    return np.asarray(img)


def ramp(heat, stops):
    out = np.zeros(heat.shape + (3,), np.float32)
    for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
        m = (heat >= p0) & (heat <= p1)
        t = ((heat - p0) / (p1 - p0))[m][:, None]
        out[m] = np.array(c0) * (1 - t) + np.array(c1) * t
    return out


def devour(component, colour, embers):
    x0, x1, y0, y1 = HOLE[component]
    w, h = embers[0].size
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (xs - x0) / (x1 - x0)
    v = (y1 - ys) / (y1 - y0)  # 0 at the bottom of the opening, 1 at the top
    # the flame is a tall noise that scrolls upward: three sizes of it
    body = [tile_noise(h, w, 5, 13, 11)[h:2 * h], tile_noise(h, w, 9, 24, 12)[h:2 * h], tile_noise(h, w, 16, 40, 13)[h:2 * h]]
    stops = RAMPS[colour]
    frames = []
    for f in range(DEVOUR_FRAMES):
        t = f / DEVOUR_FPS
        # the front of the flame: climbs over the item, stays, then sinks as the item is eaten
        rise = min(1.0, t / 0.42)
        front = 1.75 * (1 - (1 - rise) ** 2)
        if t > 0.60:
            k = min(1.0, (t - 0.60) / 0.40)
            front = 1.75 - 2.2 * k * k
        shift = int(t * 240) % h
        n = 0.0
        for i, (layer, weight) in enumerate(zip(body, (0.5, 0.3, 0.2))):
            n = n + weight * np.roll(layer, int(shift * (1 + 0.5 * i)), axis=0)
        heat = np.clip((front - v) * 1.7 + (n - 0.5) * 1.5, 0, 1)
        mask = (np.clip((u + 0.14) / 0.14, 0, 1) * np.clip((1.14 - u) / 0.14, 0, 1)
                * np.clip((v + 0.06) / 0.10, 0, 1) * np.clip((1.22 - v) / 0.22, 0, 1))
        heat = heat * mask
        alpha = np.clip(heat * 1.3, 0, 1) * 0.82
        # inside the flame the colour moves with the noise, so it burns instead of glowing flat
        hue = np.clip(heat * (0.30 + 0.62 * np.roll(body[1], shift * 2, axis=0)), 0, 1)
        rgb = ramp(hue, stops)
        fire = np.concatenate([rgb * 255, (alpha * 255)[..., None]], axis=2).astype(np.uint8)
        frame = embers[f % len(embers)].copy()
        # the embers fade as the fire takes over
        frame.putalpha(frame.getchannel('A').point(lambda a: int(a * 0.7)))
        frame.alpha_composite(Image.fromarray(fire, 'RGBA'))
        frames.append(frame)
    return frames


# ---------------------------------------------------------------- build

def main():
    if os.path.isdir(OUT):
        for child in os.listdir(OUT):  # the folder itself may be open in a shell; only what is in it is cleared
            shutil.rmtree(os.path.join(OUT, child))
    count = 0
    for component, (turn, only) in COPIED.items():
        base = os.path.join(SRC, component)
        for folder, _, files in os.walk(base):
            relative = os.path.relpath(folder, SRC)
            top = relative.split(os.sep)[1] if os.sep in relative else ''
            if only is not None and top not in only:
                continue
            for name in files:
                if not name.lower().endswith('.png'):
                    continue
                image = Image.open(os.path.join(folder, name)).convert('RGBA')
                if turn:
                    image = image.rotate(turn, expand=True)
                save(image, os.path.join(OUT, relative), name)
                count += 1

    streams = {c: [np.asarray(f.crop(STREAM_BOX), np.float32) for f in frames_of(os.path.join(SRC, 'Pipe_Entry', 'VFX_Flow_' + c))] for c in ('Orange', 'Green')}
    pieces = [
        ('Pipe_LeftCorner', 'Orange', LEFT_CORNER), ('Pipe_RightCorner', 'Green', RIGHT_CORNER),
        ('Pipe_MiddleCenter', 'Orange', CROSS_ORANGE), ('Pipe_MiddleCenter', 'Green', CROSS_GREEN),
    ]
    for component, colour, (w, h, path) in pieces:
        for i, frame in enumerate(bend_stream(streams[colour], path, (w, h), colour)):
            save(frame, os.path.join(OUT, component, 'VFX_Flow_' + colour), 'frame_%03d.png' % i)
            count += 1

    for component, colour in (('ItemSlot_Left', 'Orange'), ('ItemSlot_Right', 'Green')):
        embers = frames_of(os.path.join(SRC, component, 'VFX_PowerUp'))
        for i, frame in enumerate(devour(component, colour, embers)):
            save(frame, os.path.join(OUT, component, 'VFX_Devour_' + colour), 'frame_%03d.png' % i)
            count += 1
    print('done', count, 'images')


main()
