#!/usr/bin/env python3
"""Inspect selected type records in a reconstructed IL2CPP v31 metadata file."""

from __future__ import annotations

import argparse
import struct
from pathlib import Path


IL2CPP_MAGIC = 0xFAB11BAF
IL2CPP_VERSION = 31
HEADER_BYTES = 0x100
STRING_HEAP_PAIR = 2
TYPE_DEFINITION_PAIR = 19
TYPE_DEFINITION_BYTES = 88
METHOD_DEFINITION_PAIR = 5
METHOD_DEFINITION_BYTES = 36
PARAMETER_DEFINITION_PAIR = 10
PARAMETER_DEFINITION_BYTES = 12
FIELD_DEFINITION_PAIR = 11
FIELD_DEFINITION_BYTES = 12
NESTED_TYPE_PAIR = 15
NESTED_TYPE_BYTES = 4
IMAGE_DEFINITION_PAIR = 20
IMAGE_DEFINITION_BYTES = 40


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("metadata", type=Path)
    parser.add_argument("--type", action="append", dest="types")
    parser.add_argument("--type-index", action="append", type=int, dest="type_indexes")
    parser.add_argument("--first", type=int, default=0)
    parser.add_argument("--images", action="store_true")
    return parser.parse_args()


def read_c_string(data: bytes, base: int, size: int, index: int) -> str:
    if index < 0 or index >= size:
        return f"<invalid-string-index:{index}>"
    end = data.find(b"\0", base + index, base + size)
    if end < 0:
        return f"<unterminated-string:{index}>"
    return data[base + index : end].decode("utf-8", "replace")


