from pathlib import Path
import struct

data = Path("research/generated/global-metadata.standard.dat").read_bytes()
string_offset, string_size = struct.unpack_from("<II", data, 8 + 2 * 8)
type_offset, type_size = struct.unpack_from("<II", data, 8 + 19 * 8)
count = type_size // 88

def cstr(index):
    end = data.find(b"\0", string_offset + index, string_offset + string_size)
    return data[string_offset + index : end].decode("utf-8", "replace")

for index in range(count):
    values = struct.unpack_from("<22I", data, type_offset + index * 88)
    name = cstr(values[0])
    ns = cstr(values[1])
    if ns:
        continue
    if any(k in name for k in ("Preview", "LineObject", "EditorLine", "LineView", "SubLine", "JudgeLine", "LineRender", "ChartLine")):
        print(index, name)
