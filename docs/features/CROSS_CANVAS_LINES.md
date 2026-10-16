# 跨画布线的复制与粘贴

状态：**已实现并验收**（2026-10-15/16）。相关：`DECISIONS.md` D-030、`CHART_FORMAT.md`（`linePoints[].canvasIndex`）、`baseline/CROSS_CANVAS_HOLD_EXPERIMENT.md`、`docs/features/CANVAS_EVENT_CLIPBOARD.md`。

## 1. 需求

一条线的节点可以分布在多个画布上（`baseline/json文件各项含义.txt`：「一条线的节点可以在不同画布上，也就是说线的形状不会保持固定」），而**原版既选不中也粘不对**。使用者要求：

1. **框选时按住 `Shift`** → 直接选中「其包含节点的所有对应的线」（整条线，含其它画布的节点）；
2. 框选**过程中**对选中的线**实时高亮**，选完即结束高亮；
3. 复制/粘贴**复用现有按键**（`Ctrl+C` / `Ctrl+V` / `Ctrl+Shift+V`）；
4. 粘贴时 **`canvasIndex` 整体加同一偏移**（例如从画布 2 到 4：节点画布 1→3、2→4、3→5），普通线与跨画布线同一套规则；
5. 自建粘贴**也要有镜像粘贴**（`Ctrl+Shift+V`），逻辑同原版；
6. 命中规则：**节点要被完全框住**（两轴）；`Ctrl` 在框选时无作用（不追加）；
7. 粘贴锚点：**全剪贴板中节拍最小的节点；同拍并列时取 x 最大者**（原版口径）；
8. x 吸附**跟随「水平细分」设置**；拍吸附跟随「垂直细分」。

## 2. 原版为什么做不到（代码与实测证据）

| 事实 | 证据 |
| --- | --- |
| 选择是**按渲染分组**做的，命中判定只把拖框的 **Y 轴换算成拍**（X 不参与原版判定） | 反汇编 `<SelectLines>d__35.MoveNext`（RVA `0x2D7D30`）：`divss [project+0x180]`（流速）→ `call Chart.RealTimeToChartTime` → 存回状态机 `+0x60`；循环体按索引遍历一个 List（`+0x18` 是 Count） |
| 原版选择**读取 Shift**（`ShortcutKeyControl.get_ShiftPressed`，`0x2D861E`） | 同上，且发生在循环之前 |
| 原版**粘贴不做任何画布换算**，直接把每个点改写成当前画布 | `LineEditor.Paste`（RVA `0x2D2C10`）的直接调用里只有 `EditControl.Stepping` 与泛型 List 操作，**没有**鼠标读取、没有时间换算；实测：画布 11 的线复制后切到画布 12 粘贴 → 新线为 `12@1, 12@2, 12@3, 12@3.875`（时间完全不变、画布被改写） |
| 原版镜像 = **把每个节点的 `xPosition` 取负** | 反汇编 `LineEditor.CopyLines`（RVA `0x2D23F0`）：循环节点，`movss xmm0,[rax+0x14]` → `xorps xmm0,xmm6`（取负）→ 写回；`LinePoint` 布局 `time`(+0x10)、`xPosition`(+0x14) |
| `linesGroups` 与谱面线**一一对应**（230 线 ↔ 230 分组），`group.lineData` 就是 `chart.lines[i]`，且**含该线跨全部画布的段** | 探针实测：`lines[32]` 17 节点 → 16 段，段画布 `3 → 4 → 5` |
| **一段属于它左端节点的画布** | 实测 `lines[118]`：谱面节点画布 `3 → 5`，但只有 2 段且段画布都是 `3` ✓ 与跨画布 Hold 实验一致 |
| 原版 `EditorInput` 的四个鼠标字段**在本构建恒为空** | 探针：`mousePositionInEditorScene` / `…SnapToNearestLine` / `…WithSnap` / `…InWorld` 全为 0（`nearestLineIndex/nearestNoteIndex` 为 −1，说明实例是活的），主动调用 `InputControl.GetMousePosition()` 后仍为 0 |
| 原版节点拖动**不按坐标网格吸附** | `LineEditor.ToolInput`（RVA `0x2D4180`）的直接调用里没有任何细分 getter；原版有 `InputControl.GetNearestLinePosition` 与 `mousePositionInEditorSceneSnapToNearestLine`（吸附到最近线节点） |

## 3. 实现

`src/RhythmEditorMod/CrossCanvasLineClipboard.cs`（运行期）+ `src/RhythmEditorMod.Core/CrossCanvasLineEditing.cs`（纯逻辑，可单测）。

### 3.1 接线（`SpeedClipboardController.cs` 的四个入口）

| 补丁 | 目标 | 线编辑（`LineEditorIsActive`）分支 |
| --- | --- | --- |
| `SpeedCopyShortcutPatch` | `LineEditor._Start_b__28_3`（`Ctrl+C`） | `CrossCanvasLineClipboard.BeginSelection(editor, cut: false)` |
| `SpeedCutShortcutPatch` | `_Start_b__28_5`（`Ctrl+X`） | `BeginSelection(editor, cut: true)`（Shift 剪切暂按复制处理） |
| `SpeedPasteExactShortcutPatch` | `_Start_b__28_8`（`Ctrl+V`） | 若 mod 剪贴板非空 → `QueuePaste(mirror: false)`，否则交回原版 |
| `SpeedPasteAlignedShortcutPatch` | `_Start_b__28_9`（`Ctrl+Shift+V`） | 同上，`mirror: true` |

