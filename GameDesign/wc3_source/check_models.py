import re, struct, os, sys
from collections import OrderedDict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from human_orc_roster import group_blocks, get, decode_ids, get_picks, race_ids

def main():
    j = open('war3map.j', 'rb').read()
    d = open('war3map.w3u', 'rb').read()
    human_ids = race_ids(j, 'Human', b'set l[lGq]=7')
    orc_ids = race_ids(j, 'Orc', b'set l[lGq]=8')

    blocks = group_blocks(d, 'unam')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'unam')
        if not name or name in seen:
            continue
        seen[name] = b

    def nearest_unam(data, pos, max_delta=6000):
        n = data.find(b'unam', pos)
        if n == -1 or n - pos > max_delta:
            return None
        t = struct.unpack('<i', data[n+4:n+8])[0]
        if t != 3:
            return None
        end = data.index(b'\x00', n+8)
        return data[n+8:end].decode('utf-8', 'replace')

    def resolve(uid):
        occ = [m.start() for m in re.finditer(re.escape(uid.encode('latin1')), d)]
        cands = {}
        for p in occ:
            nm = nearest_unam(d, p)
            if nm:
                cands[nm] = cands.get(nm, 0) + 1
        return max(cands, key=cands.get) if cands else None

    import mpyq, tempfile
    src = open(r'C:\Users\Lizardan\Downloads\SurvivalChaosReborn v1.58c_w3p.w3x', 'rb').read()[512:]
    tmp = os.path.join(tempfile.gettempdir(), 'sc_map_noheader.mpq')
    open(tmp, 'wb').write(src)
    a = mpyq.MPQArchive(tmp, listfile=False)

    rows = []
    for race, ids in [('HUMAN', human_ids), ('ORC', orc_ids)]:
        for uid in ids:
            name = resolve(uid)
            if not name:
                continue
            b = seen.get(name)
            mdl = get(b, 'umdl') if b else None
            if not mdl:
                continue
            # try as-is and with .mdx/.mdl swap
            present = 'NOT-IN-MAP'
            size = ''
            for variant in [mdl, mdl.replace('.mdl', '.mdx', 1) if mdl.lower().endswith('.mdl') else mdl.replace('.mdx', '.mdl', 1)]:
                try:
                    data = a.read_file(variant)
                    if data is not None:
                        present = 'IN-MAP'
                        size = str(len(data))
                        if variant != mdl:
                            present += ' (as ' + os.path.basename(variant) + ')'
                        break
                except Exception as e:
                    pass
            rows.append((race, uid, name, mdl, present, size))

    for race, uid, name, mdl, present, size in rows:
        print('%-5s %-6s %-26s %-45s %-10s %s' % (race, uid, name, mdl, present, size))

if __name__ == '__main__':
    main()
