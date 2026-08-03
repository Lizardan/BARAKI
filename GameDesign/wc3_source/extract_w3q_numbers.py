import struct
from collections import OrderedDict

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3q', 'rb').read()
version, count = struct.unpack('<II', d[:8])

def read_cstr(d, i):
    j = i
    while j < len(d) and d[j] != 0:
        j += 1
    return d[i:j].decode('latin1', 'replace'), j - i

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

# The header field {1,0,size} - size was 62 but mods region is bigger.
# Instead: parse mods greedily; a string mod = id(4)+type(4)+8bytes+string(pad to 4)+trailing(4)
# numeric mod = id(4)+type(4)+value(4)+trailing(4)
# We detect object boundaries by the {1,0,size} pattern where size != a plausible int.
# Simpler: walk, at each position if bytes form id+type+value, consume; count objects by newId+oldId+{1,0,size}.
# We'll rely on the 84 object count and object starts detected by header: 8 bytes id-ish + {1,0,NNN}.
# But NN varies. Use total size to find: object0 starts at 8. After mods of obj0 ends, obj1 starts.
# We don't know where obj0 ends. However, w3q strings: header 16 bytes then string padded then trailing? 
# From w3a: anam at +16. Use that: string value at id+16. Numeric value at id+8.

i = 8
objs = []
for o in range(count):
    newId = d[i:i+4].decode('latin1', 'replace')
    oldId = d[i+4:i+8].decode('latin1', 'replace')
    h1, h2, size = struct.unpack('<III', d[i+8:i+20])
    i += 20
    mods = []
    # parse mods until we reach a plausible next-object header: {id,oldid,1,0,N}
    # Heuristic: scan forward; stop when we see pattern where next 20 bytes = 8 bytes ids + {1,0,N} and
    # the mods-so-far region is >= size. Use size as minimum.
    start = i
    while i < start + size or True:
        if i + 8 > len(d):
            break
        fid = d[i:i+4]
        t = struct.unpack('<I', d[i+4:i+8])[0]
        if not is_id(fid) or t not in (0,1,2,3):
            break
        if t == 3:
            s, slen = read_cstr(d, i + 16)
            mods.append((fid.decode('latin1'), t, s))
            pad = ((slen + 1 + 3) // 4) * 4
            i += 16 + pad
        else:
            if t == 0:
                v = struct.unpack('<i', d[i+8:i+12])[0]
            else:
                v = struct.unpack('<f', d[i+8:i+12])[0]
            mods.append((fid.decode('latin1'), t, v))
            i += 16
        if i - start >= size:
            # maybe next object header starts here: check d[i:i+4] is id, d[i+4:i+8] is id or zeros
            nxt = d[i:i+4]
            oid = d[i+4:i+8]
            if is_id(nxt) and (is_id(oid) or oid == b'\x00'*4):
                if i + 20 <= len(d):
                    hh = struct.unpack('<III', d[i+8:i+20])
                    if hh[0] == 1 and hh[1] == 0 and hh[2] < 2000:
                        break
    objs.append((newId, oldId, mods))

print('parsed:', len(objs))
def get(mods, mid):
    vals = [m[2] for m in mods if m[0] == mid]
    return vals[-1] if vals else None

print('=== UPGRADES (id, gold/none, lumber, time, level, eff1) ===')
for newId, oldId, mods in objs:
    glmb = get(mods, 'glmb')
    gtim = get(mods, 'gtim')
    glvl = get(mods, 'glvl')
    gef1 = get(mods, 'gef1')
    greq = get(mods, 'greq')
    gcls = get(mods, 'gcls')
    print('%-6s lvl=%s time=%s lumber=%s eff=%s cls=%s req=%s' % (newId, glvl, gtim, glmb, gef1, gcls, greq))
