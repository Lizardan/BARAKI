import struct, sys, os

def read_int(f):
    return struct.unpack('<i', f.read(4))[0]

def read_float(f):
    return struct.unpack('<f', f.read(4))[0]

def read_chars(f, n=4):
    return f.read(n).decode('latin1')

def read_cstr(f):
    out = bytearray()
    while True:
        b = f.read(1)
        if not b or b == b'\x00':
            break
        out += b
    return out.decode('latin1')

def read_modifications(f, rule):
    mods = []
    n = read_int(f)
    for _ in range(n):
        mid = read_chars(f, 4)
        vtype = read_int(f)
        value = None
        if vtype == 0:
            value = read_int(f)
        elif vtype == 1:
            value = read_float(f)
        elif vtype == 2:
            value = read_int(f)
        elif vtype == 3:
            ln = read_int(f)
            value = f.read(ln).decode('latin1', errors='replace')
        else:
            value = ('unknown_type_%d' % vtype)
        end_id = None
        end_level = None
        if rule == 'type2' and vtype == 2:
            end_id = read_int(f)
            end_level = read_int(f)
        elif rule == 'always':
            end_id = read_int(f)
            end_level = read_int(f)
        mods.append((mid, vtype, value, end_id, end_level))
    return mods

def read_object(f, rule, new_objects):
    t = f.read(1)
    if not t:
        return None, False
    if t == b'D':
        is_del = True
    else:
        is_del = False
    new_id = read_chars(f, 4)
    old_id = read_chars(f, 4)
    if is_del:
        return (is_del, new_id, old_id, None), True
    if not new_objects:
        nvar = read_int(f)
        for _ in range(nvar):
            read_int(f); read_int(f); read_int(f)
    mods = read_modifications(f, rule)
    return (is_del, new_id, old_id, mods), True

def parse(path, rule='type2'):
    f = open(path, 'rb')
    version = read_int(f)
    orig_count = read_int(f)
    orig_objs = []
    for _ in range(orig_count):
        obj, ok = read_object(f, rule, False)
        if not ok: break
        orig_objs.append(obj)
    new_count = read_int(f)
    new_objs = []
    for _ in range(new_count):
        obj, ok = read_object(f, rule, True)
        if not ok: break
        new_objs.append(obj)
    rest = f.read()
    f.close()
    return version, orig_count, orig_objs, new_count, new_objs, rest

if __name__ == '__main__':
    for rule in ['type2', 'always']:
        version, oc, oo, nc, no, rest = parse(sys.argv[1], rule)
        print('rule=%s version=%d orig=%d new=%d leftover=%d' % (rule, version, oc, nc, len(rest)))
