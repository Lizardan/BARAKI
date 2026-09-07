"""Decode BLP1 textures referenced by Nazjatar building MDX files to PNG.

Usage: pwsh/python from repo root:
    python Tooling/MdxReview/convert_nazjatar_buildings_textures.py
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from blp1 import decode_blp

SRC = Path(__file__).resolve().parents[2] / "Assets" / "Nazjatar_Houses_By_Ageron"
DST = (
    Path(__file__).resolve().parents[2]
    / "Assets"
    / "Game"
    / "Art"
    / "Races"
    / "Faceless"
    / "Buildings"
    / "Review"
    / "Textures"
)

MODELS = [
    "8nzj_nazjatar_building_small006_2532410.mdx",
    "8nzj_nazjatar_building_small01_2324290.mdx",
    "8nzj_nazjatar_building_small01_2565378.mdx",
    "8nzj_nazjatar_building_small01_2578738.mdx",
    "8nzj_nazjatar_building_small03_2406772.mdx",
]


def _u32(buf: bytes, i: int) -> int:
    return struct.unpack_from("<I", buf, i)[0]


def tex_paths(mdx: bytes) -> list[str]:
    """TEXS entries: replaceableId u32 + 256-byte path + 8 bytes flags = 268."""
    i = 4
    paths: list[str] = []
    while i + 8 <= len(mdx):
        tag = mdx[i : i + 4]
        size = _u32(mdx, i + 4)
        payload = mdx[i + 8 : i + 8 + size]
        if tag == b"TEXS":
            for t in range(0, len(payload) - 267, 268):
                end = payload.index(0, t + 4, t + 4 + 256)
                paths.append(payload[t + 4 : end].decode("ascii", "replace"))
        i += 8 + size
    return paths


def main() -> int:
    DST.mkdir(parents=True, exist_ok=True)
    referenced: set[str] = set()
    for name in MODELS:
        paths = tex_paths((SRC / name).read_bytes())
        print(f"{name}: {len(paths)} textures")
        for p in paths:
            referenced.add(Path(p.replace("\\", "/")).name)

    ok = 0
    for blp_name in sorted(referenced):
        blp = SRC / blp_name
        if not blp.exists():
            print("MISSING", blp_name)
            continue
        img = decode_blp(blp.read_bytes())
        out = DST / (Path(blp_name).stem + ".png")
        img.save(out)
        ok += 1
        print(f"png {out.name} {img.size} mode={img.mode}")
    print(f"decoded {ok}/{len(referenced)} textures -> {DST}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
