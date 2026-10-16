#!/usr/bin/env python3
"""Create the isolated chart used to verify canvas-event mouse paste coordinates."""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "baseline" / "runtime-artifacts" / "2026-08-27-baseline-01" / "test-chart"
OUTPUT_DIR = ROOT / "baseline" / "runtime-artifacts" / "2026-09-18-canvas-paste-coordinate-01" / "test-chart"
OUTPUT_STEM = "Canvas Paste Coordinate Test"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def main() -> None:
    source_chart = SOURCE_DIR / "Baseline Sample.json"
    source_audio = SOURCE_DIR / "Baseline Sample.ogg"
    if not source_chart.is_file() or not source_audio.is_file():
        raise SystemExit("The established runtime baseline chart or audio is missing.")

    chart = json.loads(source_chart.read_text(encoding="utf-8-sig"))
    canvas = next(move for move in chart["canvasMoves"] if move["index"] == 0)
    canvas["xPositionKeyPoints"] = [
        {"time": 0.0, "value": 0.0, "easeType": 0, "floorPosition": 0.0},
        {"time": 2.0, "value": -0.2, "easeType": 0, "floorPosition": 0.0},
        {"time": 3.0, "value": 0.2, "easeType": 0, "floorPosition": 0.0},
    ]

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    output_chart = OUTPUT_DIR / f"{OUTPUT_STEM}.json"
    output_audio = OUTPUT_DIR / f"{OUTPUT_STEM}.ogg"
    output_chart.write_text(
        json.dumps(chart, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    shutil.copyfile(source_audio, output_audio)

    print(f"chart={output_chart}")
    print(f"chart_sha256={sha256(output_chart)}")
    print(f"audio={output_audio}")
    print(f"audio_sha256={sha256(output_audio)}")


if __name__ == "__main__":
    main()
