# 代码与资源地图

这是阶段 1 起逐步维护的代码与资源地图。当前没有源码，因此“文件”可能是二进制、Unity 场景、序列化资源或从受保护元数据中恢复的原源码路径字符串；方法签名已确认不等于方法体逻辑已确认。

可信状态：`confirmed`、`likely`、`uncertain`、`unknown`。

## 程序启动

状态：`confirmed`

- 文件：`Rizline Editor.exe`
- 职责：Windows x64 Unity 启动器
- 运行时：`UnityPlayer.dll` 2022.3.62f2
- 应用原生逻辑：`GameAssembly.dll`
- 唯一构建场景：`Assets/Scenes/Editor.unity`
- 发布场景文件：`Rizline Editor_Data/level0`

## 启动初始化

状态：`confirmed`（注册关系）；具体执行逻辑 `unknown`

文件：`Rizline Editor_Data/RuntimeInitializeOnLoads.json`

注册方法：

- `ProjectInformation.Init`
- `ThemeControl.SyncColors`

## 主窗口与工具栏

状态：`likely`

场景对象或文本：

- `WindowCanvas`
- `TopBarCanvas`
- `ToolbarButton`
- `ToolbarToolButton`
- `ToolbarHoverButton`

候选类型：

- `WindowControl`
- `ToolbarBodyHolder`
- `ToolbarButton`
- `ToolbarToolButton`
- `ToolbarHoverButton`

已确认 UI 绑定方法名：`WindowControl.SetWindowFeedback`。

## 项目与文件操作

状态：类型、字段、方法签名和下列直接调用 `confirmed`；未列出的分支与间接调用仍为 `unknown`

从 IL2CPP v31 元数据确认的原源码路径字符串：

- `Assets/Scripts/MainScripts/DataManagerController.cs`
- `Assets/Scripts/MainScripts/FileControl.cs`
- `Assets/Scripts/MainScripts/ProjectControl.cs`
- `Assets/Scripts/MainScripts/ProjectInformation.cs`
- `Assets/Scripts/MainScripts/WorkspaceDB.cs`

确认的职责边界：

- `DataManagerController`：`SelectOpenPath(title, formatName, format)`、`SelectSavePath(title, formatName, format)`，以及原生文件对话框入口 `GetOpenFileName(ofn)`、`GetSaveFileName(ofn)`。
- `FileControl`：`Save`、`AutoSave`、`AutoSaveCountdown`、`SaveAs`、`OpenProject`、`OpenLastProject`、`NewProject`、`SaveCache(filePath, chart)`、音乐加载及手册/样例目录入口。
- `ProjectInformation`：持有 `fileControl`、当前 `chart`、`filePath`、`notSaved`、`ChartIsDirty` 等中央编辑状态；提供 `MarkChartDirty`、`ClearChartDirty` 和 `SyncWindowTitle`。
- `ProjectControl`：持有 `chartName`、`songName`、`chartPath`、`songPath` 等启动界面字段；当前确认的方法只有 `Start`、`OnApplicationQuit` 和构造函数。
- `WorkspaceDB`：持有 `writer`、`db`、`services`，提供 `Register`、`Get`、`Init`、`GetAll`；更像通用工作区持久化服务，尚未确认是否直接参与谱面文件保存。

场景中的 `NewProject`、`OpenProject`、`OpenLastProject` 现已通过元数据确认属于 `FileControl`，此前把它们列为 `ProjectControl` 候选的判断已修正。

## JSON 谱面读取与保存

状态：文件操作行为、类型、方法签名、原生入口地址和下列直接调用 `confirmed`

以下 RVA 只适用于 SHA-256 为 `F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876` 的当前 `GameAssembly.dll`：

| 方法 | token | RVA |
| --- | --- | ---: |
| `FileControl.Save` | `0x0600005D` | `0x2A9440` |
| `FileControl.AutoSave` | `0x0600005E` | `0x2A7610` |
| `FileControl.SaveAs` | `0x06000062` | `0x2A8E60` |
| `FileControl.OpenProject` | `0x06000063` | `0x2A8770` |
| `FileControl.OpenLastProject` | `0x06000064` | `0x2A81C0` |
| `FileControl.NewProject` | `0x06000065` | `0x2A7EB0` |
| `FileControl.SaveCache` | `0x0600006E` | `0x2A91E0` |

已确认的保存主链：

1. `Save` 先检查当前路径；没有可用路径时跳转到 `SaveAs`。
2. `SaveAs` 调用 `DataManagerController.SelectSavePath`；两条保存路径都会记录项目/歌曲路径到 `PlayerPrefs`，并先调用 `AutoSave`。
3. `Save` / `SaveAs` 调用 `WorkspaceDB.GetAll`，把结果临时放入当前 `Chart.__db`。
4. 使用 `JsonConvert.SerializeObject(value, Formatting, settings)` 序列化当前 `Chart`，再调用 `File.WriteAllText` 写入主谱面文件。
5. 写入后清空临时 `Chart.__db`；随后更新窗口/未保存状态。`SaveAs` 明确调用 `ProjectInformation.SyncWindowTitle`。

已确认的自动保存主链：

