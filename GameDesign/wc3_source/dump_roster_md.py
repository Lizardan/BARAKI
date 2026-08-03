import re, struct, os, sys
from collections import OrderedDict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from human_orc_roster import group_blocks, get, race_ids

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

    def stats(name):
        b = seen.get(name)
        if not b:
            return None
        return dict(
            hp=get(b, 'uhpm'), arm=get(b, 'udef'), ms=get(b, 'umvs'),
            gold=get(b, 'ugol'), lvl=get(b, 'ulev'),
            a1r=get(b, 'ua1r'), a1s=get(b, 'ua1s'), a1b=get(b, 'ua1b'),
            a1d=get(b, 'ua1d'), a1e=get(b, 'ua1e'), a2r=get(b, 'ua2r'),
            a2s=get(b, 'ua2s'), a2b=get(b, 'ua2b'), a2d=get(b, 'ua2d'),
            a2e=get(b, 'ua2e'),
            typ=get(b, 'utyp'), rac=get(b, 'urac'), armt=get(b, 'uarm'),
            abi=get(b, 'uabi'), mdl=get(b, 'umdl'), col=get(b, 'ucol'),
            ssc=get(b, 'ussc'), hgt=get(b, 'ushh'), fmax=get(b, 'ufma'),
            buildt=get(b, 'ubld'), sight=get(b, 'usin'), stock=get(b, 'ushu'),
            food=get(b, 'ufoo'), unam2=get(b, 'unam'),
        )

    def fmt(st):
        if not st:
            return ''
        parts = []
        if st['hp'] is not None: parts.append('HP=%s' % st['hp'])
        if st['arm'] is not None: parts.append('AR=%s' % st['arm'])
        if st['ms'] is not None: parts.append('MS=%s' % st['ms'])
        if st['gold'] is not None: parts.append('Bounty=%s' % st['gold'])
        if st['food'] is not None: parts.append('Food=%s' % st['food'])
        if st['col'] is not None: parts.append('Col=%s' % st['col'])
        if st['ssc'] is not None: parts.append('Scale=%s' % st['ssc'])
        for which in ('1', '2'):
            r = st['a%s' % which + 'r']; s = st['a%s' % which + 's']
            b = st['a%s' % which + 'b']; dd = st['a%s' % which + 'd']; de = st['a%s' % which + 'e']
            if r is not None or s is not None:
                dmg = ''
                if b is not None and dd is not None and de is not None:
                    dmg = ' dmg=%s+%sd%s' % (b, dd, de)
                parts.append('atk%s:rng=%s cd=%s%s' % (which, r, s, dmg))
        if st['abi']: parts.append('abi=%s' % st['abi'])
        if st['mdl']: parts.append('mdl=%s' % st['mdl'])
        return ' | '.join(parts)

    out = []
    for race, ids in [('HUMAN', human_ids), ('ORC', orc_ids)]:
        out.append('### %s' % race)
        for uid in ids:
            name = resolve(uid)
            if not name:
                out.append('- `%s` -> ???' % uid)
                continue
            out.append('- `%s` **%s** — %s' % (uid, name, fmt(stats(name))))
        out.append('')
    text = '\n'.join(out)
    with open(r'F:\Unity Projects\BARAKI\GameDesign\wc3_human_orc_roster.md', 'w', encoding='utf-8') as f:
        f.write(text)
    print('written')

if __name__ == '__main__':
    main()
