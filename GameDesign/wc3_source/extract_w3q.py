import struct
from collections import OrderedDict

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3q', 'rb').read()

def read_cstr(d, i):
    j = i
    while j < len(d) and d[j] != 0:
        j += 1
    return d[i:j].decode('latin1', 'replace'), j - i

# Collect all string mods (type 3) with their field ids
mods = []
pos = 0
while pos < len(d) - 8:
    fid = d[pos:pos+4]
    if all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in fid):
        t = struct.unpack('<I', d[pos+4:pos+8])[0]
        if t == 3:
            s, _ = read_cstr(d, pos + 16)
            mods.append((fid.decode('latin1'), s, pos))
            pos += 12
    pos += 1

print('string mods:', len(mods))
# Group by field id
by_id = OrderedDict()
for fid, s, p in mods:
    by_id.setdefault(fid, []).append(s)

for fid, vals in sorted(by_id.items()):
    print('=== field %s (%d) ===' % (fid, len(vals)))
    seen = OrderedDict()
    for v in vals:
        seen.setdefault(v, 0)
        seen[v] += 1
    for v, c in list(seen.items())[:60]:
        print('   %4d  %s' % (c, v[:80]))
