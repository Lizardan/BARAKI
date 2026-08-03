import struct, sys, json
from collections import OrderedDict

def read_w3u(path):
    d = open(path, 'rb').read()
    p = 0
    ver, = struct.unpack('<i', d[p:p+4]); p += 4
    count, = struct.unpack('<i', d[p:p+4]); p += 4
    objects = []
    for _ in range(count):
        orig = d[p:p+4].decode('latin1'); p += 4
        cust = d[p:p+4].decode('latin1'); p += 4
        nmods, = struct.unpack('<i', d[p:p+4]); p += 4
        mods = []
        for _ in range(nmods):
            mid = d[p:p+4].decode('latin1'); p += 4
            mtype, = struct.unpack('<i', d[p:p+4]); p += 4
            if mtype == 0:
                val, = struct.unpack('<i', d[p:p+4]); p += 4
            elif mtype == 1:
                val, = struct.unpack('<f', d[p:p+4]); p += 4
            elif mtype == 2:
                val = None; p += 8
            elif mtype == 3:
                end = d.index(b'\x00', p)
                val = d[p:end].decode('utf-8', 'replace')
                ln = end - p + 1
                p += ln
                p += (4 - (ln % 4)) % 4
            else:
                break
            mods.append((mid, val))
        objects.append({'orig': orig, 'cust': cust, 'mods': OrderedDict(mods)})
    return ver, count, objects

if __name__ == '__main__':
    ver, count, objs = read_w3u(sys.argv[1])
    print('version:', ver, 'objects:', count)
    for o in objs:
        name = o['mods'].get('unam', '')
        rac = o['mods'].get('urac', '')
        print(o['orig'], o['cust'], repr(name), repr(rac))
