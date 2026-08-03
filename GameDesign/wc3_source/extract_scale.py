import struct
from collections import OrderedDict

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

def scan(d):
    out = []; i = 0; n = len(d)
    while i < n - 8:
        b = d[i:i+4]
        if is_id(b):
            t = struct.unpack('<i', d[i+4:i+8])[0]
            if t in (0,1,2,3):
                if t == 3:
                    j = i+8; s = b''
                    while j < n and d[j] != 0:
                        s += d[j:j+1]; j += 1
                    out.append((b.decode('latin1'), t, s.decode('latin1','replace'), i))
                elif t == 0 and i+12 <= n:
                    out.append((b.decode('latin1'), t, struct.unpack('<i', d[i+8:i+12])[0], i))
                elif i+12 <= n:
                    out.append((b.decode('latin1'), t, struct.unpack('<f', d[i+8:i+12])[0], i))
        i += 1
    return out

def gb(d, namef):
    mods = scan(d); blk = []; cur = []
    for m in mods:
        if m[0] == namef:
            cur.append(m); blk.append(cur); cur = []
        else:
            cur.append(m)
    return blk

def get(mods, mid):
    v = [m[2] for m in mods if m[0] == mid]
    return v[-1] if v else None

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3u', 'rb').read()
blocks = gb(d, 'unam')
seen = OrderedDict()
for b in blocks:
    n = get(b, 'unam')
    if n and n not in seen:
        seen[n] = b

print('%-24s %-10s %-8s %-7s %-7s' % ('NAME', 'COLLISION', 'SCALE%', 'MS', 'MDL'))
print('-'*70)
keys = ['Barracks','Fortress','Tower','Town Hall','Keep','Castle','Elite','Gate',
        'Altar','Swordsman','Warrior','Mage','Archer','Spearman','Grunt','Footman',
        'Abyssal','Zombie','Golem','Fortress','Main','Militia','Peasant']
seen_names = set()
for target in keys:
    for n, b in seen.items():
        if target.lower() in n.lower() and n not in seen_names:
            col = get(b, 'ucol')
            sc = get(b, 'ussc')
            ms = get(b, 'umvs')
            mdl = (get(b, 'umdl') or '')
            print('%-24s %-10s %-8s %-6s %s' % (n[:24], col, sc, ms, mdl[-40:]))
            seen_names.add(n)
            break