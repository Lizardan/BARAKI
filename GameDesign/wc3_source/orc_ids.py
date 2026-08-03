import re
j = open('war3map.j', 'rb').read()
o = 323258
start = j.find(b'set l[lGq]=8', o)
end = j.find(b'SetPlayerTechMaxAllowed', o)
seg = j[start:end].decode('utf-8', 'replace')
ids = []
for ln in seg.split('\n'):
    m = re.search(r'^\s*set\s+\w+\[lGq\]=\$([0-9A-Fa-f]{8})', ln)
    if m:
        v = int(m.group(1), 16)
        ids.append(v.to_bytes(4, 'big').decode('latin1'))
print(len(ids))
print(ids)
