#!/usr/bin/env python3
"""
Generates a seamless, tileable ground texture for 3D terrain meshes.

Same pixel-art philosophy as generate_sprites.py (hard edges, no antialiasing,
a muted earthy palette) but every mark is placed with wrap-around coordinates so
the result tiles edge-to-edge with no visible seam - this gets repeated across
a whole terrain mesh rather than standing alone like an object sprite.

Run:  python3 generate_terrain_texture.py <output_path> [seed]
"""

import sys
import os
import numpy as np
from PIL import Image

S = 64


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else "ground.png"
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1

    rng = np.random.default_rng(seed)  # fixed seed: deterministic, reproducible art

    base = np.array([0.30, 0.28, 0.16])
    dark = base * 0.55
    light = base * 1.4

    rgb = np.tile((base * 255).clip(0, 255).astype(np.uint8), (S, S, 1))

    # Hand-inked hatch marks and speckles, wrapped so they tile seamlessly. Needs to be dense
    # enough (checked by tiling a 4x4 grid and eyeballing it) that no accidental macro shape
    # near the tile edges becomes a visible repeating "wallpaper" artifact once tiled at scale.
    for _ in range(900):
        cx, cy = int(rng.integers(0, S)), int(rng.integers(0, S))
        length = int(rng.integers(1, 4))
        horizontal = rng.random() < 0.5
        tone = dark if rng.random() < 0.6 else light
        color = (tone * 255).clip(0, 255).astype(np.uint8)
        for i in range(length):
            x = (cx + (i if horizontal else 0)) % S
            y = (cy + (0 if horizontal else i)) % S
            rgb[y, x] = color

    image = Image.fromarray(rgb, "RGB")
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    image.save(out_path)
    print(f"wrote {out_path}")


if __name__ == "__main__":
    main()