1. `AutoSave` 只在当前项目路径非空且文件存在时继续。
2. 它先用 `JsonConvert.SerializeObject(value, settings)` 序列化当前 `Chart`，与 `FileControl.chartJson` 比较；内容未变化时直接返回，变化时更新 `chartJson`。
3. 它在主谱面所在目录下组合自动保存目录和编号文件名，枚举旧文件、解析编号，并包含删除旧文件的轮换逻辑。
4. 写入前同样把 `WorkspaceDB.GetAll` 临时放入 `Chart.__db`，然后序列化并通过 `File.WriteAllText` 写入自动保存文件，最后清空 `Chart.__db`。

已确认的打开主链：

1. `OpenProject` 调用两次 `DataManagerController.SelectOpenPath` 并验证两个路径，随后以 `File.ReadAllText` 读取谱面文本。
2. 读取文本经泛型 JSON 反序列化实例进入 `ProjectInformation.chart`；这个泛型实例的原生地址为 `0x436E40`，尚未从共享泛型注册项恢复出完整签名，因此签名细节仍记为 `likely`。
3. 方法记录路径到 `PlayerPrefs`，调用 `FileControl.LoadMusic` 并启动协程。
4. 加载后的当前 `Chart` 会再次序列化到 `FileControl.chartJson`，作为自动保存的变化比较基线；随后启动 `CheckIfLoadCompleted` 协程。
5. `OpenLastProject` 从 `PlayerPrefs.GetString` 取回路径，然后执行同类的读取、音乐加载、基线序列化和加载完成检查。

`SaveCache(filePath, chart)` 会创建缓存目录，至少执行两次 `JsonConvert.SerializeObject(value, Formatting, settings)` 与 `File.WriteAllText`，并遍历待处理的谱面对象。全 DLL 字节扫描没有找到对其入口 RVA `0x2A91E0` 的直接 `E8` 调用，因此它可能通过委托、接口、协程/退出路径间接调用，也可能是未使用入口；当前不能断言。

证据：

- `ScriptingAssemblies.json` 包含 `Newtonsoft.Json.dll`。
- `#Samples/` 中存在 18 个结构一致且可解析的谱面 JSON。
- 存在 `Rizline Editor Auto-Save/` 及编号自动保存文件。
- `FileControl` 字段包含 `jsonSettings`、`chartJson`、`db`、`pendingChart`、`projectInformation` 和 `songPath`。
- 普通保存入口为 `FileControl.Save`，另存为入口为 `FileControl.SaveAs`，自动保存入口为 `FileControl.AutoSave`；`AutoSaveCountdown` 具有编译器生成的协程状态机 `<AutoSaveCountdown>d__11`。
- 打开入口为 `FileControl.OpenProject` / `OpenLastProject`，新建入口为 `FileControl.NewProject`。
- `DataManagerController.SelectOpenPath` / `SelectSavePath` 封装打开和保存路径选择。
- `FileControl.SaveCache(filePath, chart)` 接收路径与 `Chart` 对象；方法体确认它会建立缓存目录并写入 JSON，但调用者仍未确定。
- 运行基线确认 `OpenProject` 对应的界面操作能够打开隔离样例。
- `Ctrl+S` 为保存，`Ctrl+Shift+S` 为另存为。
- 未编辑样例经“另存为”后，输出 JSON 与输入 JSON 字节完全一致。
- 实际编辑后，普通保存和自动保存会重建 JSON，并删除实验加入的未知顶层字符串字段。
- `lineColor` 与 `judgeRingColor` 的普通保存和自动保存均保留同刻、倒序事件的原数组顺序；编辑器不会按 `time` 自动排序。
- 多轮实验观察到自动保存可能记录编辑操作的瞬时中间状态，且文件名包含 `latest` 不足以证明它是最后写入的完整状态。

尚未确认未知嵌套字段、一般对象字段顺序和浮点精度的完整保存规则。泛型反序列化实例、`SaveCache` 的间接调用者、自动保存轮换上限和缓存文件精确命名仍需后续恢复。

## 数据模型与编辑事务

状态：类型、字段、方法签名和下列直接调用 `confirmed`

- `Chart` 直接声明 `fileVersion`、`chartDelayMs`、`themes`、`challengeTimes`、`bPM`、`bpmShifts`、`lines`、`canvasMoves`、`cameraMove`、`__db`；另有 `RealTimeToChartTime`、`ChartTimeToRealTime` 和 `Clone`。
- `Line` 只声明四个 JSON 成员：`linePoints`、`notes`、`judgeRingColor`、`lineColor`，并提供 `Clone`。
- `LinePoint` 声明 `time`、`xPosition`、`color`、`easeType`、`canvasIndex`、`floorPosition`。
- `Note` 声明 `type`、`time`、`floorPosition`、`otherInformations`。
- `KeyPoint` 声明 `time`、`value`、`easeType`、`floorPosition`；被 BPM、画布和相机关键点列表复用。
- `ColorKeyPoint` 声明 `startColor`、`endColor`、`time`，并提供 `CanMerge`、`ColorsAreEqual`、`CompareTo`。
- `EditControl.Stepping`（token `0x0600003C`，RVA `0x2A55C0`）在执行编辑动作前后调用 `Chart.Clone`，标记谱面 dirty，并受 `MinRecordSteps` / `PreservedMemory` 和可用内存检查约束。
- `EditControl.Undo`（RVA `0x2A5810`）和 `Redo`（RVA `0x2A3B00`）均调用 `ProjectInformation.MarkChartDirty`，最后进入 `OnChartUndoRedo` 通知链。
- `EditorObjectControl` 持有画布、线组、线和音符 prefab，并负责在 `Start` / `Update` 中刷新或实例化编辑对象；具体逐帧分支尚未展开。

