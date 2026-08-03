import struct, sys, re
from collections import Counter, OrderedDict

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

def scan(d):
    out = []
    i = 0
    n = len(d)
    while i < n - 8:
        b = d[i:i+4]
        if is_id(b):
            t = struct.unpack('<i', d[i+4:i+8])[0]
            if t in (0, 1, 2, 3):
                if t == 3:
                    j = i + 8
                    s = b''
                    while j < n and d[j] != 0:
                        s += d[j:j+1]
                        j += 1
                    out.append((b.decode('latin1'), t, s.decode('latin1', 'replace'), i))
                elif t == 0 and i+12 <= n:
                    v = struct.unpack('<i', d[i+8:i+12])[0]
                    out.append((b.decode('latin1'), t, v, i))
                elif i+12 <= n:
                    v = struct.unpack('<f', d[i+8:i+12])[0]
                    out.append((b.decode('latin1'), t, v, i))
        i += 1
    return out

def group_blocks(d, name_field):
    mods = scan(d)
    blocks = []
    cur = []
    for m in mods:
        if m[0] == name_field:
            cur.append(m)
            blocks.append(cur)
            cur = []
        else:
            cur.append(m)
    return blocks

def get(mods, mid):
    vals = [m[2] for m in mods if m[0] == mid]
    return vals[-1] if vals else None

def fmt(v):
    if isinstance(v, float):
        return ('%.3f' % v).rstrip('0').rstrip('.')
    return str(v)

def unit_table(path):
    d = open(path, 'rb').read()
    blocks = group_blocks(d, 'unam')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'unam')
        if not name:
            continue
        if name not in seen:
            seen[name] = b
    print('=== UNITS/BUILDINGS (%d unique) ===' % len(seen))
    rows = []
    for name, b in seen.items():
        rows.append((name,
            get(b,'uhpm'), get(b,'udef'), get(b,'umvs'), get(b,'ugol'), get(b,'ulur'),
            get(b,'upoi'), get(b,'ulev'), get(b,'ua1r'), get(b,'ua1s'), get(b,'ua1b'),
            get(b,'ua1d'), get(b,'ua1e'), get(b,'ua2r'), get(b,'ua2b'),
            get(b,'utyp'), get(b,'urac'), get(b,'uarm'),
            get(b,'uabi'), get(b,'umdl'), get(b,'uico')))
    # sort by name
    for r in sorted(rows, key=lambda x: x[0].lower()):
        name, hp, arm, mvs, gol, lur, poi, lev, a1r, a1s, a1b, a1d, a1e, a2r, a2b, typ, rac, armt, abi, mdl, ico = r
        if hp is None:
            continue
        dmg1 = ''
        if a1b is not None and a1d is not None and a1e is not None:
            dmg1 = '%s+%sd%s' % (a1b, a1d, a1e)
        line = '%-28s HP=%-6s AR=%-4s MS=%-6s Bounty=%-5s Lvl=%s Rng=%s CD=%s Dmg=%s type=%s' % (
            name, hp, arm, mvs, gol, lev, a1r, a1s, dmg1, typ)
        print(line)

if __name__ == '__main__':
    unit_table(sys.argv[1])
