#!/usr/bin/env python3
"""Expand one canvas-speed transition into ordinary step-speed key points.

This is a deliberately conservative prototype.  It never overwrites the input
file and currently accepts only a one-canvas chart with a constant base BPM.
Those limits keep every recalculated ``floorPosition`` inside behavior already
confirmed by the project's chart-format experiments.
"""

from __future__ import annotations

import argparse
import copy
import json
import math
from pathlib import Path
from typing import Any, Callable


EPSILON = 1e-7


class ChartValidationError(ValueError):
    """The chart is outside the prototype's currently verified safe subset."""


def linear(progress: float) -> float:
    return progress


def in_quad(progress: float) -> float:
    return progress * progress


def out_quad(progress: float) -> float:
    return 1.0 - (1.0 - progress) ** 2


def in_out_quad(progress: float) -> float:
    if progress < 0.5:
        return 2.0 * progress * progress
    return 1.0 - ((-2.0 * progress + 2.0) ** 2) / 2.0


EASINGS: dict[str, Callable[[float], float]] = {
    "linear": linear,
    "in-quad": in_quad,
    "out-quad": out_quad,
    "in-out-quad": in_out_quad,
}


def close(left: float, right: float) -> bool:
    return math.isclose(left, right, rel_tol=0.0, abs_tol=EPSILON)


def rounded(value: float) -> float:
    result = round(value, 9)
    return 0.0 if result == 0 else result


