"""Draws the crafting orb and the fracture diamond as PNGs.

    python scripts/Generate-OrbArt.py

The images land in LastEpoch_Hud/Assets/Generated and are built into the mod as resources.
"""
import os
import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), '..', 'LastEpoch_Hud', 'Assets', 'Generated')
os.makedirs(OUT, exist_ok=True)


def value_noise(shape, scale, seed):
    rng = np.random.default_rng(seed)
    h, w = shape
    gh, gw = int(h / scale) + 3, int(w / scale) + 3
    grid = rng.random((gh, gw))
    ys = np.linspace(0, gh - 3, h)
    xs = np.linspace(0, gw - 3, w)
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    fy = (ys - y0)[:, None]
    fx = (xs - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)
    a = grid[y0][:, x0]
    b = grid[y0][:, x0 + 1]
    c = grid[y0 + 1][:, x0]
    d = grid[y0 + 1][:, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def fbm(shape, base, seed, octaves=4):
    total = np.zeros(shape)
    amp = 1.0
    norm = 0.0
    for o in range(octaves):
        total += amp * value_noise(shape, base / (2 ** o), seed + o * 17)
        norm += amp
        amp *= 0.5
    return total / norm


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def orb(size, core, glow, seed):
    c = (size - 1) / 2
    yy, xx = np.mgrid[0:size, 0:size].astype(float)
    radius = size * 0.40
    dx = (xx - c) / radius
    dy = (yy - c) / radius
    d2 = dx * dx + dy * dy
    d = np.sqrt(d2)
    inside = d <= 1.0
    z = np.sqrt(np.clip(1 - d2, 0, 1))
    nx, ny, nz = dx, -dy, z
    light = np.array([-0.45, 0.62, 0.64])
    light /= np.linalg.norm(light)
    diffuse = np.clip(nx * light[0] + ny * light[1] + nz * light[2], 0, 1)

    # mist swirling in the glass: noise bent by more noise
    warp_x = fbm((size, size), size / 5, seed, 3) - 0.5
    warp_y = fbm((size, size), size / 5, seed + 50, 3) - 0.5
    n = fbm((size, size), size / 7, seed + 100, 5)
    n2 = fbm((size, size), size / 4, seed + 200, 4)
    swirl = np.sin((n * 6.0 + warp_x * 5.0 + dx * 2.5 + dy * 1.5) * 1.6) * 0.5 + 0.5
    mist = np.clip(0.75 * swirl * (0.4 + n2) + 0.5 * n - 0.22, 0, 1)
    mist *= (0.45 + 0.55 * z)

    core = np.array(core, dtype=float)
    glow = np.array(glow, dtype=float)
    shade = np.clip(diffuse * 0.75 + mist * 0.5, 0, 1)[..., None]
    body = core * 0.20 * (1 - shade) + core * shade
    body = body + glow * (mist[..., None] * 0.42 * (1 - d[..., None] * 0.6))

    # light caught inside the glass on the dark side, a thin bright rim on the lit side
    under = (np.clip(-ny * 0.9 + 0.25, 0, 1) ** 3) * (1 - z) * 0.65
    body += glow * under[..., None]
    rim_lit = smooth(0.78, 0.97, d) * np.clip(nx * light[0] + ny * light[1] + 0.35, 0, 1)
    body += glow * rim_lit[..., None] * 0.55

    # dark worn edge
    edge = d ** 3.4
    body = body * (1 - edge[..., None] * 0.80)

    # hard glints
    g1 = np.hypot(dx + 0.36, dy + 0.40)
    glint = np.clip(1 - g1 / 0.19, 0, 1) ** 3
    g1b = np.hypot((dx + 0.30) / 1.5, (dy + 0.55) / 0.8)
    streak = np.clip(1 - g1b / 0.30, 0, 1) ** 2 * 0.55
    g2 = np.hypot(dx - 0.42, dy + 0.46)
    glint2 = np.clip(1 - g2 / 0.10, 0, 1) ** 3 * 0.55
    body += np.array([1, 1, 1]) * (glint + streak + glint2)[..., None]

    # thin dark outline so it reads on any background
    ring = smooth(0.93, 1.0, d)
    body = body * (1 - ring[..., None]) + np.array([0.012, 0.014, 0.02]) * ring[..., None]

    rgb = np.clip(body, 0, 1)
    alpha = np.where(inside, 1 - smooth(0.985, 1.0, d), 0.0)

    # soft halo round it
    halo = np.clip(1 - (d - 1.0) / 0.34, 0, 1)
    halo = np.where(d > 1.0, halo ** 2.2 * 0.5, 0.0)
    out_rgb = np.where(inside[..., None], rgb, glow * 0.7)
    out_a = np.where(inside, alpha, halo)
    rgba = np.dstack([out_rgb, out_a])
    return Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA')


def diamond(size, base, bright):
    c = (size - 1) / 2
    yy, xx = np.mgrid[0:size, 0:size].astype(float)
    dx = (xx - c) / (size * 0.30)
    dy = (yy - c) / (size * 0.40)
    m = np.abs(dx) + np.abs(dy)  # the diamond is m <= 1
    inside = m <= 1.0
    base = np.array(base, dtype=float)
    bright = np.array(bright, dtype=float)
    # four facets: lit upper left, dark lower right
    facet = np.where((dx < 0) & (dy < 0), 1.0, np.where((dx >= 0) & (dy < 0), 0.62, np.where((dx < 0) & (dy >= 0), 0.5, 0.28)))
    body = base[None, None, :] * facet[..., None] + bright * (np.clip(1 - m, 0, 1) ** 2)[..., None] * 0.7
    # bright edge and a hard white glint on the upper left facet
    edge = smooth(0.80, 1.0, m)
    body = body * (1 - edge[..., None] * 0.5) + bright * edge[..., None] * 0.9
    g = np.hypot(dx + 0.35, dy + 0.35)
    body += (np.clip(1 - g / 0.28, 0, 1) ** 2 * 0.9)[..., None]
    alpha = np.where(inside, 1.0, 0.0)
    # soft glow
    glow_a = np.clip(1 - (m - 1.0) / 0.9, 0, 1) ** 2 * 0.55
    out_rgb = np.where(inside[..., None], np.clip(body, 0, 1), bright * 0.8)
    out_a = np.where(inside, alpha, glow_a)
    rgba = np.dstack([out_rgb, out_a])
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA')
    return img


# orb_encapsulation.png is supplied artwork, not drawn here
diamond(64, (0.25, 0.60, 0.95), (0.65, 0.92, 1.0)).save(os.path.join(OUT, 'diamond_fractured.png'))
print('done', os.path.abspath(OUT))
