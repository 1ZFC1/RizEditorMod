#!/usr/bin/env python3
"""Find candidate direct x86-64 calls in an IL2CPP method.

The byte scan reports E8/E9 targets and labels a target only when it exactly
matches a method pointer from a recovered code-generation module. It is not a
full instruction decoder: important results must be checked in a disassembler.
Indirect calls, generic instantiations, and shared-method thunks need separate
analysis.
"""

from __future__ import annotations

import argparse
import struct
from collections import defaultdict
from pathlib import Path

import pefile


HEADER_BYTES = 0x100
STRING_HEAP_PAIR = 2
METHOD_DEFINITION_PAIR = 5
TYPE_DEFINITION_PAIR = 19
IMAGE_DEFINITION_PAIR = 20
METHOD_BYTES = 36
TYPE_BYTES = 88
IMAGE_BYTES = 40
MAX_LABELS_PER_TARGET = 6


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_assembly", type=Path)
    parser.add_argument("metadata", type=Path)
    parser.add_argument("module_name")
    parser.add_argument("token", type=lambda value: int(value, 0))
    return parser.parse_args()


def find_all(data: bytes, needle: bytes) -> list[int]:
    result: list[int] = []
    start = 0
    while True:
        offset = data.find(needle, start)
        if offset < 0:
            return result
        result.append(offset)
        start = offset + 1


def read_c_string(data: bytes, base: int, size: int, index: int) -> str:
    end = data.find(b"\0", base + index, base + size)
    return data[base + index : end].decode("utf-8", "replace")


def find_codegen_module(
    raw: bytes, pe: pefile.PE, module_name: str
) -> tuple[int, int, int] | None:
    image_base = pe.OPTIONAL_HEADER.ImageBase
    candidates: list[tuple[int, int, int]] = []
    for string_offset in find_all(raw, module_name.encode("ascii") + b"\0"):
        string_va = image_base + pe.get_rva_from_offset(string_offset)
        for pointer_offset in find_all(raw, struct.pack("<Q", string_va)):
            if pointer_offset + 24 > len(raw):
                continue
            count = struct.unpack_from("<I", raw, pointer_offset + 8)[0]
            table_va = struct.unpack_from("<Q", raw, pointer_offset + 16)[0]
            table_rva = table_va - image_base
            if count and 0 <= table_rva < pe.OPTIONAL_HEADER.SizeOfImage:
                candidates.append(
                    (pe.get_rva_from_offset(pointer_offset), count, table_rva)
                )
    candidates = sorted(set(candidates))
    if len(candidates) == 1:
        return candidates[0]
    return None


def main() -> int:
    args = parse_args()
    raw = args.game_assembly.read_bytes()
    pe = pefile.PE(data=raw, fast_load=False)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    metadata = args.metadata.read_bytes()
    pairs = list(struct.iter_unpack("<II", metadata[8:HEADER_BYTES]))
    string_offset, string_size = pairs[STRING_HEAP_PAIR]
    method_offset, method_size = pairs[METHOD_DEFINITION_PAIR]
    type_offset, type_size = pairs[TYPE_DEFINITION_PAIR]
    image_offset, image_size = pairs[IMAGE_DEFINITION_PAIR]

    address_labels: dict[int, list[str]] = defaultdict(list)
    token_addresses: dict[tuple[str, int], int] = {}

    for image_index in range(image_size // IMAGE_BYTES):
        image_record = struct.unpack_from(
            "<10I", metadata, image_offset + image_index * IMAGE_BYTES
        )
        image_name = read_c_string(
            metadata, string_offset, string_size, image_record[0]
        )
        type_start = image_record[2]
        type_count = image_record[3]
        module = find_codegen_module(raw, pe, image_name)
        if module is None:
            continue
        _, method_pointer_count, method_table_rva = module
        method_table_offset = pe.get_offset_from_rva(method_table_rva)

        for type_index in range(type_start, type_start + type_count):
            type_record = struct.unpack_from(
                "<22I", metadata, type_offset + type_index * TYPE_BYTES
            )
            type_name = read_c_string(
                metadata, string_offset, string_size, type_record[0]
            )
            namespace = read_c_string(
                metadata, string_offset, string_size, type_record[1]
            )
            method_start = type_record[9]
            method_count = type_record[16] & 0xFFFF
            full_type = f"{namespace}.{type_name}" if namespace else type_name

            for method_index in range(method_start, method_start + method_count):
                method_record = struct.unpack_from(
                    "<7I4H", metadata, method_offset + method_index * METHOD_BYTES
                )
                method_name = read_c_string(
                    metadata, string_offset, string_size, method_record[0]
                )
                token = method_record[6]
                row = token & 0x00FFFFFF
                if token >> 24 != 0x06 or row == 0 or row > method_pointer_count:
                    continue
                method_va = struct.unpack_from(
                    "<Q", raw, method_table_offset + (row - 1) * 8
                )[0]
                if not method_va:
                    continue
                method_rva = method_va - image_base
                label = f"{image_name}!{full_type}.{method_name} [0x{token:08X}]"
                address_labels[method_rva].append(label)
                token_addresses[(image_name, token)] = method_rva

    target_key = (args.module_name, args.token)
    if target_key not in token_addresses:
        raise ValueError(
            f"Could not resolve {args.module_name} token 0x{args.token:08X}"
        )
    target_rva = token_addresses[target_key]
    all_method_rvas = sorted(address_labels)
    next_rva = next((value for value in all_method_rvas if value > target_rva), 0)
    if not next_rva:
        raise ValueError("Could not determine an upper bound for the target method")
    target_offset = pe.get_offset_from_rva(target_rva)
    code = raw[target_offset : target_offset + next_rva - target_rva]

    print(
        f"target_rva=0x{target_rva:X} next_method_rva=0x{next_rva:X} "
        f"bytes={len(code)}"
    )
    for label in address_labels[target_rva]:
        print(f"TARGET {label}")

    direct_count = 0
    managed_count = 0
    for index in range(len(code) - 4):
        opcode = code[index]
        if opcode not in (0xE8, 0xE9):
            continue
        displacement = struct.unpack_from("<i", code, index + 1)[0]
        source_rva = target_rva + index
        destination_rva = source_rva + 5 + displacement
        if not 0 <= destination_rva < pe.OPTIONAL_HEADER.SizeOfImage:
            continue
        direct_count += 1
        kind = "CALL" if opcode == 0xE8 else "JUMP"
        labels = address_labels.get(destination_rva, [])
        if labels:
            managed_count += 1
            print(
                f"{kind} source_rva=0x{source_rva:X} "
                f"target_rva=0x{destination_rva:X}"
            )
            if len(labels) > MAX_LABELS_PER_TARGET:
                print(
                    f"  MANAGED_SHARED candidates={len(labels)} labels_omitted=yes"
                )
            else:
                for label in labels:
                    print(f"  MANAGED {label}")

    print(f"direct_branches_scanned={direct_count} managed_targets={managed_count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