def require_number(value: Any, path: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ChartValidationError(f"{path} must be a number")
    result = float(value)
    if not math.isfinite(result):
        raise ChartValidationError(f"{path} must be finite")
    return result


def validate_safe_subset(chart: dict[str, Any], canvas_index: int) -> dict[str, Any]:
    bpm = require_number(chart.get("bPM"), "bPM")
    if bpm <= 0:
        raise ChartValidationError("bPM must be greater than zero")

    bpm_shifts = chart.get("bpmShifts")
    if not isinstance(bpm_shifts, list) or len(bpm_shifts) != 1:
        raise ChartValidationError(
            "prototype only supports one bpmShifts entry (constant BPM)"
        )
    shift = bpm_shifts[0]
    if not isinstance(shift, dict):
        raise ChartValidationError("bpmShifts[0] must be an object")
    if not close(require_number(shift.get("time"), "bpmShifts[0].time"), 0.0):
        raise ChartValidationError("prototype requires bpmShifts[0].time == 0")
    if not close(require_number(shift.get("value"), "bpmShifts[0].value"), 1.0):
        raise ChartValidationError("prototype requires bpmShifts[0].value == 1")

    canvases = chart.get("canvasMoves")
    if not isinstance(canvases, list) or len(canvases) != 1:
        raise ChartValidationError("prototype only supports charts with one canvas")
    canvas = canvases[0]
    if not isinstance(canvas, dict) or int(canvas.get("index", -1)) != canvas_index:
        raise ChartValidationError(f"the only canvas must have index {canvas_index}")

    speed_points = canvas.get("speedKeyPoints")
    if not isinstance(speed_points, list) or not speed_points:
        raise ChartValidationError("canvas speedKeyPoints must be a non-empty array")
    previous_time: float | None = None
    for index, point in enumerate(speed_points):
        if not isinstance(point, dict):
            raise ChartValidationError(f"speedKeyPoints[{index}] must be an object")
        point_time = require_number(point.get("time"), f"speedKeyPoints[{index}].time")
        require_number(point.get("value"), f"speedKeyPoints[{index}].value")
        if point_time < 0:
            raise ChartValidationError("prototype does not yet support negative speed times")
        if previous_time is not None and point_time <= previous_time + EPSILON:
            raise ChartValidationError(
                "speedKeyPoints must already be strictly time-ordered; the tool will not sort them"
            )
        previous_time = point_time

    lines = chart.get("lines")
    if not isinstance(lines, list):
        raise ChartValidationError("lines must be an array")
    for line_index, line in enumerate(lines):
        if not isinstance(line, dict):
            raise ChartValidationError(f"lines[{line_index}] must be an object")
        for point_index, point in enumerate(line.get("linePoints", [])):
            if int(point.get("canvasIndex", -1)) != canvas_index:
                raise ChartValidationError(
                    f"lines[{line_index}].linePoints[{point_index}] references another canvas"
                )

    return canvas


def make_endpoint(
    existing: dict[str, Any] | None,
    time: float,
    value: float,
) -> dict[str, Any]:
    point = copy.deepcopy(existing) if existing is not None else {}
    point["time"] = rounded(time)
    point["value"] = rounded(value)
    point["easeType"] = 0
    point["floorPosition"] = 0.0
    return point


def expand_speed_ease(
    chart: dict[str, Any],
    *,
    canvas_index: int,
    start_beat: float,
    end_beat: float,
    start_speed: float,
    end_speed: float,
    easing_name: str,
    steps: int,
) -> dict[str, Any]:
    """Return a transformed deep copy of *chart*."""

    if not all(math.isfinite(value) for value in (start_beat, end_beat, start_speed, end_speed)):
        raise ChartValidationError("beats and speeds must be finite")
    if start_beat < 0 or end_beat <= start_beat:
        raise ChartValidationError("require 0 <= start beat < end beat")
    if steps < 2 or steps > 4096:
        raise ChartValidationError("steps must be between 2 and 4096")
    if easing_name not in EASINGS:
        raise ChartValidationError(f"unknown easing: {easing_name}")

    result = copy.deepcopy(chart)
    canvas = validate_safe_subset(result, canvas_index)
    speed_points: list[dict[str, Any]] = canvas["speedKeyPoints"]

    existing_start: dict[str, Any] | None = None
    existing_end: dict[str, Any] | None = None
    prefix: list[dict[str, Any]] = []
    suffix: list[dict[str, Any]] = []
    for point in speed_points:
        point_time = float(point["time"])
        if close(point_time, start_beat):
            existing_start = point
        elif close(point_time, end_beat):
            existing_end = point
        elif start_beat < point_time < end_beat:
            raise ChartValidationError(
                f"existing speed point at beat {point_time:g} lies inside the requested interval"
            )
        elif point_time < start_beat:
            prefix.append(point)
        else:
            suffix.append(point)

    if existing_start is not None and not close(float(existing_start["value"]), start_speed):
        raise ChartValidationError("existing start-point speed differs from --start-speed")
    if existing_end is not None and not close(float(existing_end["value"]), end_speed):
        raise ChartValidationError("existing end-point speed differs from --end-speed")

    easing = EASINGS[easing_name]
    generated: list[dict[str, Any]] = []
    duration = end_beat - start_beat
    for step in range(steps + 1):
        progress = step / steps
        point_time = start_beat + duration * progress
        point_value = start_speed + (end_speed - start_speed) * easing(progress)
        existing = existing_start if step == 0 else existing_end if step == steps else None
        generated.append(make_endpoint(existing, point_time, point_value))

    canvas["speedKeyPoints"] = prefix + generated + suffix
    recalculate_floor_positions(result, canvas)
    return result


def floor_position(speed_points: list[dict[str, Any]], beat: float, bpm: float) -> float:
    position = 0.0
    previous_beat = 0.0
    previous_speed = 1.0
    for point in speed_points:
        point_beat = float(point["time"])
        if point_beat > beat:
            break
        position += (point_beat - previous_beat) * 60.0 / bpm * previous_speed
        previous_beat = point_beat
        previous_speed = float(point["value"])
    position += (beat - previous_beat) * 60.0 / bpm * previous_speed
    return rounded(position)


def recalculate_floor_positions(chart: dict[str, Any], canvas: dict[str, Any]) -> None:
    bpm = float(chart["bPM"])
    speed_points: list[dict[str, Any]] = canvas["speedKeyPoints"]

    for point in speed_points:
        point["floorPosition"] = floor_position(speed_points, float(point["time"]), bpm)

    for line in chart["lines"]:
        for point in line.get("linePoints", []):
            point["floorPosition"] = floor_position(speed_points, float(point["time"]), bpm)
        for note in line.get("notes", []):
            note["floorPosition"] = floor_position(speed_points, float(note["time"]), bpm)
            if int(note.get("type", -1)) == 2:
                other = note.get("otherInformations")
                if not isinstance(other, list) or len(other) < 3:
                    raise ChartValidationError("Hold otherInformations must contain three values")
                other_canvas = int(float(other[1]))
                if other_canvas != int(canvas["index"]):
                    raise ChartValidationError("Hold end references another canvas")
                other[2] = floor_position(speed_points, float(other[0]), bpm)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="source chart JSON (never modified)")
    parser.add_argument("output", type=Path, help="new chart JSON path")
    parser.add_argument("--canvas", type=int, default=0, help="canvas index; default: 0")
    parser.add_argument("--start-beat", type=float, required=True)
    parser.add_argument("--end-beat", type=float, required=True)
    parser.add_argument("--start-speed", type=float, required=True)
    parser.add_argument("--end-speed", type=float, required=True)
    parser.add_argument("--ease", choices=sorted(EASINGS), default="in-out-quad")
    parser.add_argument(
        "--steps",
        type=int,
        default=32,
        help="number of constant-speed intervals; inserts steps-1 intermediate points",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    input_path = args.input.resolve()
    output_path = args.output.resolve()
    if input_path == output_path:
        raise SystemExit("error: output must differ from input; input is never overwritten")
    if output_path.exists():
        raise SystemExit(f"error: output already exists: {output_path}")

    try:
        with input_path.open("r", encoding="utf-8-sig") as stream:
            chart = json.load(stream)
        transformed = expand_speed_ease(
            chart,
            canvas_index=args.canvas,
            start_beat=args.start_beat,
            end_beat=args.end_beat,
            start_speed=args.start_speed,
            end_speed=args.end_speed,
            easing_name=args.ease,
            steps=args.steps,
        )
    except (OSError, json.JSONDecodeError, ChartValidationError) as error:
        raise SystemExit(f"error: {error}") from error

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(transformed, stream, ensure_ascii=False, indent=2)
        stream.write("\n")

    speed_count = len(transformed["canvasMoves"][0]["speedKeyPoints"])
    print(f"created: {output_path}")
    print(f"easing: {args.ease}; intervals: {args.steps}; total speed points: {speed_count}")
    print("input was not modified")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