def main() -> int:
    args = parse_args()
    data = args.metadata.read_bytes()
    magic, version = struct.unpack_from("<II", data)
    if (magic, version) != (IL2CPP_MAGIC, IL2CPP_VERSION):
        raise ValueError(
            f"Expected IL2CPP v31 header, got magic=0x{magic:08X}, version={version}"
        )

    pairs = list(struct.iter_unpack("<II", data[8:HEADER_BYTES]))
    string_offset, string_size = pairs[STRING_HEAP_PAIR]
    type_offset, type_size = pairs[TYPE_DEFINITION_PAIR]
    method_offset, method_size = pairs[METHOD_DEFINITION_PAIR]
    parameter_offset, parameter_size = pairs[PARAMETER_DEFINITION_PAIR]
    field_offset, field_size = pairs[FIELD_DEFINITION_PAIR]
    nested_type_offset, nested_type_size = pairs[NESTED_TYPE_PAIR]
    image_offset, image_size = pairs[IMAGE_DEFINITION_PAIR]
    if type_size % TYPE_DEFINITION_BYTES:
        raise ValueError(
            f"Type-definition section size {type_size} is not divisible by {TYPE_DEFINITION_BYTES}"
        )
    if method_size % METHOD_DEFINITION_BYTES:
        raise ValueError(
            f"Method-definition section size {method_size} is not divisible by {METHOD_DEFINITION_BYTES}"
        )
    if parameter_size % PARAMETER_DEFINITION_BYTES:
        raise ValueError("Unexpected parameter-definition section size")
    if field_size % FIELD_DEFINITION_BYTES:
        raise ValueError("Unexpected field-definition section size")
    if nested_type_size % NESTED_TYPE_BYTES:
        raise ValueError("Unexpected nested-type section size")
    if image_size % IMAGE_DEFINITION_BYTES:
        raise ValueError("Unexpected image-definition section size")

    selected = set(args.types or [])
    selected_indexes = set(args.type_indexes or [])
    count = type_size // TYPE_DEFINITION_BYTES
    print(
        f"string_heap=0x{string_offset:X}+{string_size} "
        f"type_definitions=0x{type_offset:X}+{type_size} count={count} "
        f"method_definitions=0x{method_offset:X}+{method_size} "
        f"count={method_size // METHOD_DEFINITION_BYTES}"
    )

    if args.images:
        print(f"IMAGES count={image_size // IMAGE_DEFINITION_BYTES}")
        for image_index in range(image_size // IMAGE_DEFINITION_BYTES):
            image_values = struct.unpack_from(
                "<10I", data, image_offset + image_index * IMAGE_DEFINITION_BYTES
            )
            image_name = read_c_string(
                data, string_offset, string_size, image_values[0]
            )
            signed_image_values = [
                value if value < 0x80000000 else value - 0x100000000
                for value in image_values
            ]
            print(
                f"  IMAGE index={image_index} name={image_name!r} "
                + "dwords="
                + ",".join(
                    f"{i}:{value}" for i, value in enumerate(signed_image_values)
                )
            )

    for index in range(count):
        values = struct.unpack_from(
            "<22I", data, type_offset + index * TYPE_DEFINITION_BYTES
        )
        name = read_c_string(data, string_offset, string_size, values[0])
        namespace = read_c_string(data, string_offset, string_size, values[1])
        if index < args.first or name in selected or index in selected_indexes:
            print(f"TYPE index={index} namespace={namespace!r} name={name!r}")
            signed_values = [
                value if value < 0x80000000 else value - 0x100000000
                for value in values
            ]
            print("dwords=" + ",".join(f"{i}:{value}" for i, value in enumerate(signed_values)))
            method_start = signed_values[9]
            method_count = values[16] & 0xFFFF
            field_start = signed_values[8]
            field_count = values[17] & 0xFFFF
            nested_type_start = signed_values[12]
            nested_type_count = values[18] & 0xFFFF
            print(f"fields start={field_start} count={field_count}")
            for field_index in range(field_start, field_start + field_count):
                field_record = struct.unpack_from(
                    "<3I",
                    data,
                    field_offset + field_index * FIELD_DEFINITION_BYTES,
                )
                field_name = read_c_string(
                    data, string_offset, string_size, field_record[0]
                )
                print(
                    f"  FIELD index={field_index} name={field_name!r} "
                    f"type_index={field_record[1]} token=0x{field_record[2]:08X}"
                )

            print(f"nested_types start={nested_type_start} count={nested_type_count}")
            for nested_index in range(
                nested_type_start, nested_type_start + nested_type_count
            ):
                nested_type = struct.unpack_from(
                    "<I", data, nested_type_offset + nested_index * NESTED_TYPE_BYTES
                )[0]
                nested_values = struct.unpack_from(
                    "<22I", data, type_offset + nested_type * TYPE_DEFINITION_BYTES
                )
                nested_name = read_c_string(
                    data, string_offset, string_size, nested_values[0]
                )
                print(f"  NESTED_TYPE index={nested_type} name={nested_name!r}")

            print(f"methods start={method_start} count={method_count}")
            for method_index in range(method_start, method_start + method_count):
                record = struct.unpack_from(
                    "<7I4H",
                    data,
                    method_offset + method_index * METHOD_DEFINITION_BYTES,
                )
                method_name = read_c_string(
                    data, string_offset, string_size, record[0]
                )
                declaring_type = record[1]
                generic_container = (
                    record[5] if record[5] < 0x80000000 else record[5] - 0x100000000
                )
                print(
                    f"  METHOD index={method_index} name={method_name!r} "
                    f"declaring_type={declaring_type} parameter_start={record[4]} "
                    f"generic_container={generic_container} token=0x{record[6]:08X} "
                    f"flags=0x{record[7]:04X} iflags=0x{record[8]:04X} "
                    f"slot={record[9]} parameter_count={record[10]}"
                )
                parameter_start = record[4]
                parameter_count = record[10]
                for parameter_index in range(
                    parameter_start, parameter_start + parameter_count
                ):
                    parameter_record = struct.unpack_from(
                        "<3I",
                        data,
                        parameter_offset
                        + parameter_index * PARAMETER_DEFINITION_BYTES,
                    )
                    parameter_name = read_c_string(
                        data, string_offset, string_size, parameter_record[0]
                    )
                    print(
                        f"    PARAMETER index={parameter_index} name={parameter_name!r} "
                        f"token=0x{parameter_record[1]:08X} "
                        f"type_index={parameter_record[2]}"
                    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
