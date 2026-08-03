import struct, sys, re
from collections import OrderedDict

def walk_w3u(path):
    d = open(path, 'rb').read()
    p = 0
    ver, = struct.unpack('<i', d[p:p+4]); p += 4
    count, = struct.unpack('<i', d[p:p+4]); p += 4
    objs = []
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
                trail = d[p:p+4]; p += 4
            elif mtype == 1:
                val, = struct.unpack('<f', d[p:p+4]); p += 4
                trail = d[p:p+4]; p += 4
            elif mtype == 2:
                val = struct.unpack('<Q', d[p:p+8])[0]; p += 8
                trail = d[p:p+4]; p += 4
            elif mtype == 3:
                end = d.index(b'\x00', p)
                val = d[p:end].decode('utf-8', 'replace')
                ln = end - p + 1
                p += ln
                p += (4 - (ln % 4)) % 4
                trail = b''
            else:
                val = None
                trail = b''
            mods.append((mid, val))
        objs.append({'orig': orig, 'cust': cust, 'mods': OrderedDict(mods)})
    return ver, count, objs, p

if __name__ == '__main__':
    ver, count, objs, end = walk_w3u(sys.argv[1])
    print('version:', ver, 'objects:', count, 'file size:', __import__('os').path.getsize(sys.argv[1]), 'parsed to:', end)
    names = {}
    for o in objs:
        nm = o['mods'].get('unam', '')
        if nm:
            names[nm] = o
    print('named objects:', len(names))
    # print first 5
    for i, o in enumerate(objs[:5]):
        print(o['orig'], o['cust'], repr(o['mods'].get('unam')))
