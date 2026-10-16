#!/usr/bin/env python3
"""Find common x86-64 RIP-relative references to an ASCII string in a PE file.

This is a read-only research helper. It does not modify the inspected executable.
It intentionally recognizes only the most common single-byte opcodes; reported
addresses are candidates that should be checked with a disassembler.
"""

from __future__ import annotations

import argparse
import struct
from pathlib import Path

import pefile


RIP_RELATIVE_OPCODES = {
    0x03,
    0x0B,
    0x13,
    0x1B,
    0x23,
    0x2B,
    0x33,
    0x3B,
    0x63,
    0x89,
    0x8B,
    0x8D,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pe_file", type=Path)
    parser.add_argument("text", help="ASCII text to locate")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    raw = args.pe_file.read_bytes()
    needle = args.text.encode("ascii")
    pe = pefile.PE(data=raw, fast_load=False)

    offsets: list[int] = []
    start = 0
    while True:
        offset = raw.find(needle, start)
        if offset < 0:
            break
        offsets.append(offset)
        start = offset + 1

    if not offsets:
        print(f"Text not found: {args.text!r}")
        return 1

    text_sections = [
        section
        for section in pe.sections
        if section.Name.rstrip(b"\0") == b".text"
    ]
    if not text_sections:
        print("PE file has no .text section")
        return 1

    section = text_sections[0]
    code = section.get_data()
    code_rva = section.VirtualAddress

    for file_offset in offsets:
        target_rva = pe.get_rva_from_offset(file_offset)
        target_va = pe.OPTIONAL_HEADER.ImageBase + target_rva
        print(
            f"text={args.text!r} file_offset=0x{file_offset:X} "
            f"rva=0x{target_rva:X} "
            f"va=0x{target_va:X}"
        )

        pointer_bytes = struct.pack("<Q", target_va)
        pointer_offsets: list[int] = []
        pointer_start = 0
        while True:
            pointer_offset = raw.find(pointer_bytes, pointer_start)
            if pointer_offset < 0:
                break
            pointer_offsets.append(pointer_offset)
            pointer_start = pointer_offset + 1
        if pointer_offsets:
            pointer_rvas = [pe.get_rva_from_offset(x) for x in pointer_offsets]
            print(
                "absolute_pointer_rvas="
                + ",".join(f"0x{x:X}" for x in pointer_rvas)
            )
        else:
            print("absolute_pointer_rvas=<none>")

        references: list[int] = []
        for index in range(len(code) - 7):
            first = code[index]
            has_rex = 0x40 <= first <= 0x4F
            opcode_index = index + 1 if has_rex else index
            opcode = code[opcode_index]
            if opcode not in RIP_RELATIVE_OPCODES:
                continue

            modrm = code[opcode_index + 1]
            if modrm & 0xC7 != 0x05:
                continue

            instruction_length = (opcode_index - index) + 6
            displacement = struct.unpack_from("<i", code, opcode_index + 2)[0]
            referenced_rva = code_rva + index + instruction_length + displacement
            if referenced_rva == target_rva:
                references.append(code_rva + index)

        if references:
            print("candidate_rip_references=" + ",".join(f"0x{x:X}" for x in references))
        else:
            print("candidate_rip_references=<none>")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
