import re, struct

def decode_ids(lines):
    ids = []
    for ln in lines:
        m = re.search(r'^\s*set\s+\w+\[lGq\]=\$([0-9A-Fa-f]{8})', ln)
        if m:
            v = int(m.group(1), 16)
            b = v.to_bytes(4, 'big')
            ids.append(b.decode('latin1'))
    return ids

def nearest_unam(d, pos, max_delta=6000):
    n = d.find(b'unam', pos)
    if n == -1 or n - pos > max_delta:
        return None
    t = struct.unpack('<i', d[n+4:n+8])[0]
    if t != 3:
        return None
    end = d.index(b'\x00', n+8)
    return d[n+8:end].decode('utf-8', 'replace')

def main():
    j = open('war3map.j', 'rb').read()
    d = open('war3map.w3u', 'rb').read()

    human_ids = None
    orc_ids = None
    picks = {}
    for m in re.finditer(rb'has chosen \|cff[0-9A-Fa-f]{6}([A-Za-z][A-Za-z ]*?)\|r!', j):
        race = m.group(1).decode()
        picks.setdefault(race, m.start())
    if 'Human' in picks:
        o = picks['Human']
        start = j.find(b'set l[lGq]=7', o)
        end = j.find(b'SetPlayerTechMaxAllowed', o)
        human_ids = decode_ids(j[start:end].decode('utf-8', 'replace').split('\n'))
    if 'Orc' in picks:
        o = picks['Orc']
        start = j.find(b'set l[lGq]=8', o)
        end = j.find(b'SetPlayerTechMaxAllowed', o)
        orc_ids = decode_ids(j[start:end].decode('utf-8', 'replace').split('\n'))

    for race, ids in [('HUMAN', human_ids), ('ORC', orc_ids)]:
        print('===== %s (%d ids) =====' % (race, len(ids)))
        for uid in ids:
            occ = [m.start() for m in re.finditer(re.escape(uid.encode('latin1')), d)]
            cands = {}
            for p in occ:
                nm = nearest_unam(d, p)
                if nm:
                    cands[nm] = cands.get(nm, 0) + 1
            if cands:
                best = max(cands, key=cands.get)
                print('%-6s -> %s  (cands: %s)' % (uid, best, ', '.join('%s x%d' % (k, v) for k, v in sorted(cands.items(), key=lambda x: -x[1]))))
            else:
                print('%-6s -> ???' % uid)

if __name__ == '__main__':
    main()
