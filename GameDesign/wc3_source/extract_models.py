import re, struct, os, sys
from collections import OrderedDict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from human_orc_roster import group_blocks, get, race_ids

OUT = r'F:\Unity Projects\BARAKI\GameDesign\wc3_models'

def main():
    os.makedirs(OUT, exist_ok=True)
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

    saved = {}
    for race, ids in [('HUMAN', human_ids), ('ORC', orc_ids)]:
        for uid in ids:
            name = resolve(uid)
            if not name:
                continue
            b = seen.get(name)
            mdl = get(b, 'umdl') if b else None
            if not mdl:
                continue
            low = mdl.lower()
            if low.startswith(('units\\', 'buildings\\', 'doodads\\', 'abilities\\', 'objects\\', 'creeps\\', 'textures\\')):
                continue
            variant = mdl.replace('.mdl', '.mdx', 1) if mdl.lower().endswith('.mdl') else mdl
            try:
                data = a.read_file(variant)
            except Exception:
                data = None
            if data is None:
                continue
            fname = os.path.basename(variant)
            if fname in saved:
                continue
            with open(os.path.join(OUT, fname), 'wb') as f:
                f.write(data)
            saved[fname] = (race, uid, name)
            print('%s  %s  (%s)' % (fname, name, race))

    print('\nTOTAL saved:', len(saved), '->', OUT)

if __name__ == '__main__':
    main()
