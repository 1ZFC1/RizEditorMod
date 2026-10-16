#!/usr/bin/env python3
"""Extract the authenticated plaintext payload from Rizline's IL2CPP metadata.

The editor wraps its metadata in a small custom container and decrypts the
payload with AES-256-GCM before IL2CPP parses it. The AES expanded-key table is
read from the matching GameAssembly.dll at runtime; no key is stored here.

The primary output is still Rizline's permuted metadata layout. An optional
second output reconstructs the conventional IL2CPP v31 header and can be used
with standard metadata-aware research tools.
"""

from __future__ import annotations

import argparse
import struct
from pathlib import Path

import pefile
from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives.ciphers.aead import AESGCM


KEY_TABLE_RVA = 0x1A6B230
AES_BLOCK_BYTES = 16
AES_256_ROUND_KEY_COUNT = 15
EXPANDED_KEY_BYTES = AES_BLOCK_BYTES * AES_256_ROUND_KEY_COUNT
WRAPPER_HEADER_BYTES = 8
GCM_NONCE_BYTES = 12
GCM_TAG_BYTES = 16
PERMUTED_HEADER_DWORDS = 31
PERMUTED_HEADER_BYTES = PERMUTED_HEADER_DWORDS * 4
STANDARD_HEADER_BYTES = 0x100
IL2CPP_MAGIC = 0xFAB11BAF
IL2CPP_VERSION = 31

# For each conventional header pair, gives the source index of its size in
# Rizline's 31-dword permuted header.
SIZE_SOURCE_INDICES = (
    28,
    21,
    6,
    18,
    1,
    27,
    11,
    15,
    8,
    2,
    12,
    30,
    19,
    26,
    20,
    5,
    24,
    22,
    4,
    13,
    25,
    16,
    9,
    23,
    17,
    14,
    3,
    0,
    29,
    10,
    7,
)

# Order in which the corresponding section bodies appear after Rizline's
# compact header. Values are conventional header-pair indices.
BODY_PAIR_ORDER = (
    13,
    14,
    15,
    16,
    0,
    11,
    29,
    25,
    17,
    1,
    10,
    24,
    3,
    4,
    30,
    22,
    19,
    8,
    18,
    2,
    23,
    28,
    20,
    26,
    5,
    9,
    6,
    27,
    7,
    12,
    21,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_assembly", type=Path)
    parser.add_argument("metadata", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--standard-output",
        type=Path,
        help="also write a conventional IL2CPP v31 global-metadata.dat",
    )
    return parser.parse_args()


def extract_aes_key(game_assembly: Path) -> bytes:
    pe = pefile.PE(str(game_assembly), fast_load=False)
    transposed = pe.get_data(KEY_TABLE_RVA, EXPANDED_KEY_BYTES)
    if len(transposed) != EXPANDED_KEY_BYTES:
        raise ValueError("Could not read the complete AES expanded-key table")

    expanded = bytes(
        transposed[(index % AES_BLOCK_BYTES) * AES_256_ROUND_KEY_COUNT + index // AES_BLOCK_BYTES]
        for index in range(EXPANDED_KEY_BYTES)
    )
    return expanded[:32]


def decrypt_payload(game_assembly: Path, metadata: Path) -> tuple[bytes, dict[str, int]]:
    raw = metadata.read_bytes()
    if len(raw) < WRAPPER_HEADER_BYTES + GCM_NONCE_BYTES + GCM_TAG_BYTES:
        raise ValueError("Metadata wrapper is too short")

    marker, declared_length = struct.unpack_from("<II", raw)
    payload_end = WRAPPER_HEADER_BYTES + declared_length
    if payload_end > len(raw):
        raise ValueError(
            f"Declared payload length {declared_length} exceeds file size {len(raw)}"
        )

    wrapped = raw[WRAPPER_HEADER_BYTES:payload_end]
    nonce = wrapped[:GCM_NONCE_BYTES]
    ciphertext_and_tag = wrapped[GCM_NONCE_BYTES:]
    key = extract_aes_key(game_assembly)

    try:
        plaintext = AESGCM(key).decrypt(nonce, ciphertext_and_tag, None)
    except InvalidTag as error:
        raise ValueError(
            "AES-GCM authentication failed; the DLL and metadata may be from different builds"
        ) from error

    facts = {
        "wrapper_marker": marker,
        "declared_payload_bytes": declared_length,
        "ignored_trailing_bytes": len(raw) - payload_end,
        "plaintext_bytes": len(plaintext),
    }
    return plaintext, facts


def rebuild_standard_metadata(plaintext: bytes) -> bytes:
    if len(plaintext) < PERMUTED_HEADER_BYTES:
        raise ValueError("Decrypted metadata is shorter than its permuted header")

    source_sizes = struct.unpack_from(f"<{PERMUTED_HEADER_DWORDS}I", plaintext)
    body = plaintext[PERMUTED_HEADER_BYTES:]
    if sum(source_sizes) != len(body):
        raise ValueError(
            f"Section sizes total {sum(source_sizes)}, but body has {len(body)} bytes"
        )

    sizes = [source_sizes[index] for index in SIZE_SOURCE_INDICES]
    header = bytearray(STANDARD_HEADER_BYTES)
    struct.pack_into("<II", header, 0, IL2CPP_MAGIC, IL2CPP_VERSION)

    cursor = STANDARD_HEADER_BYTES
    for pair_index in BODY_PAIR_ORDER:
        pair_offset = 8 + pair_index * 8
        struct.pack_into("<II", header, pair_offset, cursor, sizes[pair_index])
        cursor += sizes[pair_index]

    standard = bytes(header) + body
    if cursor != len(standard):
        raise ValueError(
            f"Rebuilt section end {cursor} does not match file length {len(standard)}"
        )
    return standard


def main() -> int:
    args = parse_args()
    plaintext, facts = decrypt_payload(args.game_assembly, args.metadata)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(plaintext)

    print(f"output={args.output}")
    print(f"wrapper_marker=0x{facts['wrapper_marker']:08X}")
    print(f"declared_payload_bytes={facts['declared_payload_bytes']}")
    print(f"ignored_trailing_bytes={facts['ignored_trailing_bytes']}")
    print(f"plaintext_bytes={facts['plaintext_bytes']}")
    print(f"plaintext_head={plaintext[:16].hex()}")

    if args.standard_output is not None:
        standard = rebuild_standard_metadata(plaintext)
        args.standard_output.parent.mkdir(parents=True, exist_ok=True)
        args.standard_output.write_bytes(standard)
        print(f"standard_output={args.standard_output}")
        print(f"standard_bytes={len(standard)}")
        print(f"standard_head={standard[:16].hex()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
