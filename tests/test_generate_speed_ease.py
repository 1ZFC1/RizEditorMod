from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "tools" / "Generate-SpeedEase.py"
SPEC = importlib.util.spec_from_file_location("generate_speed_ease", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def make_chart() -> dict:
    return {
        "fileVersion": 0,
        "chartDelayMs": 0,
        "themes": [],
        "challengeTimes": [],
        "bPM": 105.0,
        "bpmShifts": [
            {"time": 0.0, "value": 1.0, "easeType": 0, "floorPosition": 0.0}
        ],
        "lines": [
            {
                "linePoints": [
                    {
                        "time": 8.0,
                        "xPosition": 0.0,
                        "color": {"r": 0, "g": 0, "b": 0, "a": 255},
                        "easeType": 0,
                        "canvasIndex": 0,
                        "floorPosition": 0.0,
                    },
                    {
                        "time": 18.0,
                        "xPosition": 0.0,
                        "color": {"r": 0, "g": 0, "b": 0, "a": 255},
                        "easeType": 0,
                        "canvasIndex": 0,
                        "floorPosition": 0.0,
                    },
                ],
                "notes": [
                    {
                        "type": 2,
                        "time": 8.0,
                        "floorPosition": 0.0,
                        "otherInformations": [18.0, 0.0, 0.0],
                    }
                ],
                "judgeRingColor": [],
                "lineColor": [],
            }
        ],
        "canvasMoves": [
            {
                "index": 0,
                "xPositionKeyPoints": [
                    {"time": 0.0, "value": 0.0, "easeType": 0, "floorPosition": 0.0}
                ],
                "speedKeyPoints": [
                    {"time": 0.0, "value": 1.0, "easeType": 0, "floorPosition": 0.0}
                ],
            }
        ],
        "cameraMove": {"scaleKeyPoints": [], "xPositionKeyPoints": []},
        "__db": [],
    }


class SpeedEaseTests(unittest.TestCase):
    def test_in_out_quad_expands_steps_and_recalculates_positions(self) -> None:
        source = make_chart()
        transformed = MODULE.expand_speed_ease(
            source,
            canvas_index=0,
            start_beat=8.0,
            end_beat=16.0,
            start_speed=1.0,
            end_speed=3.0,
            easing_name="in-out-quad",
            steps=4,
        )

        self.assertEqual(source["canvasMoves"][0]["speedKeyPoints"], [
            {"time": 0.0, "value": 1.0, "easeType": 0, "floorPosition": 0.0}
        ])
        points = transformed["canvasMoves"][0]["speedKeyPoints"]
        self.assertEqual([point["time"] for point in points], [0.0, 8.0, 10.0, 12.0, 14.0, 16.0])
        self.assertEqual([point["value"] for point in points], [1.0, 1.0, 1.25, 2.0, 2.75, 3.0])
        self.assertAlmostEqual(points[-1]["floorPosition"], 12.571428571, places=8)
        self.assertEqual(transformed["lines"][0]["linePoints"][1]["floorPosition"], 16.0)
        self.assertEqual(transformed["lines"][0]["notes"][0]["otherInformations"][2], 16.0)

    def test_rejects_existing_point_inside_interval(self) -> None:
        chart = make_chart()
        chart["canvasMoves"][0]["speedKeyPoints"].append(
            {"time": 12.0, "value": 2.0, "easeType": 0, "floorPosition": 0.0}
        )
        with self.assertRaisesRegex(MODULE.ChartValidationError, "lies inside"):
            MODULE.expand_speed_ease(
                chart,
                canvas_index=0,
                start_beat=8.0,
                end_beat=16.0,
                start_speed=1.0,
                end_speed=3.0,
                easing_name="linear",
                steps=4,
            )

    def test_cli_refuses_to_overwrite_output(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.json"
            output = root / "output.json"
            source.write_text(json.dumps(make_chart()), encoding="utf-8")
            output.write_text("keep", encoding="utf-8")
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    str(source),
                    str(output),
                    "--start-beat",
                    "8",
                    "--end-beat",
                    "16",
                    "--start-speed",
                    "1",
                    "--end-speed",
                    "3",
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("output already exists", completed.stderr)
            self.assertEqual(output.read_text(encoding="utf-8"), "keep")

    def test_cli_creates_a_separate_output(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source.json"
            output = root / "output.json"
            source.write_text(json.dumps(make_chart()), encoding="utf-8")
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    str(source),
                    str(output),
                    "--start-beat",
                    "8",
                    "--end-beat",
                    "16",
                    "--start-speed",
                    "1",
                    "--end-speed",
                    "3",
                    "--steps",
                    "4",
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertTrue(output.is_file())
            self.assertEqual(json.loads(source.read_text(encoding="utf-8")), make_chart())
            generated = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(len(generated["canvasMoves"][0]["speedKeyPoints"]), 6)


if __name__ == "__main__":
    unittest.main()