这说明后续功能修改必须通过 `EditControl.Stepping` 进入历史记录，直接改 `ProjectInformation.chart` 很可能会绕过 dirty 状态和 Undo/Redo。

## 线、音符、选择、复制与粘贴

状态：方法归属与主要调用 `confirmed`；精确交互条件仍需逐个方法反编译

- `LineEditor` 持有 `selectAxis`、`selectArea`、`lineClipboard`、`mirrorLineClipboard` 和拖动起点状态。
- `LineEditor.CopyLines(lines, additional)`、`CutLines(lines, additional)`、`SelectLines(color, next)`、`Paste(mirror)` 分别是线的复制、剪切、选择和普通/镜像粘贴入口。
- `LineEditor.ToolInput`（RVA `0x2D4180`）检查 `ShortcutKeyControl.CtrlPressed`，处理选择区域/颜色与线点编辑，并在多个编辑分支调用 `EditControl.Stepping`；它还在部分修改后调用 `EditControl.SetFloorPosition`。
- `LineEditor.Paste`（RVA `0x2D2C10`）在完成粘贴后调用 `EditControl.Stepping`，因此粘贴进入 Undo/Redo 历史。
- `NoteEditor` 持有音符预览、类型、Hold 起始时间和起始线索引；`ToolInput`（RVA `0x2D6E70`）的多个新增/修改分支调用 `EditControl.Stepping`，Hold 预览会调用 `ProgressControl.ChartTimeRageToRealTime`。
- `InputControl` 的关键入口为 `GetMousePosition`、`GetMouseButton`、`GetNearestLine`、`GetNearestLinePosition(lineIndex, time, nearestNoteIndex)` 和 `CheckClick`，负责把鼠标输入映射到最近的线和音符位置。

## 画布

状态：类型和方法签名 `confirmed`；精确拖动算法 `unknown`

场景对象：

- `Canvas Editor`
- `Canvas Selector`
- `CanvasKeyPoints`
- `CanvasPos`
- `Focused Canvas`
- `GameSceneCanvas`

关键类型：`CanvasControl`、`CanvasEditor`、`CanvasInformation`、`CanvasKeyPointInformation`、`FocusCanvasEditPanelControl`。

`CanvasEditor` 持有拖动起点 `_startPositionX`、`_startTime`、`_startCanvasPositionX`、`_startDrag` 和当前关键点索引；入口包括 `MoveCurrentKeyPoint(step)`、`AddCanvas`、`DeleteNowCanvas`、`Update` 与 `SetCanvasKeyPoint`。对应 RVA 分别包括 `0x2CB990`、`0x2CB530`、`0x2CB610`、`0x2CD560`、`0x2CBD80`。

## 时间轴与播放进度

状态：类型、方法签名和播放/暂停主要调用 `confirmed`

场景对象或资源名：

- `TimeLine`
- `BeatLines`
- `Beats`
- `project_tl_drag`
- `PlayMask`
- `Play Speed`

关键类型：`ProgressControl`、`ProgressPercentDisplay`、`SnapControl`、`LineControl`。

- `ProgressControl` 持有 `_audioSource`、`_isPlaying`、`playSpeed`、时间文本、进度条和书签数据。
- 时间换算入口为 `RealTimeToChartTime`、`RealTimeRangeToChartTime`、`ChartTimeRageToRealTime`；后者的拼写 `Rage` 是程序原名。
- 播放入口为 `PlayOrPause`（token `0x060000EE`，RVA `0x2BA1F0`）、`PlayWithoutPreview` 和 `RePlay`。
- `PlayOrPause` 的暂停分支直接调用 `AudioSource.Pause`；播放分支设置音高和音频时间后调用 `AudioSource.Play`，同时切换多个编辑/预览对象和画布交互状态。
- 已确认 UI 绑定方法：`ProgressControl.SetPlaySpeed`。

垂直细分状态：

- `ProjectInformation.get_SnapLinesSplitCount`（token `0x06000144`，RVA `0x2BE890`）读取当前垂直细分；垂直吸附关闭时返回 1，否则读取并取整 `snapLinesSplit` 的 UI 值。
- `ProjectInformation.set_SnapLinesSplitCount`（token `0x06000145`，RVA `0x2BED70`）把整数写回同一 UI 状态。
- `SnapControl.ApplyPreset`（RVA `0x2C4370`）直接跳转到该 setter。
- `ShortcutKeysControl.KeyWithShift`（RVA `0x2C1B30`）读取鼠标滚轮，并对 getter 的结果执行 ±1 后写回 setter。
- 使用者确认 UI 可在 `1/1` 至 `1/64` 间设置。正式流速缓动功能将直接复用这个 getter 作为采样密度来源。

## 音频

状态：运行行为与加载入口 `confirmed` / 播放同步方法体 `unknown`

