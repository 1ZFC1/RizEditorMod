import os, re
root = r"RhythmEditor_working"
targets = [
    os.path.join(root, "GameAssembly.dll"),
]
data_dir = os.path.join(root, "Rizline Editor_Data")
for dirpath, dirnames, filenames in os.walk(data_dir):
    for fn in filenames:
        targets.append(os.path.join(dirpath, fn))

needles = [b"InOut Quart", b"In Circle", b"Out Circle", b"In Quint", b"Out Quint",
           b"Linear", b"In Quad", b"Out Quad", b"In Cubic", b"Zero", b"One",
           b"Bounce", b"Elastic", b"Back", b"Expo", b"Sine", b"Spring",
           b"Vibrate", b"Shake", b"Oscillate", b"Wave", b"Jitter"]
for path in targets:
    try:
        with open(path, "rb") as fh:
            data = fh.read()
    except Exception as exc:
        print("skip", path, exc); continue
    hits = {}
    for n in needles:
        c = data.count(n)
        if c:
            hits[n.decode()] = c
    if hits:
        print(f"{os.path.relpath(path, root)}  ({len(data)} bytes)")
        for k, v in hits.items():
            print(f"    {k}: {v}")
