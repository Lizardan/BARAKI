import struct, sys, re

def parse_file(path):
    d = open(path, 'rb').read()
    return d

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

def scan(d, known_ids=None):
    """Scan for mods: id(4) + type(int32) + value + trailing(int32). Return list of (id, type, value, pos)."""
    out = []
    i = 0
    n = len(d)
    while i < n - 8:
        b = d[i:i+4]
        if is_id(b):
            t = struct.unpack('<i', d[i+4:i+8])[0]
            if t in (0, 1, 2, 3):
                if t == 3:
                    # string: null-terminated
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

def group_units(path):
    d = parse_file(path)
    # Find object boundaries by locating 'unam' mods, then walk backward? 
    # Instead: sequential scan, split blocks at 'unam' string mods.
    # We detect blocks: start after header; each 'unam' string value marks end of a unit block.
    # Object header = newId(4) oldId(4) + {int32,int32,int32} = 20 bytes; we skip garbage before first valid mod.
    mods = scan(d)
    units = []
    cur = []
    for m in mods:
        if m[0] == 'unam':
            cur.append(m)
            units.append(cur)
            cur = []
        else:
            cur.append(m)
    return units

if __name__ == '__main__':
    units = group_units(sys.argv[1])
    print('total unit-blocks:', len(units))
    # names histogram
    from collections import Counter
    names = Counter()
    for u in units:
        nm = next((m[2] for m in u if m[0] == 'unam'), None)
        if nm is None:
            continue
        names[nm] += 1
    print('unique names:', len(names))
    for nm, c in names.most_common(60):
        print('  %5d  %s' % (c, nm))
