import struct
from collections import OrderedDict

d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.w3a', 'rb').read()

def read_cstr(d, i):
    j = i
    while j < len(d) and d[j] != 0:
        j += 1
    return d[i:j].decode('latin1', 'replace'), j - i

names = OrderedDict()
count = 0
pos = 0
while True:
    i = d.find(b'anam', pos)
    if i < 0:
        break
    # string value is at i+16 (id4 + type4 + int8)
    s, _ = read_cstr(d, i + 16)
    if s:
        names.setdefault(s, 0)
        names[s] += 1
        count += 1
    pos = i + 1

print('total anam mods:', count)
print('unique names:', len(names))
for n, c in sorted(names.items(), key=lambda kv: kv[0].lower()):
    print('%3d  %s' % (c, n))
