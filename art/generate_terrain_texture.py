#!/usr/bin/env python3
"""
Generates a seamless woodcut-style ground tile: the same ink-mark philosophy as
generate_sprites.py (mark density carries tone), but every mark wraps around the tile
edges, and there is no diagonal light gradient - one repeated across many tiles reads as
wallpaper banding - so the ink density is uniform.

Run:  python3 generate_terrain_texture.py <output_path> [seed]
"""

import sys
import os
import numpy as np
from PIL import Image

S = 256  # matches generate_sprites.py's canvas (SCALE * 64)

BASE = np.array([0.32, 0.40, 0.18]) * 255           # meadow green-olive, near generate_sprites.py's grass
BASE_VARIANT = np.array([0.26, 0.33, 0.15]) * 255   # darker green, shadow between blades
HIGHLIGHT = np.array([0.52, 0.58, 0.28]) * 255       # lit blade tip
INK = np.array([0.14, 0.10, 0.08]) * 255            # same INK as generate_sprites.py

# No large macro shape (a dirt patch, a distinct blob) anywhere on the tile: the eye locks onto
# a silhouette repeating in a grid as soon as the mesh tiles this over any distance. Fine, dense
# marks read as uniform noise and the seams vanish.


def _blade_dash(rgb, cx, cy, rng, color, length_range):
    """A tiny hand-inked dash at one of several angles - the same discrete-mark idea as
    generate_sprites.py's hatch lines, but isotropic. Used for both dark shadow strokes and
    lit highlight strokes; dark strokes alone read as flat."""
    length = rng.integers(*length_range)
    direction = rng.choice([(1, -1), (1, 1), (1, 0), (0, 1)])
    dx_step, dy_step = direction
    for i in range(length):
        y, x = (cy + i * dy_step) % S, (cx + i * dx_step) % S
        rgb[y, x] = color


def _shade_blob(rgb, cx, cy, rng, color):
    """A 2-3 px cluster, not a single pixel: reads as shadow between blades rather than a
    speckle, and is still too small to recognise once tiled."""
    for _ in range(rng.integers(2, 4)):
        dy, dx = rng.integers(-1, 2), rng.integers(-1, 2)
        y, x = (cy + dy) % S, (cx + dx) % S
        rgb[y, x] = color


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else "ground.png"
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    rng = np.random.default_rng(seed)

    rgb = np.tile(BASE.clip(0, 255).astype(np.uint8), (S, S, 1))

    shade = BASE_VARIANT.clip(0, 255).astype(np.uint8)
    highlight = HIGHLIGHT.clip(0, 255).astype(np.uint8)

    # Darkest to lightest: shadow blobs, ink strokes, then highlight strokes. Dense enough that
    # no accidental shape near a tile edge repeats visibly (see the tiled preview written below).
    for _ in range(1400):
        cx, cy = int(rng.integers(0, S)), int(rng.integers(0, S))
        _shade_blob(rgb, cx, cy, rng, shade)

    for _ in range(2600):
        cx, cy = int(rng.integers(0, S)), int(rng.integers(0, S))
        _blade_dash(rgb, cx, cy, rng, INK.astype(np.uint8), (2, 5))

    for _ in range(1100):
        cx, cy = int(rng.integers(0, S)), int(rng.integers(0, S))
        _blade_dash(rgb, cx, cy, rng, highlight, (1, 3))

    image = Image.fromarray(rgb, "RGB")
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    image.save(out_path)
    print(f"wrote {out_path}")

    preview_path = os.path.join(os.path.dirname(out_path) or ".", "_ground_tiled_preview.png")
    tile_count = 6
    tiled = Image.new("RGB", (S * tile_count, S * tile_count))
    for row in range(tile_count):
        for col in range(tile_count):
            tiled.paste(image, (col * S, row * S))
    tiled.save(preview_path)
    print(f"wrote {preview_path}")


if __name__ == "__main__":
    main()