- 依赖中包含 `NAudio.dll`。
- 示例音乐使用 OGG。
- 运行基线确认 OGG 能够播放、有声音、时间轴同步移动且可正常暂停。
- `FileControl` 包含 `LoadMusic`、`LoadOGG(filePath)`、`LoadMP3(filePath)`、`CheckIfLoadCompleted` 和 `SetBpm`。
- 尚未定位实际播放、暂停和时间轴同步的方法体。

## 输入与快捷键

状态：类型、修饰键分派和方法入口 `confirmed`；各具体按键码仍未完整映射

- `ShortcutKeyControl` 提供 `CtrlPressed`、`AltPressed`、`ShiftPressed`、`HelpPressed` 及对应松开状态，并通过 `RegisterShortcutKey(holder, shortcutKey, description, action)` 维护可显示的快捷键提示。
- `ShortcutKeysControl.Update`（RVA `0x2C3590`）读取 Unity Legacy Input，并按无修饰键、Ctrl、Alt、Shift、Ctrl+Alt 分派到独立处理方法。
- `KeyWithControl` 调整相对流速；显示值在负向是 `1 + 相对流速 / 10`，原先下限 -9 对应 0.1。插件把该下限改到 -15，显示最低 -0.5，同时保持由此联动的编辑器纵向缩放为正。`KeyWithAlt` 结合鼠标滚轮与歌曲长度移动时间；`KeyWithShift` 调整拍线细分；`KeyWithControlAlt` 调整纵向吸附密度。
- 运行基线已确认：`Ctrl+S` 为保存，`Ctrl+Shift+S` 为另存为。保存快捷键的委托绑定发生在初始化闭包中，尚未把具体闭包映射到按键码。

## 相机、速度、颜色与主题

状态：速度编辑器类型、字段、方法及下列直接调用 `confirmed`；其余类型职责 `likely`

- 相机：`CameraControl`、`CameraEditor`
- 速度：`SpeedEditor`
- 颜色：`ColorEditor`、`ColorSelectorControl`。运行中的调色板是 `ProjectInformation.colorPanels`（`Color[]`），变更时触发 `OnColorPanelsChanged`；`PlayerPrefs` 键 `colorPanels{n}` 保存同一组颜色，空槽为 `FFFFFFFF`。
- 主题：`ThemeControl`、`ThemeEditor`、`ThemeElement`
- 缓动：`GetEase`

`SpeedEditor` 持有 `bpmShiftColor`、`canvasSpeedColor`、`currentTag`、`editInput`、`currentNode`、`nowCanvasIndex`、`nowTime`、`nowSpeed`、`editingBpmShift` 和画布关键点预览对象。当前版本的关键方法为：

| 方法 | token | RVA | 已确认作用 |
| --- | --- | ---: | --- |
| `ReloadTags` | `0x06000372` | `0x2E1270` | 重建 BPM/流速标签并调用 `MoveTags` |
| `EditCurrentTag` | `0x06000374` | `0x2E0A10` | 进入当前标签编辑 |
| `DeleteCurrentTag` | `0x06000375` | `0x2E0910` | 删除当前标签 |
| `EditOver` | `0x06000376` | `0x2E0B50` | 解析输入值并调用 `EditControl.Stepping`，随后刷新标签 |
| `UpdateCanvasMovePreview` | `0x06000379` | `0x2E27A0` | 更新画布流速预览 |
| `SetCanvasKeyPoint` | `0x0600037A` | `0x2E1B70` | 构建关键点预览；调用 `GetEase.GetEaseWithIndex` |

`GetEase.GetEaseWithProgress` 和 `GetEase.GetEaseWithIndex` 分别位于 RVA `0x2A9D50` 和 `0x2A9C70`。现有 `SpeedEditor` 没有缓动类型或采样密度字段；正式接入流速缓动需要新增 UI/状态入口，或使用独立辅助界面，而不能仅把 `speedKeyPoints[].easeType` 改成非零值。

缓动的完整链路（2026-09-23 实测，详见 `docs/features/EXTRA_EASING.md`）：

| 项 | 事实 |
| --- | --- |
| 名称表 | 19 个名字**只存在于场景** `Rizline Editor_Data\level0` 的 Dropdown 序列化选项；`GameAssembly.dll` 和重建的 `global-metadata.dat` 里都没有 |
| UI | `CanvasEditor.easeType` / `LineEditor.easeType` / `CameraEditor.easeType`（`UnityEngine.UI.Dropdown`，各 19 项）；`CanvasEditor.easeTypeEditor` 指向场景对象 `Canvas Ease Editor`；三者各有 `LoopEase(int)`，前两者另有 `OnEaseTypeChange()` |
| 单位 | `KeyPoint.easeType` / `LinePoint.easeType` / `KeyPointEaseMark.easeType`（`int`） |
| 求值 | `GetEase.EaseInfors` 是 `19 × 101` 的预计算表，第 `type` 行对应 `progress 0.00–1.00`；`GetEaseWithIndex(index, type)` 的 `index` 被钳在 0–100，`GetEaseWithProgress(t, type)` 对 `type ≥ 19` 抛 `Il2CppException` |
| 预览 | `CanvasEditor.SetCanvasKeyPoint()` 调用 `GetEase.GetEaseWithIndex`；`SpeedEditor.easeMarkContainer` 指向 `EaseMarks` |

