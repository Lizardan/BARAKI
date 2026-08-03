import struct, sys
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

def upgrades(path):
    d = open(path, 'rb').read()
    blocks = group_blocks(d, 'unan')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'unan')
        if name and name not in seen:
            seen[name] = b
    print('=== UPGRADES (%d unique) ===' % len(seen))
    for name, b in sorted(seen.items(), key=lambda kv: kv[0].lower()):
        gcos = get(b, 'gcos')       # gold cost
        glum = get(b, 'glum')       # lumber cost
        grac = get(b, 'grac')       # research time
        greq = get(b, 'greq')       # base requirement
        ghub = get(b, 'ghub')       # 
        # effect fields
        effs = []
        for f in ('gf1s','gf2s','gf3s','gf4s','gDUR'):
            v = get(b, f)
            if v is not None:
                effs.append('%s=%s' % (f, v))
        print('%-40s cost=%s lumber=%s time=%s req=%s %s' % (name[:40], gcos, glum, grac, greq, ' '.join(effs)))

def abilities(path):
    d = open(path, 'rb').read()
    blocks = group_blocks(d, 'anam')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'anam')
        if name and name not in seen:
            seen[name] = b
    print('=== ABILITIES (%d unique) ===' % len(seen))
    for name, b in sorted(seen.items(), key=lambda kv: kv[0].lower()):
        amcs = get(b, 'amcs')   # mana cost
        acdn = get(b, 'acdn')   # cooldown
        arac = get(b, 'arac')   # range
        adur = get(b, 'adur')   # duration
        ahdu = get(b, 'ahdu')   # damage
        aare = get(b, 'aare')   # area
        atp1 = get(b, 'atp1')   # tooltip
        print('%-32s mana=%s cd=%s range=%s dur=%s dmg=%s area=%s | %s' % (name[:32], amcs, acdn, arac, adur, ahdu, aare, (atp1[:70] if isinstance(atp1, str) else atp1)))

if __name__ == '__main__':
    if 'w3q' in sys.argv[1]:
        upgrades(sys.argv[1])
    elif 'w3a' in sys.argv[1]:
        abilities(sys.argv[1])
