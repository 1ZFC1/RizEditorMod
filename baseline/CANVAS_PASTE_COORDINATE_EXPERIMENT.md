# 画布事件鼠标粘贴坐标实验

状态：完成；最终相机坐标换算版本已通过使用者运行验收。

## 目的

隔离验证画布事件粘贴的三个变量：目标拍数、目标横坐标和镜像方向。测试不修改原始编辑器或既有谱面。

## 输入

测试目录：`baseline/runtime-artifacts/2026-09-18-canvas-paste-coordinate-01/test-chart/`

- `Canvas Paste Coordinate Test.json` SHA-256：`2A0FBD9A7150FEEF45B41B08A6C832503954451CC7AFB361A90CEBBA5AC0A82F`
- `Canvas Paste Coordinate Test.ogg` SHA-256：`C3D50A34B2A754B33D6F2EB42AAAD06239A194B04526B21B1A2BCB256B24BA2C`

谱面从已经通过运行基线的 `Baseline Sample.json/.ogg` 生成，只把画布 0 的横向事件替换为：

| 拍数 | value |
| ---: | ---: |
| 0 | 0 |
| 2 | -0.2 |
| 3 | 0.2 |

生成命令：

```powershell
python .\tools\Create-CanvasPasteCoordinateTest.py
```

## 操作与预期

1. 打开 `Canvas Paste Coordinate Test.json`。
2. 切换一次“线编辑”，再返回“画布编辑”。
3. 用 `Ctrl+C` 框选第 2–3 拍两个事件。
4. 把播放头放在第 5 拍，鼠标放到第 9 拍、横坐标 0 的细分交点。
5. 按 `Ctrl+V`：预期生成 `9/0` 和 `10/0.4`。
6. 撤销后，在相同鼠标位置按 `Ctrl+Shift+V`：预期生成 `9/0` 和 `10/-0.4`。

普通和镜像粘贴的最早事件都必须位于第 9 拍、横坐标 0；两次操作应各自只占一步 Undo/Redo。

## 证据

首次运行在鼠标第 9 拍、横坐标 0 时记录 `beat=9`、`canvasValue=0.2`，证明插件错误叠加了测试谱面保持的画布值 `0.2`。移除该换算后，`.Value` 仍丢失横向分量；改用 `.value` / `.hasValue` 后又出现随机 `(0,0)`，证明整个 `Nullable<Vector2>` 互操作路径不可靠。当前修正版完全绕开这些字段，直接从屏幕坐标和原相机重建世界坐标，再按反汇编确认的 `InputControl.GetMousePosition` 与 `CanvasEditor.Update` 公式换算拍数和 value。

每次粘贴会在 `BepInEx/LogOutput.log` 写入目标拍、目标 `canvasValue`、屏幕坐标、相机世界坐标及两轴吸附前数值。运行后把观察结果及最终保存文件补充到本目录。

## 结果

2026-09-18，使用者在 working 副本确认最终版本“一切正常”。这确认普通粘贴能把最早事件的拍数和横坐标对齐鼠标，镜像粘贴能在同一锚点翻转横向相对偏移；此前出现的固定 `(0,0)`、随机节拍和错误横向偏移均未再出现。

最终实现没有读取不稳定的 `EditorInput Nullable<Vector2>`，也没有修改测试谱面之外的原始程序文件。自动测试 13/13 通过，插件编译为 0 警告、0 错误。
