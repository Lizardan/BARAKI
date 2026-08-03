import struct
from collections import OrderedDict

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3q', 'rb').read()
version, count = struct.unpack('<II', d[:8])
print('version', version, 'count', count)

def read_cstr(d, i):
    j = i
    while j < len(d) and d[j] != 0:
        j += 1
    return d[i:j].decode('latin1', 'replace'), j - i

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

# Object header: newId(4) oldId(4) then {1,0,size}? Actually from dump:
# 0x08 "Resc" 0x0c 0 0x10 1 0x14 0 0x18 62
# mods begin 0x1c
i = 8
objs = []
for o in range(count):
    newId = d[i:i+4].decode('latin1', 'replace')
    oldId = d[i+4:i+8].decode('latin1', 'replace')
    h1, h2, size = struct.unpack('<III', d[i+8:i+20])
    i += 20
    mods = []
    end = i + size
    while i < end and i + 8 <= len(d):
        fid = d[i:i+4]
        t = struct.unpack('<I', d[i+4:i+8])[0]
        if not is_id(fid) or t not in (0,1,2,3):
            break
        if t == 3:
            s, _ = read_cstr(d, i + 16)
            mods.append((fid.decode('latin1'), t, s))
            # string padded to 4
            pad = (len(s.encode('latin1')) + 1 + 3) // 4 * 4
            i += 16 + pad
        else:
            if t == 0:
                v = struct.unpack('<i', d[i+8:i+12])[0]
            else:
                v = struct.unpack('<f', d[i+8:i+12])[0]
            mods.append((fid.decode('latin1'), t, v))
            i += 16
    objs.append((newId, oldId, mods))

print('parsed:', len(objs))
# field id inventory
from collections import Counter
fcount = Counter()
for _,_,mods in objs:
    for m in mods:
        fcount[m[0]] += 1
print('distinct fields:', len(fcount))
for k, c in sorted(fcount.items()):
    print('%4d  %s' % (c, k))