因此「新增缓动」不能在谱面里写 `easeType ≥ 19`（编辑器自己就会抛异常，游戏行为未知），只能把新曲线烘焙成大量 `easeType = 0` 的普通节点；方案与未决参数见 `docs/features/EXTRA_EASING.md`。

首个算法原型位于 `tools/Generate-SpeedEase.py`。它不修改发布文件，只将目标曲线采样成原格式的多个 `speedKeyPoints`，并在已验证的单画布、恒定 BPM 子集内重算位置。详见 `baseline/SPEED_EASING_PROTOTYPE.md`。

`SpeedEditor.ReloadTags` 的闭包类型进一步确认了标签编辑结构：

- `NodeInfo` 保存 `KeyPoint keyPoint` 和 `ValueTag tag`；标签集合使用 `LinkedList<NodeInfo>` 节点。
- `<>c__DisplayClass24_2.<ReloadTags>b__1`（token `0x06000384`，RVA `0x2EB300`）是实际的单节点删除操作：调用 `EditControl.Stepping`、销毁标签对象、维护当前节点并调用 `SpeedEditor.MoveTags`。
- `SpeedEditor.DeleteCurrentTag` 主要负责检查播放/选中状态、保护首节点，然后调用当前标签绑定的删除委托。

运行时插件入口位于 `src/RhythmEditorMod/`：

