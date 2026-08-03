import struct
from collections import OrderedDict

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
                        s += d[j:j+1]; j += 1
                    out.append((b.decode('latin1'), t, s.decode('latin1', 'replace'), i))
                elif t == 0 and i+12 <= n:
                    out.append((b.decode('latin1'), t, struct.unpack('<i', d[i+8:i+12])[0], i))
                elif i+12 <= n:
                    out.append((b.decode('latin1'), t, struct.unpack('<f', d[i+8:i+12])[0], i))
        i += 1
    return out

def group_blocks(d, name_field):
    mods = scan(d)
    blocks = []
    cur = []
    for m in mods:
        if m[0] == name_field:
            cur.append(m); blocks.append(cur); cur = []
        else:
            cur.append(m)
    return blocks

def get(mods, mid):
    vals = [m[2] for m in mods if m[0] == mid]
    return vals[-1] if vals else None

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3u', 'rb').read()
blocks = group_blocks(d, 'unam')
seen = OrderedDict()
for b in blocks:
    name = get(b, 'unam')
    if name and name not in seen:
        seen[name] = b

combat = []
for name, b in seen.items():
    hp = get(b, 'uhpm')
    gol = get(b, 'ugol')
    mvs = get(b, 'umvs')
    if hp is None or gol is None or mvs is None:
        continue
    if gol <= 0:
        continue
    combat.append((name,
        int(hp), get(b,'udef'), get(b,'umvs'), int(gol), get(b,'ulev'),
        get(b,'ua1r'), get(b,'ua1s'), get(b,'ua1b'), get(b,'ua1d'), get(b,'ua1e'),
        get(b,'urac'), get(b,'utyp')))

print('=== COMBAT UNITS with bounty (%d) ===' % len(combat))
def fmt(v):
    if isinstance(v, float):
        return ('%.1f' % v).rstrip('0').rstrip('.')
    return str(v)

for r in sorted(combat, key=lambda x: (int(x[3]) if x[3] else 0)):
    name, hp, arm, mvs, gol, lev, a1r, a1s, a1b, a1d, a1e, rac, typ = r
    dmg = ''
    if a1b is not None and a1d is not None and a1e is not None:
        dmg = '%s-%s' % (fmt(a1b), fmt(a1e))
    print('%-32s HP=%-7s AR=%-4s MS=%-4s B=%-5s Rng=%-5s CD=%-4s Dmg=%-10s %s %s' % (
        name, hp, arm, mvs, gol, a1r, a1s, dmg, rac or '', typ or ''))
