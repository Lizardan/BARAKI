import struct, sys, re
from collections import OrderedDict, Counter

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

def main(path):
    d = open(path, 'rb').read()
    blocks = group_blocks(d, 'unam')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'unam')
        if not name or name in seen:
            continue
        seen[name] = b
    print('=== RACES (%d units) ===' % len(seen))
    c = Counter()
    for name, b in seen.items():
        r = get(b, 'urac')
        c[r] += 1
    for k, v in c.most_common():
        print('%5d  %s' % (v, k))
    print()
    print('=== UNITS BY RACE (combat: has uhpm + bounty) ===')
    from collections import defaultdict
    byrace = defaultdict(list)
    for name, b in seen.items():
        r = get(b, 'urac')
        hp = get(b, 'uhpm')
        gol = get(b, 'ugol')
        if hp is not None and gol is not None:
            byrace[r].append(name)
    for r in sorted(byrace, key=lambda x: -len(byrace[x])):
        line = '%s (%d):' % (r, len(byrace[r]))
        sys.stdout.buffer.write((line + '\n').encode('utf-8'))
        sys.stdout.buffer.write(('   ' + ', '.join(sorted(byrace[r])) + '\n').encode('utf-8'))

if __name__ == '__main__':
    main(sys.argv[1])