- `SpeedEasePanel.cs`：按 `ToolbarToolButton` / 各 `*Editor` 切换 IMGUI 面板。工具识别全部只看**当前选中按钮自身**（D-021）：`toolObject != null` 走编辑器分流；`toolObject == null` 且**有直接子物体 `Body`** 判为发布（仅谱面统计，另以该子树内的 `PublishControl` 作附加确认，不做全场景查找）；`toolObject == null` 且**没有 `Body`** 判为光标（基础设置：突破缩放、预览细线、自绘宽高/透明度滑条，另需没有任何已知编辑器或发布控件激活）；插件自己的入口按对象身份判为自定义工具面板（D-022）。音符编辑：无插件面板。流速：缓动下拉。画线/颜色/主题：调色板。面板命中时跳过 `LineEditor.ToolInput`。每次工具栏选择写一行 `已选择工具：按钮 <按钮名>，toolObject <toolObject 名>，Body <有/无>，PublishControl <名/无>，面板 <PanelMode>` 日志，作为工具识别的证据，便于换版本后定位。工具栏按钮的完整结构转储见 D-021；`PublishControl` 的查找方式与 Il2CppInterop 陷阱见 D-022。
- `SpeedClipboardController.cs`：`Ctrl+C/X/V/D`；框选复用 `LineEditor.SelectLines`。颜色目标的判断见 D-019：不再读 `ColorEditor.editingLineColor`，而是用 `_SelectLines_d__35._selectionStart_5__2` 与松开时的 `Input.mousePosition` 经 `InputControl._camera.ScreenToWorldPoint` 换算成大地 x，与 x=0 比较后决定取 `lineColor`、`judgeRingColor` 还是两者；粘贴跟随上一次真正复制到内容的那次复制。没有聚焦画布（`ProjectInformation.focusCanvasIndex < 0`）时拒绝开始并提示。原编辑器的 `ColorEditor.CopyColors` / `PasteColors` 在颜色工具下被 Prefix 拦截，但同一条件不成立时放行，避免 Ctrl+C/X/V 两头失效。`Ctrl+D` 用堆上 `SelectionRequest` / `LineDeleteRequest` 传递删除标志（Il2Cpp 委托不可靠捕获局部量），删除后恢复软件剪贴板；线删除经 `CaptureLines`/`ReplaceLineClipboard` 深拷贝还原。
- `CustomToolbarTool.cs`：插件自己的工具栏入口（D-022）。运行时克隆**光标按钮**（唯一「无 `toolObject` 且无 `Body`」的按钮）追加到 `Tools` 末尾，标题「插件」，快捷键 `9` 由插件自己处理；克隆后调用原版 `Start()`，再做选择自检并恢复原工具。面板按**对象身份**（`ActiveToolButton == Button`）判为 `PanelMode.CustomTool`，不按名字或结构。查找组件走 `FindComponent<T>`（手动遍历子树 + `GetComponents<Component>()` + `TryCast<T>()`），因为 `GetComponentInChildren<T>(bool)` 在本构建必抛 `VerificationException`。
- `ProjectSwitcher.cs`：编辑期内切换工程（D-024）。切换前若 `notSaved` 就先调用原版 `FileControl.Save()`（无路径则拒绝）；随后把目标路径写入 `PlayerPrefs` 的 `lastFilePath`/`lastSongPath` 并调用原版 `FileControl.OpenLastProject()`，等待「目标路径生效 + `loadOver` + `_songLoadOver`」判定完成；完成后清理上一次遗留的 `%TEMP%\tmpXXXX.tmp` 音乐缓存（仅限系统临时目录、扩展名 `.tmp`、与当前缓存不同）。「选择文件…」直接调用原版 `FileControl.OpenProject()`（两步对话框）。每 2 秒轮询 `filePath` + `songPath`，把当前工程记入槽位并更新状态行。
- `NativeProjectPanel.cs`：**「项目」工具的原生 body**。整体克隆 `Canvas/Toolbar/Tools/Edit Canvas/Body`（保留原生 `Title`/`Lock`，销毁源工具控件，移除克隆体上的 `ToolbarBodyHolder`），行控件克隆该 body 的 `DeleteCanvas`（`Graphic+Button+Text`）、状态行克隆 `Canvas Title`；标题栏白字「项目」并把子物体拉伸填满标题框，行文字黑色并开启自动缩放与截断。挂在插件自己的工具栏按钮下、名为 `PluginBody`；**显隐规则为「选中即显示」**（不跟随鼠标，原因见 `PROJECT_SWITCH.md` 5.2）。组件查找一律走 `FindComponent<T>`（`GetComponents<Component>()` + `TryCast`），因为 interop 会把 `RectTransform` 报成基类 `Transform`。
- `ExtraEasing.cs`：额外弹性缓动的入口与拟合。向**画布**（`CanvasEditor.easeType`）与**摄像机**（`CameraEditor.easeType` / `selectEase`）的下拉框末尾追加 `In Elastic` / `Out Elastic` / `InOut Elastic`；画布在 `CanvasEditor.OnEaseTypeChange` 拦截，摄像机在 `Dropdown.set_value` 处拦截。写入目标：画布 `CanvasMove.xPositionKeyPoints`、摄像机 `CameraMove.xPositionKeyPoints`（**只用横向移动列表**；缩放列表经使用者决定不碰，选择时只提示不支持）。共用拟合核心 `BakeKeyPoints`：定位播放头所在区间 → `EaseFit.SampleCurve` 自适应采样 → 一个 `Stepping` 内删掉区间内旧节点、把起点 `easeType` 置 0、插入 Linear 节点 → `SetFloorPosition()` → 对应工具刷新。**编辑线已移除**（曾写入选中线节点的 `linePoints`，因与「拖动创建线 / `Ctrl`+拖动改属性」的交互割裂，使用者决定改回原版）。
- `EaseDropdownPatch.cs`：`Dropdown.set_value` 的 Prefix。摄像机的缓动接线不是画布那条，若不拦截，原版会把追加选项的索引（19+）写进关键点，`GetEase` 求值即越界（`IndexOutOfRangeException`）。此处按引用只匹配插件自己的下拉，拟合后把值换成 Linear 再放行，非法索引永不入谱。
- `EaseTypeGuard.cs`：越界缓动的安全网。`GetEase.GetEaseWithProgress` / `GetEaseWithIndex` 遇到 `type` 超出 0–18 时按 Linear 返回（`progress` / `index/100`）并告警一次；正常 0–18 的求值完全不受影响。
- `PluginLayout.cs`：body 的布局参数，读写 `BepInEx/config/RhythmEditorMod/layout.json`（面板尺寸/锚点/轴心/偏移、首行、行高、边距、状态行高度、标题框尺寸与留白、标签与状态字数上限）。键名大小写不敏感，未知键会告警。
- `ProjectSlots.cs` + `RhythmEditorMod.Core/ProjectSlotList.cs`：最近三个工程的持久化（`BepInEx/config/RhythmEditorMod/projects.json`，明文 JSON）与纯逻辑（按谱面路径大小写不敏感去重、置顶、容量 3、空路径忽略）。
- `SystemClipboard.cs`：Windows 剪切板 Unicode 文本。
- `RhythmEditorMod.Core/EditorClipboardText.cs`：内部与系统剪切板文本互转；空格/制表符；颜色块按 header 分段，支持一次写入线色与判定环两个区块。
- `EditorOperations.cs`：流速/画布事件/摄像机移动的 Stepping 修改；调色板写入；`FormatChartStatistics`。
- `EventClipboardOperations.cs`：线色、判定环、线/镜像线剪贴板捕获与替换。颜色写入必须构造 `Color32`（0–255），构造 `Color` 会被隐式转换钳成白色（D-020）；粘贴时塌缩目标拍上的同拍重复并重定向粘贴块最后一条的 `endColor`。
- `RhythmEditorMod.Core/ColorEventPasteTargeting.cs`：纯数据判定——目标拍上已有事件的下标（含同拍多条）与保持拍序的插入下标。
- `NoteSpeedLimit.cs`：`set_RelativeSpeed` 下限在突破极限开启时从 -9 改为 -15。
- `PreviewMaskControl.cs`：光标「基础设置」面板的 `预览蒙版：开/关`（D-027）。**开**＝零干预；**关**＝带预览播放期间隐藏 `blackMask`（编辑器 UI 画布上两片铺满左右半屏的深灰图）与 `playMask` 两个主题组中**名字命中显式白名单**的侧板（`TopSide`、`BottomSideForward`、`BottomSideBack`、`LeftSide`、`RightSide`；共 10 块），保留各组的 `Background`（背景色）。判据是**精确名字匹配而非名字后缀**（后缀曾漏掉 `BottomSideForward`/`BottomSideBack`，导致播放头以下内容被遮）；作用域固定在 `playMask` 的孙辈两层。`SetActive(false)` + 每帧重申（在 `SpeedEasePanel.LateUpdate`），播放停止不还原、播放中拨回「开」按隐藏前状态恢复；不碰 `editorMask`（仍是 D-017 的职责）。结构与弯路记录见 `docs/features/PREVIEW_MASK.md`。
- `src/RhythmEditorMod.Core/ZoomStepping.cs`：整体缩放的纯逻辑与范围常量（`Minimum = 0.4`、`Maximum = 2`、`Step = 0.1`）、`Next(current, notches)`（加法步进 + 10% 栅格取整 + 夹取）、`Notches(scrollDelta)`（滚轮量 → 整数格）、`IsNeutral`（仅 100% 才恢复原版投影）。D-031，含自动测试。- `CrossCanvasLineClipboard.cs`：跨画布线接管（D-030）。线编辑内挂 `LineEditor._Start_b__28_3/5/8/9`（`Ctrl+C/X/V`/`Ctrl+Shift+V`）：`Ctrl+C/X` 时由插件 `StartCoroutine` 起原版框选协程，拖动期间**按住 Shift** 才实时高亮并按「当前画布上被完全框住的节点 → 整条线」判定；复制写插件剪贴板并清空原版两个线剪贴板，原版复制/系统导入则清空插件剪贴板；粘贴排队到下一帧、锚点取「全剪贴板最早拍、并列取最大 x」、画布整体偏移、`Stepping` + `SetFloorPosition` 写入、0.5 秒后只读复核渲染。x 吸附 = 实测基础格距（`SnapLines/SnapLine(Clone)` 世界 0.5625 = 局部 0.1）÷ `snapHLinesSplit`，局部 x 由实测渲染映射（斜率 5.625）求得。细节见 `docs/features/CROSS_CANVAS_LINES.md`。
- `src/RhythmEditorMod.Core/CrossCanvasLineEditing.cs`：跨画布线的纯逻辑（可单测）：`DistinctCanvases`/`CountCrossCanvas`、`TryEvaluateKeyPoints`（缓动关键点求值）、`TryPlace`（锚点规则 + 画布偏移 + Hold 的 `otherInformations[0]/[1]` + 音符/颜色关键点随线平移 + 镜像反射 + 越界拒绝）。
- `EventClipboardOperations.cs`：`CaptureLine`/`CreateLine` 已开放为 `internal` 并新增 `CaptureLines(IReadOnlyList<Line>)`，供跨画布线剪贴板复用。
- `EditorOperations.cs`：`GetCanvasPasteAnchor` 与 `CanvasEditorWorldUnitsPerChartUnit` 已开放为 `internal`（跨画布线粘贴复用同一鼠标锚点与单位口径）。- `PlayAreaFrame.cs`：游玩区域边框（D-029）。满足「`预览蒙版：关` 且带预览播放」或「整体缩放 ≠ 100%」时，用 2px 边框标出原本未被遮住的区域。几何全部取**世界固定值**再投影：左右 = `LeftSide.min.x`/`RightSide.max.x` 的**外侧**边（∓2.813）、下 = `BottomSideForward.max.y`（−2.9，**排除** `BottomSideBack`）、上 = `TopSide.max.y` 的外侧边（+4.1）；在 `playMask` 与 `editorMask` 两个根里取最外/最内。颜色 = `themes[inChallengeTime?1:0].colorsList[2]`（**0–255**，÷255 后给 `GUI.color`）。绘制路径：`GUI.DrawTexture` 在本构建被裁剪（`Method unstripping failed`），故用自建 1×1 纹理 + 纯色 `GUIStyle` 配 `GUI.Box`，不可用则回退默认 `GUI.Box`。线宽恒定 2px，不随缩放变粗。挂在 `SpeedEasePanel.OnGUI` 早期，不受 F8 显隐影响。细节见 `docs/features/PLAY_AREA_FRAME.md`。
- `EditorNavigation.cs`：突破极限门闩；**等比缩放**（`halfHeight = orthographicSize / factor`、`halfWidth = halfHeight × aspect`，D-028）；范围 **40%–200%、每格 10 个百分点（加法）**，放大与缩小共用门闩（D-031），步进/夹取/中性判定全部走 `Core/ZoomStepping.cs`；`Ctrl+M` 复位；横向缩小时把 ScreenSpaceCamera 画布搬 Overlay 以保 UI 可视（代价：点击命中错位，见 bug 清单第 4 条）。**D-017 的 editorMask 自动隐藏在 D-028 中整段删除**（`ApplyEdgeMasks`/`SetMaskActive` 已不存在）。
- `RhythmEditorMod.Core/SpeedEditing.cs`：采样、闭区间、粘贴规划；`FilterInsertSamples` 去掉塌缩到端点的内部点。
- `RhythmEditorMod.Core/CanvasEventEditing.cs`：画布事件粘贴纯数据转换。
- `RhythmEditorMod.Core/ChartColorCollection.cs`：颜色去重与空槽合并。

