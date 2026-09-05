import struct
from pathlib import Path


def u32(b, i):
    return struct.unpack_from("<I", b, i)[0]


def i32(b, i):
    return struct.unpack_from("<i", b, i)[0]


def f32(b, i):
    return struct.unpack_from("<f", b, i)[0]


def tag(b, i):
    return b[i : i + 4].decode("ascii", "replace")


def cstr(b, i, n):
    return b[i : i + n].split(b"\x00", 1)[0].decode("latin1", "replace")


def dump(path):
    d = Path(path).read_bytes()
    print("====", Path(path).name, "size", len(d))
    i = 4
    chunks = {}
    while i + 8 <= len(d):
        t = tag(d, i)
        sz = u32(d, i + 4)
        payload = d[i + 8 : i + 8 + sz]
        chunks[t] = payload
        print(f"  chunk {t} {sz}")
        i += 8 + sz

    texs = chunks.get("TEXS")
    mtls = chunks.get("MTLS")
    geos = chunks.get("GEOS")
    geoa = chunks.get("GEOA")
    seqs = chunks.get("SEQS")
    pivt = chunks.get("PIVT")

    textures = []
    p = 0
    while texs and p + 268 <= len(texs):
        textures.append((u32(texs, p), cstr(texs, p + 4, 256)))
        p += 268
    print("textures:")
    for ti, (r, tp) in enumerate(textures):
        print(f"  t{ti} repl={r} {tp}")

    mats = []
    i = 0
    while mtls and i + 4 <= len(mtls):
        inc = u32(mtls, i)
        end = i + inc
        p = i + 12
        layers = []
        if p + 8 <= end and mtls[p : p + 4] == b"LAYS":
            n = u32(mtls, p + 4)
            p += 8
            for _ in range(n):
                lsz = u32(mtls, p)
                fmode = u32(mtls, p + 4)
                sh = u32(mtls, p + 8)
                tid = u32(mtls, p + 12)
                repl, tp = textures[tid] if tid < len(textures) else (-1, "?")
                name = Path(str(tp).replace("\\", "/")).name if tp else "team"
                layers.append((fmode, sh, tid, repl, name))
                p += lsz
        mats.append(layers)
        i = end
    print("materials:")
    for mi, layers in enumerate(mats):
        print(f"  m{mi} {layers}")

    print("sequences:")
    if seqs:
        for s in range(0, len(seqs), 132):
            name = cstr(seqs, s, 80)
            st = i32(seqs, s + 80)
            en = i32(seqs, s + 84)
            print(f"  {name!r} {st}-{en}")
    print("pivots", 0 if not pivt else len(pivt) // 12)

    print("geosets:")
    i = 0
    gi = 0
    all_ids = set()
    while geos and i + 4 <= len(geos):
        inc = u32(geos, i)
        end = i + inc
        p = i + 4
        assert geos[p : p + 4] == b"VRTX"
        p += 4
        vcount = u32(geos, p)
        p += 4 + vcount * 12
        assert geos[p : p + 4] == b"NRMS"
        p += 4
        ncount = u32(geos, p)
        p += 4 + ncount * 12
        assert geos[p : p + 4] == b"PTYP"
        p += 4
        tcount = u32(geos, p)
        p += 4 + tcount * 4
        assert geos[p : p + 4] == b"PCNT"
        p += 4
        gcount = u32(geos, p)
        p += 4 + gcount * 4
        assert geos[p : p + 4] == b"PVTX"
        p += 4
        fcount = u32(geos, p)
        p += 4 + fcount * 2
        assert geos[p : p + 4] == b"GNDX"
        p += 4
        vg = u32(geos, p)
        p += 4 + vg
        assert geos[p : p + 4] == b"MTGC"
        p += 4
        mg = u32(geos, p)
        p += 4 + mg * 4
        assert geos[p : p + 4] == b"MATS"
        p += 4
        mi = u32(geos, p)
        p += 4
        mids = [u32(geos, p + 4 * k) for k in range(mi)]
        p += mi * 4
        mat_id = u32(geos, p)
        sel = u32(geos, p + 4)
        flags = u32(geos, p + 8)
        all_ids.update(mids)
        layers = mats[mat_id] if mat_id < len(mats) else []
        layer_names = [(fm, nm) for fm, sh, tid, r, nm in layers]
        uniq = sorted(set(mids))
        extra = "..." if len(uniq) > 24 else ""
        print(
            f"  g{gi} v={vcount} faces={fcount} mat={mat_id} sel={sel} flags={flags} "
            f"groups={mg} boneIds={uniq[:24]}{extra} layers={layer_names}"
        )
        gi += 1
        i = end
    print("unique matrix objectIds", sorted(all_ids))

    print("GEOA (size, alpha, flags, color[3], geosetId):")
    i = 0
    ai = 0
    while geoa and i + 4 <= len(geoa):
        inc = u32(geoa, i)
        end = i + inc
        static_a = f32(geoa, i + 4)
        flags = u32(geoa, i + 8)
        color = (f32(geoa, i + 12), f32(geoa, i + 16), f32(geoa, i + 20))
        gid = i32(geoa, i + 24)
        rest = geoa[i + 28 : end]
        kgao = None
        p = 0
        while p + 8 <= len(rest):
            t = rest[p : p + 4]
            if t not in (b"KGAO", b"KGAC"):
                break
            n = u32(rest, p + 4)
            interp = u32(rest, p + 8)
            p += 16
            if t == b"KGAO":
                stride = 16 if interp >= 2 else 8
                keys = []
                for _ in range(n):
                    tm = i32(rest, p)
                    val = f32(rest, p + 4)
                    keys.append((tm, round(val, 3)))
                    p += stride
                kgao = keys[:10]
                if len(keys) > 10:
                    kgao.append(("n", len(keys)))
            else:
                stride = 16 if interp < 2 else 40
                p += n * stride
        print(
            f"  a{ai} geoset={gid} staticA={static_a:.3f} flags={flags} "
            f"color={tuple(round(c, 2) for c in color)} kgao={kgao}"
        )
        ai += 1
        i = end


dump(r"F:\Unity Projects\BARAKI\Assets\FacelessRetexture_V2\FacelessKing_G.mdx")
print()
dump(r"F:\Unity Projects\BARAKI\Assets\FacelessRetexture_V2\FacelessOne_G.mdx")
