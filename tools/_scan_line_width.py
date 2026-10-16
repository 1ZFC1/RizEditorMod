from pathlib import Path
import struct

data = Path("research/generated/global-metadata.standard.dat").read_bytes()
string_offset, string_size = struct.unpack_from("<II", data, 8 + 2 * 8)
type_offset, type_size = struct.unpack_from("<II", data, 8 + 19 * 8)
count = type_size // 88

def cstr(index):
    end = data.find(b"\0", string_offset + index, string_offset + string_size)
    return data[string_offset + index : end].decode("utf-8", "replace")

hits = []
for index in range(count):
    name_index = struct.unpack_from("<I", data, type_offset + index * 88)[0]
    name = cstr(name_index)
    low = name.lower()
    if any(k in low for k in ("previewline", "linewidth", "lineview", "editorline", "subline", "linemesh", "lineobject")):
        hits.append((index, name))
print("types", hits)

# field names containing width near line
field_offset, field_size = struct.unpack_from("<II", data, 8 + 11 * 8)
field_count = field_size // 12
print("fields", field_count)
for i in range(field_count):
    name_index, type_index, token = struct.unpack_from("<3I", data, field_offset + i * 12)
    name = cstr(name_index)
    if "width" in name.lower() or "Width" in name or name in ("previewLine", "lineWidth"):
        print(f"FIELD {i} {name} type_index={type_index}")
