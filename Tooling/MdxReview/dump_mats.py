"""Dump MDX v800 geoset -> material layer texture mapping."""
from __future__ import annotations

import struct
import sys
from pathlib import Path


def u32(buf: bytes, i: int) -> int:
    return struct.unpack_from("<I", buf, i)[0]


def parse(path: Path) -> None:
    data = path.read_bytes()
    i = 4
    chunks: dict[bytes, bytes] = {}
    while i + 8 <= len(data):
        tag = data[i : i + 4]
        size = u32(data, i + 4)
        chunks[tag] = data[i + 8 : i + 8 + size]
        i += 8 + size

    texs = chunks.get(b"TEXS", b"")
    textures: list[tuple[int, str]] = []
    t = 0
    while t + 268 <= len(texs):
        rid = u32(texs, t)
        name = texs[t + 4 : t + 260].split(b"\x00", 1)[0].decode("ascii", "replace")
        textures.append((rid, name))
        t += 268

    mtls = chunks.get(b"MTLS", b"")
    mats: list[list[tuple[int, int, int]]] = []
    i = 0
    while i + 4 <= len(mtls):
        end = i + u32(mtls, i)
        p = i + 12
        layers: list[tuple[int, int, int]] = []
        if p + 8 <= end and mtls[p : p + 4] == b"LAYS":
            nlay = u32(mtls, p + 4)
            p += 8
            for _ in range(nlay):
                lend = p + u32(mtls, p)
                filt = u32(mtls, p + 4)
                shading = u32(mtls, p + 8)
                tid = u32(mtls, p + 12)
                layers.append((tid, filt, shading))
                p = lend
        mats.append(layers)
        i = end

    geos = chunks.get(b"GEOS", b"")
    i = 0
    gi = 0
    print(f"=== {path.name} tex={len(textures)} mat={len(mats)}")
    while i + 4 <= len(geos):
        end = i + u32(geos, i)
        p = i + 4

        def expect(name: bytes) -> None:
            nonlocal p
            got = geos[p : p + 4]
            if got != name:
                raise RuntimeError(f"{path.name} geo{gi} expected {name!r} got {got!r}")
            p += 4

        expect(b"VRTX")
        vc = u32(geos, p)
        p += 4 + vc * 12
        expect(b"NRMS")
        nc = u32(geos, p)
        p += 4 + nc * 12
        expect(b"PTYP")
        tc = u32(geos, p)
        p += 4 + tc * 4
        expect(b"PCNT")
        gc = u32(geos, p)
        p += 4 + gc * 4
        expect(b"PVTX")
        fc = u32(geos, p)
        p += 4 + fc * 2
        expect(b"GNDX")
        vg = u32(geos, p)
        p += 4 + vg
        expect(b"MTGC")
        mg = u32(geos, p)
        p += 4 + mg * 4
        expect(b"MATS")
        mi = u32(geos, p)
        p += 4 + mi * 4
        mat_id = u32(geos, p)
        layers = mats[mat_id] if 0 <= mat_id < len(mats) else []
        parts = []
        for tid, filt, shading in layers:
            if 0 <= tid < len(textures):
                rid, tex_path = textures[tid]
                leaf = tex_path.split("\\")[-1] if tex_path else f"repl{rid}"
                if not tex_path:
                    leaf = f"repl{rid}"
            else:
                leaf = f"tid{tid}"
            parts.append(f"{leaf}/fm{filt}/sh{shading}")
        print(f"  g{gi} v={vc} f={fc} mat={mat_id} " + " | ".join(parts))
        gi += 1
        i = end


def main() -> int:
    src = Path(r"F:\Unity Projects\BARAKI\Assets\FacelessRetexture_V2")
    files = sorted(list(src.glob("*.mdx")) + list(src.glob("*.Mdx")))
    for path in files:
        parse(path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
