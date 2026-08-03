import re, struct
from collections import OrderedDict, Counter

# --- scan-based w3u parser (from extract_data.py) ---
def is_id(b):
    return all((65 <= x <= 90) or (97 <= x <= 122) or (48 <= x <= 57) for x in b)

def scan(d):
    out = []
    i = 0
    n = len(d)
    while i < n - 8:
        b = d[i:i+4]
        if is_id(b):
            t = struct.unpack('<i', d[i+4:i+8])[0]
            if t in (0, 1, 2, 3):
                if t == 3:
                    j = i + 8
                    s = b''
                    while j < n and d[j] != 0:
                        s += d[j:j+1]
                        j += 1
                    out.append((b.decode('latin1'), t, s.decode('latin1', 'replace'), i))
                elif t == 0 and i+12 <= n:
                    v = struct.unpack('<i', d[i+8:i+12])[0]
                    out.append((b.decode('latin1'), t, v, i))
                elif i+12 <= n:
                    v = struct.unpack('<f', d[i+8:i+12])[0]
                    out.append((b.decode('latin1'), t, v, i))
        i += 1
    return out

def group_blocks(d, name_field):
    mods = scan(d)
    blocks = []
    cur = []
    for m in mods:
        if m[0] == name_field:
            cur.append(m)
            blocks.append(cur)
            cur = []
        else:
            cur.append(m)
    return blocks

def get(mods, mid):
    vals = [m[2] for m in mods if m[0] == mid]
    return vals[-1] if vals else None

# --- ID extraction from JASS ---
def decode_ids(lines):
    ids = []
    for ln in lines:
        m = re.search(r'^\s*set\s+\w+\[lGq\]=\$([0-9A-Fa-f]{8})', ln)
        if m:
            v = int(m.group(1), 16)
            ids.append(v.to_bytes(4, 'big').decode('latin1'))
    return ids

def get_picks(j):
    picks = {}
    for m in re.finditer(rb'has chosen \|cff[0-9A-Fa-f]{6}([A-Za-z][A-Za-z ]*?)\|r!', j):
        race = m.group(1).decode()
        picks.setdefault(race, m.start())
    return picks

def race_ids(j, race, idxline, endmark=b'SetPlayerTechMaxAllowed'):
    o = get_picks(j)[race]
    start = j.find(idxline, o)
    end = j.find(endmark, o)
    return decode_ids(j[start:end].decode('utf-8', 'replace').split('\n'))

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

    human_ids = race_ids(j, 'Human', b'set l[lGq]=7')
    orc_ids = race_ids(j, 'Orc', b'set l[lGq]=8')

    # name -> block
    blocks = group_blocks(d, 'unam')
    seen = OrderedDict()
    for b in blocks:
        name = get(b, 'unam')
        if not name or name in seen:
            continue
        seen[name] = b

    def resolve(uid):
        occ = [m.start() for m in re.finditer(re.escape(uid.encode('latin1')), d)]
        cands = Counter()
        for p in occ:
            nm = nearest_unam(d, p)
            if nm:
                cands[nm] += 1
        return cands.most_common(1)[0][0] if cands else None

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
        )

    def fmt_stats(st):
        if not st:
            return '   (no w3u block)'
        dmg1 = ''
        if st['a1b'] is not None and st['a1d'] is not None and st['a1e'] is not None:
            dmg1 = '%s+%sd%s' % (st['a1b'], st['a1d'], st['a1e'])
        dmg2 = ''
        if st['a2b'] is not None and st['a2d'] is not None and st['a2e'] is not None:
            dmg2 = '%s+%sd%s' % (st['a2b'], st['a2d'], st['a2e'])
        return 'HP=%-6s AR=%-5s MS=%-6s Bounty=%-5s Lvl=%s Rng1=%s CD1=%s Dmg1=%s | Rng2=%s CD2=%s Dmg2=%s | col=%s ssc=%s' % (
            st['hp'], st['arm'], st['ms'], st['gold'], st['lvl'],
            st['a1r'], st['a1s'], dmg1, st['a2r'], st['a2s'], dmg2,
            st['col'], st['ssc'])

    for race, ids in [('HUMAN', human_ids), ('ORC', orc_ids)]:
        print('======== %s (%d ids) ========' % (race, len(ids)))
        for uid in ids:
            name = resolve(uid)
            print('%-6s  %-30s' % (uid, name if name else '???'))
            if name:
                print('        ' + fmt_stats(stats(name)))

if __name__ == '__main__':
    main()
