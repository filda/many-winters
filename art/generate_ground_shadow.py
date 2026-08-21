#!/usr/bin/env python3
"""
Generates a soft radial-gradient shadow decal used under every entity (GroundShadow.cs).

One shared texture, not per-entity-kind art: a shadow's shape doesn't need to match the
silhouette above it to read correctly, and a single soft blob scales cleanly to any size
via GroundShadow's PixelSize math.

Run:  python3 generate_ground_shadow.py <output_path>
"""

import sys
import os
import numpy as np
from PIL import Image

S = 128


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else "ground_shadow.png"

    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx = cy = (S - 1) / 2
    r = np.sqrt(((xx - cx) / cx) ** 2 + ((yy - cy) / cy) ** 2)

    # Flat dark core, then a smooth ease-out fade to fully transparent at the rim - not a
    # linear falloff, which would show a visible edge where the fade rate kinks to zero.
    core = 0.55
    t = np.clip((r - core) / (1.0 - core), 0.0, 1.0)
    fade = 1.0 - (t * t * (3.0 - (2.0 * t)))  # smoothstep, inverted
    alpha = np.clip(fade, 0.0, 1.0) * (r <= 1.0)

    rgb = np.zeros((S, S, 3), dtype=np.uint8)
    rgb[..., 0] = 18
    rgb[..., 1] = 14
    rgb[..., 2] = 10

    out = np.zeros((S, S, 4), dtype=np.uint8)
    out[..., :3] = rgb
    out[..., 3] = (alpha * 150).astype(np.uint8)

    image = Image.fromarray(out, "RGBA")
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    image.save(out_path)
    print(f"wrote {out_path}")


if __name__ == "__main__":
    main()
