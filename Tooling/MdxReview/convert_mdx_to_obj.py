"""Convert Warcraft 3 MDX v800 geosets to OBJ and BLP1 JPEG to PNG.

WC3 is Z-up; Unity is Y-up. Vertices are emitted as (x, z, y) at scale 0.01.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from blp1 import decode_blp

SCALE = 0.01


def _u32(buf: bytes, i: int) -> int:
    return struct.unpack_from("<I", buf, i)[0]


def _tag(buf: bytes, i: int) -> str:
    return buf[i : i + 4].decode("ascii", errors="replace")


def _kgao_alpha_at_zero(rest: bytes) -> float | None:
    if rest[:4] != b"KGAO" or len(rest) < 16:
        return None
    p = 4
    count = _u32(rest, p)
    p += 4
    interp = _u32(rest, p)
    p += 8  # interp + globalSequenceId
    stride = 16 if interp >= 2 else 8
    first = None
    at_zero = None
    for _ in range(count):
        if p + 8 > len(rest):
            break
        frame = _u32(rest, p)
        p += 4
        value = struct.unpack_from("<f", rest, p)[0]
        p += 4
        if interp >= 2:
            p += 8
        if first is None:
            first = value
        if frame <= 0:
            at_zero = value
    return at_zero if at_zero is not None else first


def _hidden_geosets(geoa: bytes | None) -> set[int]:
    hidden: set[int] = set()
    if not geoa:
        return hidden
    i = 0
    while i + 4 <= len(geoa):
        end = i + _u32(geoa, i)
        p = i + 4
        static_alpha = struct.unpack_from("<f", geoa, p)[0]
        p += 4
        p += 4  # flags
        p += 12  # color
        geoset_id = _u32(geoa, p)
        p += 4
        keyed = _kgao_alpha_at_zero(geoa[p:end])
        alpha = keyed if keyed is not None else static_alpha
        if alpha <= 0.01:
            hidden.add(geoset_id)
        i = end
    return hidden


def _is_junk(g: dict) -> bool:
    verts = g["verts"]
    faces = g["faces"]
    vcount = len(verts) // 3
    fcount = len(faces) // 3
    if vcount <= 1 or fcount <= 0:
        return True
    if vcount <= 4 and fcount <= 2:
        return True
    xs, ys, zs = verts[0::3], verts[1::3], verts[2::3]
    dx = (max(xs) - min(xs)) * SCALE
    dy = (max(ys) - min(ys)) * SCALE
    dz = (max(zs) - min(zs)) * SCALE
    return max(dx, dy, dz) > 2.5


def parse_geosets(mdx: bytes) -> list[dict]:
    if mdx[:4] != b"MDLX":
        raise ValueError("Not MDX")
    version = 800
    i = 4
    geos_payload = None
    geoa_payload = None
    while i + 8 <= len(mdx):
        tag = _tag(mdx, i)
        size = _u32(mdx, i + 4)
        payload = mdx[i + 8 : i + 8 + size]
        if tag == "VERS":
            version = struct.unpack_from("<I", payload, 0)[0]
        elif tag == "GEOS":
            geos_payload = payload
        elif tag == "GEOA":
            geoa_payload = payload
        i += 8 + size
    if geos_payload is None:
        return []
    if version != 800:
        raise ValueError(f"Unsupported MDX version {version}")
    geosets = _read_geosets_v800(geos_payload)
    hidden = _hidden_geosets(geoa_payload)
    visible = []
    for idx, g in enumerate(geosets):
        if idx in hidden or _is_junk(g):
            continue
        visible.append(g)
    return visible if visible else geosets


def _expect(buf: bytes, i: int, wanted: str) -> int:
    got = _tag(buf, i)
    if got != wanted:
        raise ValueError(f"Expected {wanted} at {i}, got {got!r}")
    return i + 4


def _read_geosets_v800(payload: bytes) -> list[dict]:
    geosets = []
    i = 0
    n = len(payload)
    while i + 4 <= n:
        inclusive = _u32(payload, i)
        end = i + inclusive
        p = i + 4
        p = _expect(payload, p, "VRTX")
        vcount = _u32(payload, p)
        p += 4
        verts = struct.unpack_from("<" + "f" * (vcount * 3), payload, p)
        p += vcount * 12
        p = _expect(payload, p, "NRMS")
        ncount = _u32(payload, p)
        p += 4
        norms = struct.unpack_from("<" + "f" * (ncount * 3), payload, p)
        p += ncount * 12
        p = _expect(payload, p, "PTYP")
        tcount = _u32(payload, p)
        p += 4
        p += tcount * 4
        p = _expect(payload, p, "PCNT")
        gcount = _u32(payload, p)
        p += 4
        p += gcount * 4
        p = _expect(payload, p, "PVTX")
        fcount = _u32(payload, p)
        p += 4
        faces = struct.unpack_from("<" + "H" * fcount, payload, p)
        p += fcount * 2
        p = _expect(payload, p, "GNDX")
        vg = _u32(payload, p)
        p += 4 + vg
        p = _expect(payload, p, "MTGC")
        mg = _u32(payload, p)
        p += 4 + mg * 4
        p = _expect(payload, p, "MATS")
        mi = _u32(payload, p)
        p += 4 + mi * 4
        p += 12  # materialId, selectionGroup, selectionFlags
        p += 28  # Extent (radius + min3 + max3)
        ext_count = _u32(payload, p)
        p += 4 + ext_count * 28
        p = _expect(payload, p, "UVAS")
        uv_sets = _u32(payload, p)
        p += 4
        uvs: tuple[float, ...] = ()
        for _ in range(uv_sets):
            p = _expect(payload, p, "UVBS")
            ucount = _u32(payload, p)
            p += 4
            coords = struct.unpack_from("<" + "f" * (ucount * 2), payload, p)
            p += ucount * 8
            if not uvs:
                uvs = coords
        geosets.append({"verts": verts, "norms": norms, "faces": faces, "uvs": uvs})
        i = end
    return geosets


def _to_unity(x: float, y: float, z: float) -> tuple[float, float, float]:
    return (x * SCALE, z * SCALE, y * SCALE)


def write_obj(path: Path, geosets: list[dict]) -> None:
    lines = ["# WC3 MDX geosets → Unity Y-up", "mtllib materials.mtl", "usemtl Faceless", "o Model"]
    v_off = 0
    for gi, g in enumerate(geosets):
        verts = g["verts"]
        norms = g["norms"]
        uvs = g["uvs"]
        faces = g["faces"]
        for i in range(0, len(verts), 3):
            x, y, z = _to_unity(verts[i], verts[i + 1], verts[i + 2])
            lines.append(f"v {x:.6f} {y:.6f} {z:.6f}")
        if norms:
            for i in range(0, len(norms), 3):
                x, y, z = _to_unity(norms[i], norms[i + 1], norms[i + 2])
                # undo scale on normals
                x, y, z = x / SCALE, y / SCALE, z / SCALE
                lines.append(f"vn {x:.6f} {y:.6f} {z:.6f}")
        if uvs:
            for i in range(0, len(uvs), 2):
                lines.append(f"vt {uvs[i]:.6f} {1.0 - uvs[i + 1]:.6f}")
        has_n = len(norms) == len(verts)
        has_t = len(uvs) == (len(verts) // 3) * 2
        for i in range(0, len(faces), 3):
            a, b, c = faces[i] + 1 + v_off, faces[i + 1] + 1 + v_off, faces[i + 2] + 1 + v_off
            # flip winding after Y-up swap
            if has_n and has_t:
                lines.append(f"f {c}/{c}/{c} {b}/{b}/{b} {a}/{a}/{a}")
            elif has_t:
                lines.append(f"f {c}/{c} {b}/{b} {a}/{a}")
            elif has_n:
                lines.append(f"f {c}//{c} {b}//{b} {a}//{a}")
            else:
                lines.append(f"f {c} {b} {a}")
        v_off += len(verts) // 3
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


MODELS = [
    (1, "FacelessOne_G.mdx", "01_FacelessOne"),
    (2, "FacelessOneBerserker_G.mdx", "02_FacelessOneBerserker"),
    (3, "RangedFacelessone_G.Mdx", "03_RangedFacelessone"),
    (4, "FacelessOneReaper_G.mdx", "04_FacelessOneReaper"),
    (5, "FacelessOneSorcerer_G1.mdx", "05_FacelessOneSorcerer_G1"),
    (6, "FacelessOneSorcerer_G2.mdx", "06_FacelessOneSorcerer_G2"),
    (7, "FacelessOneSorcerer_G3.mdx", "07_FacelessOneSorcerer_G3"),
    (8, "FacelessKing_G.mdx", "08_FacelessKing"),
    (9, "FacelessThanatos_G.mdx", "09_FacelessThanatos"),
    (10, "Unbroken_Izual.mdx", "10_Unbroken_Izual"),
    (11, "FacelessOneWorker_G.mdx", "11_FacelessOneWorker"),
    (12, "FacelessOneWorker_G_Portrait.mdx", "12_FacelessOneWorker_Portrait"),
]


def main() -> int:
    repo = Path(__file__).resolve().parents[2]
    src = repo / "Assets" / "FacelessRetexture_V2"
    dst = repo / "Assets" / "Game" / "Art" / "Races" / "Faceless"
    dst.mkdir(parents=True, exist_ok=True)
    tex = decode_blp((src / "FacelessOneUnbrokenV2.blp").read_bytes())
    png = dst / "FacelessOneUnbrokenV2.png"
    tex.save(png)
    print("png", png, tex.size)
    mtl = dst / "materials.mtl"
    mtl.write_text(
        "newmtl Faceless\nmap_Kd FacelessOneUnbrokenV2.png\n",
        encoding="utf-8",
    )
    for _num, filename, stem in MODELS:
        path = src / filename
        geosets = parse_geosets(path.read_bytes())
        out = dst / f"{stem}.obj"
        write_obj(out, geosets)
        print(f"{stem}: {len(geosets)} geosets -> {out.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
