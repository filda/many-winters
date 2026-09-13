#!/usr/bin/env python3
"""
Generates the soft radial shadow decal under every entity (GroundShadow.cs). One shared
texture: a shadow need not match the silhouette above it, and one blob scales to any size
through GroundShadow's PixelSize.

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

    # Flat dark core, then a smoothstep fade to transparent; a linear falloff shows an edge
    # where the fade rate kinks.
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