其它工具（流速/画布/颜色/摄像机）的既有分支不变。**不按 `Shift` 时行为与原版完全一致。**

### 3.2 选择（`Shift` + 框选）

- 仍由**原版自己的** `LineEditor.SelectLines(color, callback)` 协程画框：`SpeedEasePanel.Instance.StartCoroutine(routine)`（照抄 `SpeedClipboardController.BeginSelection` 的写法）。缺这一步就没有十字架/矩形 ✗。
- 仅在**已聚焦画布**时启动（`SpeedClipboardController.HasFocusedCanvas()`，原版没聚焦画布时不画矩形）。
- `Track()`（`SpeedEasePanel.LateUpdate`）在拖动期间采样 `Shift`；**只有按住 Shift** 才进入跨画布判定与高亮，且**每帧**按当前矩形重算命中集合（离开矩形的线立刻恢复原色）。
- 命中规则：遍历 `linesGroups`，只看 `segment.lineData.canvasIndex == SelectedCanvas` 的节点，节点渲染位置（`startLineTransform`/`endLineTransform` 经 `WorldToScreenPoint`）**两轴都落在矩形内**即命中 → **整条线**（含其它画布的节点）入选。因此其它画布上恰好也在矩形范围内的线**不会**被误选。
- 拖框起点 `state._selectionStart_5__2` 是**被 `_scale` 缩放过的坐标**：必须 `start /= state._scale_5__3` 才能与渲染屏幕坐标比较；否则**原版 `Ctrl+滚轮` 改过音符流速后第一次框选**会错乱（`_scale == 1` 时是空操作，所以只在缩放后暴露）。
- 取消：原版协程被 `Esc`/右键终止时不会回调 → `Track()` 用 `state.__1__state == -1 && !Input.GetMouseButton(0)` 兜底收尾并清高亮。

### 3.3 高亮

- 直接写 `LineRenderer.startColor/endColor`：原版的 `LineInformation.SetColors` 在本构建**调用即抛 `Il2CppException`**（方法被裁剪，和 `GUI.DrawTexture` 同类），所以不能用。
- 原版每帧重写线色，因此高亮在 `LateUpdate` **逐帧重设**（同 `UpdatePreviewLineWidth` 的做法），并记录原色，在离开矩形/选完/取消时**原样恢复**。
- 高亮颜色取 `LineEditor.copyColor`。

### 3.4 复制与「剪贴板归属」

| 动作 | 行为 |
| --- | --- |
| Shift 拖框（mod 路径） | 采集 `LineClipboardEntry`（逐节点含 `time/xPosition/easeType/canvasIndex/floorPosition/color` + `notes` + 两个颜色数组）→ 清空**原版**两个线剪贴板 |
| `Ctrl+C/X` 不按 Shift | 调原版 `CopyLines/CutLines` → **清空 mod 剪贴板**（`ForgetClipboard`） |
| 系统剪切板导入线 | 写原版剪贴板 → 同样清空 mod 剪贴板 |

→ 规则：**最后复制者拥有剪贴板**。修这个之前出现过「原版复制后 `Ctrl+V` 仍粘 mod 的旧内容」✗。

### 3.5 粘贴（`Ctrl+V` / `Ctrl+Shift+V`）

1. **排队到下一帧**（`QueuePaste` → `Track()` 里 `FlushPendingPaste()`），与插件其它粘贴一致，不在快捷键回调里写谱面。
2. **锚点**：全剪贴板中**节拍最小**的节点；同拍并列（容差 `1e-4`）时取 **x 最大**者（原版口径）。
3. **画布偏移** `offset = 目标画布 − 源画布`（源画布＝复制那一刻的 `SelectedCanvas`），每个节点 `canvasIndex += offset`；Hold 的 `otherInformations[1]`（结束位置所用画布）**同步偏移**；若任一新画布在 `canvasMoves[].index` 中不存在 → **放弃粘贴并提示**（不写半有效数据）。
4. **时间**：所有节点 `time += (鼠标拍 − 锚点拍)`；音符时刻、Hold 结束拍（`otherInformations[0]`）、`lineColor`/`judgeRingColor` 的关键点时刻**一起平移**。
5. **x**：所有节点 `xPosition += (鼠标局部 x 吸附后 − 锚点 xPosition)`；镜像粘贴则改为**以鼠标为对称轴反射** `x' = 目标 − (x − 锚点 x)`（等价于原版「复制时取负 + 粘贴对齐鼠标」两步合成的结果）。
6. **写入**：`EditControl.Stepping`（单步撤销）内 `chart.lines.Add(...)` + `project.editorControl.SetFloorPosition()`（派生位置重算）。**不再**用 `IncOrDecCanvasIndex`/`ChangeCanvasIndex` 强制刷新——那会改动画布聚焦甚至失败；改为 0.5 秒后**只读复核**渲染分组是否跟上，仅在确实没跟上时记一行提示。

