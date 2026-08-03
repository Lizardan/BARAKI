import struct
import sys

def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

def parse_objects(d):
    version, count = struct.unpack('<II', d[:8])
    objs = []
    i = 8
    for o in range(count):
        newId = d[i:i+4]
        oldId = d[i+4:i+8]
        h1, h2, size = struct.unpack('<III', d[i+8:i+20])
        i += 20
        mods = []
        end = i + size if size else None
        while True:
            if end and i >= end:
                break
            if i + 8 > len(d):
                break
            fid = d[i:i+4]
            t = struct.unpack('<I', d[i+4:i+8])[0]
            if not is_id(fid) or t not in (0,1,2,3):
                # not a mod; probably object header mismatch
                break
            if t == 3:
                j = i + 8
                s = b''
                while j < len(d) and d[j] != 0:
                    s += d[j:j+1]
                    j += 1
                padded = ((len(s)+1+3)//4)*4
                mods.append((fid.decode('latin1'), t, s.decode('latin1','replace'), i))
                i += 8 + padded + 4
            else:
                if i + 12 > len(d):
                    break
                if t == 0:
                    v = struct.unpack('<i', d[i+8:i+12])[0]
                else:
                    v = struct.unpack('<f', d[i+8:i+12])[0]
                mods.append((fid.decode('latin1'), t, v, i))
                i += 16
        objs.append((newId.decode('latin1','replace'), oldId.decode('latin1','replace'), mods))
    return objs

def main(path):
    d = open(path, 'rb').read()
    print('size', len(d))
    objs = parse_objects(d)
    print('parsed objects:', len(objs))
    # try to collect ability names
    names = {}
    for oid, ooid, mods in objs:
        name = None
        for m in mods:
            if m[0] == 'anam' and isinstance(m[2], str):
                name = m[2]
        if name:
            names.setdefault(name, []).append(oid)
    print('names found:', len(names))
    for n, ids in sorted(names.items())[:80]:
        print(n[:40], ids[:5])

if __name__ == '__main__':
    main(sys.argv[1])
