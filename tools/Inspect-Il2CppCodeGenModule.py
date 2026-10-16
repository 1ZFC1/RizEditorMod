#!/usr/bin/env python3
"""Resolve IL2CPP method tokens through a PE's code-generation module table."""

from __future__ import annotations

import argparse
import struct
from pathlib import Path

import pefile


def parse_token(text: str) -> int:
    return int(text, 0)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_assembly", type=Path)
    parser.add_argument("module_name")
    parser.add_argument("tokens", nargs="+", type=parse_token)
    return parser.parse_args()


def find_all(data: bytes, needle: bytes) -> list[int]:
    offsets: list[int] = []
    start = 0
    while True:
        offset = data.find(needle, start)
        if offset < 0:
            return offsets
        offsets.append(offset)
        start = offset + 1


def main() -> int:
    args = parse_args()
    raw = args.game_assembly.read_bytes()
    pe = pefile.PE(data=raw, fast_load=False)
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]]
    )
    image_base = pe.OPTIONAL_HEADER.ImageBase
    module_bytes = args.module_name.encode("ascii") + b"\0"
    module_offsets = find_all(raw, module_bytes)
    if len(module_offsets) != 1:
        raise ValueError(
            f"Expected one {args.module_name!r} string, found {len(module_offsets)}"
        )

    module_name_rva = pe.get_rva_from_offset(module_offsets[0])
    module_name_va = image_base + module_name_rva
    pointer_offsets = find_all(raw, struct.pack("<Q", module_name_va))
    candidates: list[tuple[int, int, int]] = []
    for pointer_offset in pointer_offsets:
        if pointer_offset + 24 > len(raw):
            continue
        method_count = struct.unpack_from("<I", raw, pointer_offset + 8)[0]
        method_table_va = struct.unpack_from("<Q", raw, pointer_offset + 16)[0]
        method_table_rva = method_table_va - image_base
        if method_count and 0 <= method_table_rva < pe.OPTIONAL_HEADER.SizeOfImage:
            candidates.append(
                (pe.get_rva_from_offset(pointer_offset), method_count, method_table_rva)
            )

    if len(candidates) != 1:
        raise ValueError(
            f"Expected one plausible code-generation module, found {len(candidates)}"
        )

    module_rva, method_count, method_table_rva = candidates[0]
    method_table_offset = pe.get_offset_from_rva(method_table_rva)
    print(
        f"module={args.module_name!r} module_rva=0x{module_rva:X} "
        f"method_count={method_count} method_table_rva=0x{method_table_rva:X}"
    )

    module_method_rvas = []
    for method_index in range(method_count):
        pointer = struct.unpack_from(
            "<Q", raw, method_table_offset + method_index * 8
        )[0]
        if pointer:
            module_method_rvas.append(pointer - image_base)
    sorted_method_rvas = sorted(set(module_method_rvas))

    for token in args.tokens:
        table = token >> 24
        row = token & 0x00FFFFFF
        if table != 0x06 or row == 0:
            raise ValueError(f"Token 0x{token:08X} is not a MethodDef token")
        index = row - 1
        if index >= method_count:
            raise ValueError(
                f"Token 0x{token:08X} index {index} exceeds module method count {method_count}"
            )
        method_va = struct.unpack_from("<Q", raw, method_table_offset + index * 8)[0]
        method_rva = method_va - image_base if method_va else 0
        function_end_rva = 0
        for runtime_function in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []):
            begin = runtime_function.struct.BeginAddress
            end = runtime_function.struct.EndAddress
            if begin <= method_rva < end:
                function_end_rva = end
                break
        next_method_rva = next(
            (candidate for candidate in sorted_method_rvas if candidate > method_rva),
            0,
        )
        print(
            f"token=0x{token:08X} index={index} "
            f"method_va=0x{method_va:X} method_rva=0x{method_rva:X} "
            f"function_end_rva=0x{function_end_rva:X} "
            f"next_method_rva=0x{next_method_rva:X}"
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
