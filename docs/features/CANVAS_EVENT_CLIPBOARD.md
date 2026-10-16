# 画布事件剪贴板规格

状态：已实现，并于 2026-09-18 通过使用者 working 副本运行验收。

## 范围

本功能只处理当前聚焦画布的 `xPositionKeyPoints`。流速节点、相机事件、线、Note 和其他画布不在一次操作的目标范围内。

每个剪贴板事件完整保存：

- `time`
- `value`
- `easeType`
- `floorPosition`

插件不改变原有缓动类型，也不增加 JSON 字段。

## 选择和快捷键

- `Ctrl+C`：调用 `LineEditor.SelectLines`，拖动原矩形后复制。
- `Ctrl+X`：调用同一矩形后剪切。
- `Ctrl+D`：框选后只删除，不写入软件剪贴板。
- `Ctrl+V`：将最早事件的 `(x, time)` 对齐原编辑器提供的吸附后鼠标位置，其余事件按相同的 x/time 偏移量平移。
- `Ctrl+Shift+V`：最早事件仍对齐鼠标；其余事件保持相对拍距，并把相对最早事件的 x 偏移取反，实现左右镜像。

矩形横向范围不参与选择；只使用上下端点对应拍数形成闭区间。每次启动后，首次框选仍需先进入一次“线编辑”标签初始化原框选组件。

鼠标读取不再访问 `EditorInput` 的四个 `Nullable<Vector2>` 字段：运行验证表明 `.Value` 会丢失一个分量，而 `.value` / `.hasValue` 也会随机读到全零临时副本。插件现在从与原生 `InputControl.GetMousePosition` 相同的源数据重新计算：读取 `Input.mousePosition`，经 `InputControl._camera.ScreenToWorldPoint` 得到世界坐标；横坐标按 `CanvasEditor.Update` 使用的 `5.5625` 比例和当前水平细分吸附；拍数按原生 `worldY + 2.4`、编辑器垂直缩放、`ProgressControl.RealTimeRangeToChartTime` 和当前垂直细分换算。镜像只作用于横坐标，不反转事件的时间顺序。两个快捷键入口都只登记请求，并在同一帧的 `LateUpdate` 合并执行一次。

## 冲突、首节点和历史记录

- 粘贴目标拍已有画布事件时，先删除旧事件，再插入剪贴板事件。
- 剪切会把首个画布事件放入剪贴板，但不会从原画布删除它。
- 一次剪切或粘贴只建立一个 `EditControl.Stepping`，因此 Undo/Redo 也是一步。
- 操作前要求事件列表已按时间严格递增；插件不会擅自排序已有数组。

## 面板

`SpeedEasePanel` 按当前工具切换内容（完整对照见 `docs/USER_GUIDE.md`）：

- “画布流速编辑”：流速缓动与流速剪贴板。
- “画布编辑”：画布事件剪贴板（不重复实现原画布缓动选项）。
- “导出到剪切板”打开后，成功的复制和剪切会写入系统剪切板；导入只替换内部剪贴板。
- `F8` 控制面板总开关。

## 实现位置

- `src/RhythmEditorMod/SpeedClipboardController.cs`：标签识别、快捷键、原矩形框选和两个独立剪贴板。
- `src/RhythmEditorMod/SystemClipboard.cs`：Windows 系统剪切板读写。
- `src/RhythmEditorMod.Core/EditorClipboardText.cs`：剪贴板文本格式。
- `src/RhythmEditorMod/EditorOperations.cs`：当前画布事件的选择、剪切、覆盖粘贴、Undo/Redo 和界面刷新。
- `src/RhythmEditorMod.Core/CanvasEventEditing.cs`：鼠标锚点平移和横向镜像的纯数据转换。
- `tests/RhythmEditorMod.Core.Tests/Program.cs`：画布事件锚点、镜像与细分吸附测试。
