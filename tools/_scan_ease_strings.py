import re, sys
p = r"RhythmEditor_working\research\generated\global-metadata.standard.dat"
data = open(p, "rb").read()
print("size", len(data))
for needle in [b"InOut Quart", b"Linear", b"In Circle", b"Sin"]:
    offs = [m.start() for m in re.finditer(re.escape(needle), data)]
    print(needle.decode(), "occurrences:", len(offs), offs[:12])
