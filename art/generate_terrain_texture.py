#!/usr/bin/env python3
"""
Generates a seamless, tileable woodcut-style ground texture - same hand-inked
crosshatch philosophy as generate_sprites.py (line density/mark density carries
tone, not a blended gradient) but every mark is placed with wrap-around
coordinates so the result tiles edge-to-edge with no visible seam, and there's
no single lit subject to shade a diagonal light gradient across the way
hatch_fill does for an object sprite - a directional gradient repeated across
many tiles would read as an obvious macro-banding "wallpaper" artifact, so this
uses uniform-density ink marks instead.

Run:  python3 generate_terrain_texture.py <output_path> [seed]
"""

import sys
import os
import numpy as np
from PIL import Image

S = 256  # matches generate_sprites.py's canvas resolution (SCALE * 64)

BASE = np.array([0.32, 0.40, 0.18]) * 255           # meadow green-olive (grass.py's own palette)
BASE_VARIANT = np.array([0.26, 0.33, 0.15]) * 255   # a second, darker green (shadow between blades)
HIGHLIGHT = np.array([0.52, 0.58, 0.28]) * 255       # a lit blade-tip green (lighten(BASE, ~0.4))
INK = np.array([0.14, 0.10, 0.08]) * 255            # same INK as generate_sprites.py

# Deliberately NOT drawing any large macro shape (a dirt patch, a distinct blob) anywhere
# on this tile: with only a handful of them fitting on one tile, the eye locks onto their
# exact silhouette repeating in a perfect grid the moment the terrain mesh tiles this
# texture across any real distance - the same "stamped, not natural" failure the forest
# scatter's hard-edged disks had (see Main.cs's ScatterClump). Fine, dense, small-scale ink
# marks read as uniform noise instead - statistically impossible for the eye to notice
# where one tile's copy ends and the next begins.


def _blade_dash(rgb, cx, cy, rng, color, length_range):
    """A tiny hand-inked dash, one of several angles - the same discrete-mark
    idea as generate_sprites.py's hatch lines (tone/texture from many short
    strokes), just isotropic here instead of following one lit-from-upper-left
    gradient. Used for both the dark ink shadow strokes and the lit highlight
    strokes - a real grass blade has both a shaded side and a lit edge, and a
    ground texture with only the dark half read as flat/lifeless in review."""
    length = rng.integers(*length_range)
    direction = rng.choice([(1, -1), (1, 1), (1, 0), (0, 1)])
    dx_step, dy_step = direction
    for i in range(length):
        y, x = (cy + i * dy_step) % S, (cx + i * dx_step) % S
        rgb[y, x] = color


def _shade_blob(rgb, cx, cy, rng, color):
    """A soft 2-3px cluster (not a single pixel) of the given colour - reads as
    a small patch of shadow/undergrowth between blades rather than a faint
    speckle, while still far too small to be individually recognisable once
    tiled (see the note above _blade_dash)."""
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

    # Layered darkest-to-lightest (shadow blobs, then ink shadow strokes, then lit
    # highlight strokes on top) - dense enough that no accidental macro shape near a
    # tile's own edges becomes a visible repeating pattern once tiled at scale
    # (checked below by rendering a tiled preview alongside the real output).
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