### 3.6 吸附与坐标换算（最容易踩坑的部分）

| 量 | 做法 | 依据 |
| --- | --- | --- |
| 鼠标局部 x | `局部原值 = (鼠标世界x − 截距) / 斜率` | **实测映射**：从同一画布上两个已渲染节点反解（`斜率 = Δ世界x / ΔxPosition`）。实测**斜率 = 5.625**，而插件原先沿用的单位常数 **5.5625 偏约 1.1%** ✗——这是长期「差半格」残差的根源，已不再使用该常数 |
| 基础格距 | 从编辑器**实际画出的** `SnapLines/SnapLine(Clone)` 量取：世界间距 **0.5625** → 局部 **0.1** | 用实测斜率换算，不依赖任何属性 |
| 吸附步长 | **基础格距 ÷ 水平细分**（`ProjectInformation.snapHLinesSplit.value`，值 2 显示为「1/2」→ 步长 0.05） | 佐证：场景里另有一条 `SnapLine / Line` 落在世界 `−0.28125`，正好是 0 与 −0.5625 的**中点**（即 1/2 细分线）；`snapLinesSplit = 1`（节拍不细分）也与「整数拍吸附」吻合 |
| 拍吸附 | `EditorOperations.GetCanvasPasteAnchor`（内含 `SnapLinesSplitCount` 垂直细分吸附） | 与画布事件粘贴同一口径（`docs/features/CANVAS_EVENT_CLIPBOARD.md`） |

> 结论：`snapHLinesSplit`/`snapLinesSplit` 的值是**对基础格的细分数**（1＝基础格本身、2＝二等分、4＝四等分），不是步长本身。

## 4. 迭代中的失败记录（避免重犯）

1. **按 `Ctrl+C` 没有十字架** → 只调用了 `SelectLines(...)`，忘了 `StartCoroutine`。
2. **没有高亮** → 用了被裁剪的 `LineInformation.SetColors`（抛 `Il2CppException`）；且原版每帧重写线色，一次覆盖留不住 → 改直接写 `LineRenderer` + 逐帧重设。
3. **高亮时机错** → 先做成「选完仍然高亮」，使用者的要求是**拖动过程中**高亮、选完即结束。
4. **把其它画布的线也选进来** → 命中判定必须**限定当前画布**的节点。
5. **原版流速缩放后第一次框选错乱** → 拖框起点要按 `_scale_5__3` 还原到屏幕空间。
6. **连续粘贴后画布聚焦改变 / 一次「切换画布聚焦失败」** → 强制刷新（`IncOrDecCanvasIndex`）的副作用，已删除。
7. **原版复制后 `Ctrl+V` 粘到 mod 旧内容** → 剪贴板归属规则（见 3.4）。
8. **x 精度反复不达标** → 依次排查：画布是**相对定位**还是绝对（实测为「画布按播放头定位、节点用画布局部坐标」）→ 网格是局部还是绝对（实测：画出的线固定在世界坐标上）→ 单位常数（实测 5.625，旧值 5.5625 偏 1.1%）→ 最后落到「实测映射 + 基础格距 ÷ 细分」。

## 5. 手动验收步骤

1. 线编辑里先点一下画布 → `Ctrl+C` → **按住 `Shift`** 拖框框住某条跨画布线的节点：拖动中该线**实时高亮**（其它画布的线不会被顺带选中），松开鼠标后高亮消失、日志出现「已选中并复制 N 条线…其中跨画布 M 条」。
2. 把鼠标移到别处 → `Ctrl+V`：整条线按**画布偏移 0** 粘贴，锚点落在鼠标处并按水平细分吸附；`Ctrl+Shift+V` 则为以鼠标为对称轴的镜像。
3. 切换显示画布 → `Ctrl+V`：各节点画布**整体偏移**（日志写 `画布偏移 +N`）。
4. 不按 `Shift` 的 `Ctrl+C/V` 与原版完全一致；原版复制后 `Ctrl+V` 应粘原版内容（日志会有「跨画布剪贴板已清空…」）。
5. 断言无副作用：不按 Shift 时零干预；`Esc`/离开线工具会清高亮；单步 Undo 撤销一次粘贴。

## 6. 风险与已知边界

- 依赖原版对象名不变量：`LineEditor._Start_b__28_3/5/8/9`、`LineInformation.startLineTransform/endLineTransform/lineData`、`SnapLines/SnapLine(Clone)`、`snapHLinesSplit`。改名会退化到「兜底换算」或直接不吸附（有日志）。
- **Shift 剪切**目前按复制处理（不删除原线），日志会注明。
- 跨画布复制**不写系统剪切板**（原版路径的导出不受影响）。
- `floorPosition` 不手工换算，交给 `SetFloorPosition()` 重算。
- 单位换算依赖「同一画布上至少有两个不同 x 的已渲染节点」；不满足时退回旧口径（日志注明）。
