import re
d = open(r'C:\Users\Lizardan\AppData\Local\Temp\opencode\sc_map\war3map.j','rb').read()
s = d.decode('latin1')
strs = re.findall(r'\"((?:[^\"\\]|\\.)*)\"', s)
print('total strings:', len(strs))
seen = set()
for t in strs:
    if 1 < len(t) < 60 and all(32 <= ord(c) < 127 for c in t):
        seen.add(t)
print('unique printable strings:', len(seen))
for t in sorted(seen):
    print(t)