相关工具栏类型：`ToolbarToolButton`（`SelectTool` / `UnselectTool` / `activeTool` / `toolObject`）。不要挂钩 `LineEditor`/`ColorEditor` 共享的 9 字节 `OnEnable`（RVA `0x2CF1D0`）。

画布事件粘贴不再读取 `EditorInput` 的 `Nullable<Vector2>`：`.Value` 会丢失分量，直接 `.value` / `.hasValue` 也在运行时随机得到全零副本。当前实现复现两个原生方法的源计算：`InputControl.GetMousePosition`（RVA `0x2AA9D0`）从 `Input.mousePosition` 和 `_camera.ScreenToWorldPoint` 得到世界坐标，使用 `worldY + 2.4`、`ProjectInformation.speed`、`nowTime/nowChartTime` 与 `RealTimeRangeToChartTime` 计算拍数；`CanvasEditor.Update`（RVA `0x2CD560`）使用 `5.5625` 把世界 x 换成画布事件 value。两轴随后分别按 `VerticalSnapDensity` 和 `SnapLinesSplitCount` 吸附。`SpeedClipboardController` 把两个快捷键入口合并为一个待处理请求，由 `SpeedEasePanel.LateUpdate` 执行一次。

原定 `Shift + 右键` 区间删除在运行验证前被需求替换，当前插件没有该补丁。完整交互见 `docs/features/CANVAS_SPEED_EDITING.md`、`docs/features/CANVAS_EVENT_CLIPBOARD.md` 和 `docs/USER_GUIDE.md`。

