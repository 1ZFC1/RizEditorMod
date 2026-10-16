import re
p = r"RhythmEditor_working\Rizline Editor_Data\level0"
data = open(p, "rb").read()
for needle in [b"OnEaseTypeChange", b"LoopEase", b"SelectEaseType", b"SetCanvasKeyPoint",
               b"MoveCurrentKeyPoint", b"easeTypeEditor", b"Canvas Ease Editor", b"onSetEaseType"]:
    offs = [m.start() for m in re.finditer(re.escape(needle), data)]
    print(f"{needle.decode():22} {len(offs):3}  {offs[:8]}")
