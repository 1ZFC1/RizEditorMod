from pathlib import Path

data = Path("research/generated/global-metadata.standard.dat").read_bytes()
needles = [
    b"lineWidth",
    b"LineWidth",
    b"widthMultiplier",
    b"previewLine",
    b"PreviewLine",
    b"lineRenderer",
    b"LineRenderer",
    b"subLine",
    b"SubLine",
    b"ToolType",
    b"currentTool",
    b"SelectedTool",
    b"editingTool",
    b"DrawLine",
    b"lineTool",
    b"colorPanels",
    b"SyncColorPanels",
]
for n in needles:
    i = data.find(n)
    print(n.decode(), i)