## 发布与 Steam

状态：`confirmed`（组件存在）；具体流程 `unknown`

- `PublishControl`
- `SteamEventHandler`
- `Rizline Editor_Data/Plugins/x86_64/steam_api64.dll`
- `Facepunch.Steamworks.Win64.dll`
- 场景包含 Workshop 上传与授权提示文本。

## 插件导出与安装管理器

状态：`confirmed`（0.2.0 干净副本安装、更新、启停和卸载流程均已执行）。

- `installer/RhythmEditorMod.Manager/Program.cs`：安装包/目标校验、payload 哈希、安装状态、更新备份、启停和卸载逻辑。
- `installer/RhythmEditorMod.Manager/ManagerLauncher.cpp`：用户双击的原生 Windows x64 EXE 入口，启动包内私有 `manager-runtime`。
- `tools/Export-RhythmEditorModPackage.ps1`：运行测试和构建，收集已验证 BepInEx/interop/metadata，生成 manifest、目录包、ZIP 和 ZIP 哈希。
- `docs/DISTRIBUTION.md`：面向后续 AI 和维护者的完整分发、安装、更新及验证规范。

管理器默认把 `RhythmEditorMod/` 的上一级识别为目标编辑器目录；高级命令行可用 `status/install/enable/disable/uninstall/remove-runtime` 和 `--target`。安装记录位于目标的 `.rhythm-editor-mod/install.json`，不写入谱面或原始程序文件。

## 配置和持久化

状态：持久化位置 `confirmed`；`WorkspaceDB` 的具体职责仍 `unknown`

候选：

- `ProjectInformation`
- `WorkspaceDB`
- `DataManagerController`
- `ThemeControl`
- `EditorSettings` 资源名

`WorkspaceDB` 提供 `Register(service)`、`Get(key)`、`Init(records)`、`GetAll()`，并包含嵌套类型 `WorkspaceDBWriter`。是否保存最近项目、编辑器设置或其他工作区数据仍需方法体或运行实验确认。

已确认历史上 Unity 会在 `%USERPROFILE%/AppData/LocalLow/PigeonGames/Rizline Editor/` 写入 `Player.log`。2026-08-28 的运行基线中，所监测 PigeonGames AppData 文件和相关注册表位置均未发生变化，已有 `Player.log` 与 `Player-prev.log` 也未更新。

编辑器设置的实际持久化位置是 Windows 注册表的 `HKCU\Software\PigeonGames\Rizline Editor`（Unity `PlayerPrefs`，键名带 Unity 生成的哈希后缀）。2026-09-22 的读取确认了下列键：

| 键前缀 | 内容 |
| --- | --- |
| `lastFilePath` | 上次打开的谱面绝对路径 |
| `lastSongPath` | 上次使用的音乐绝对路径 |
| `colorPanels0` … `colorPanels59` | 调色板槽位，十六进制 RGBA 文本，空槽为 `FFFFFFFF` |
| `windowXCache` / `windowYCache` / `windowWidthCache` / `windowHeightCache` | 窗口位置与大小缓存 |
| `verticalSnapLineIsOn`、`revertX`、`themeEnabled`、`HideKeyHint`、`SkipHint`、`settings.offset` | 编辑器开关与设置 |
| `Screenmanager *`、`UnitySelectMonitor`、`unity.player_session_count`、`unity.player_sessionid` | Unity 自身的窗口、显示器与会话状态 |

**启动行为 `confirmed`**：编辑器启动时会用 `lastFilePath` 自动打开上次的谱面，`lastSongPath` 对应其音乐。该路径可能位于工作区之外（实测为游戏安装目录 `D:\steam\steamapps\common\Rizline\Editor\谱面\...`）；此时编辑器会按正常行为在其同目录的 `Rizline Editor Auto-Save\` 下写入自动保存，而不会改写谱面本体。因此做隔离实验前必须先把 `lastFilePath` 清空或改指到测试谱面。

`Player.log` 不随编辑器启动更新，与 2026-08-28 基线的观察一致；编辑器自身的运行日志由 BepInEx 写入工作区的 `BepInEx/LogOutput.log`。

## 独立手册

状态：`confirmed`

- 文件：`Rizline Editor Manual.exe`
- 技术：Tauri 2.9.3、WebView2、VitePress
- 已发现路由：`/en/`、`/zh/`、`quickstart`、`reference`
- 手册运行会在 `%LOCALAPPDATA%/com.PigeonGames.rizline-editor-manual/` 创建 WebView2 缓存。
