# 编辑器 X/Y 轴与坐标参考（交接给下一个任务）

最后更新：2026-11-01

本文只整理 Rizline Editor 中与 **x 轴、y 轴、时间/拍、画布坐标** 有关的事实、公式和已知陷阱，供另一个任务直接接手。它不替代 `docs/CHART_FORMAT.md`（谱面字段语义）和 `docs/features/CROSS_CANVAS_LINES.md`（跨画布线的实现细节）。

## 1. 先看这 8 条

1. **谱面数据没有统一的 x/y 两轴**：`time` 是拍数（时间轴），`xPosition` 是**节点所属画布局部坐标系的横坐标**，`canvasIndex` 是画布编号（不是深度/世界 y）。
2. **画布按播放头定位**：编辑器把整张画布放在“当前播放头拍”的横向位置上，节点用画布局部 `xPosition` 绘制；所以“世界 x”= 画布在播放头的 frame + 局部 x 的缩放。
3. **线节点的 x 映射要用实测斜率**：同一画布两个已渲染节点反解，实测约 **5.625 世界单位 / 局部 x 单位**；旧常数 **5.5625** 对画布事件仍在使用，但对线节点约偏 1.1%。
4. **基础横向格距**：世界 **0.5625** = 局部 **0.1**；横向吸附步长 = `0.1 ÷ 水平细分数`。
5. **`snapHLinesSplit` / `snapLinesSplit` 的值是“细分数”**（1=基础格、2=二等分、4=四等分），不是步长；UI 显示 `1/2` 时内部值通常是 `2`。
6. **y 轴有两套时间量**：`ProjectInformation.nowTime` 是**秒**；`nowChartTime` 是**拍**。鼠标到拍还要经过 `ProjectInformation.speed`、`108 px/秒`、相机和 `RealTimeRangeToChartTime`。
7. **跨画布粘贴时 x 与 canvasIndex 分开处理**：`xPosition` 平移/镜像；`canvasIndex` 整体加 `目标画布 − 源画布`；Hold 的结束时间、结束画布和颜色关键点一起跟随。
8. **不要按字段名猜方向**：摄像机向右移动时画面内容向左，这是相机参考系；JSON 的正方向、屏幕正方向和画布局部正方向不是同一个东西。

## 2. 可信状态

沿用项目规则：

- `confirmed`：代码、运行实验或多个直接证据确认。
- `likely`：强间接证据，尚未完全验证。
- `uncertain`：有合理猜测，但证据不足。
- `unknown`：目前不知道。

本文绝大多数“字段存在/代码路径”是 `confirmed`；“方向符号、精确合法范围、floorPosition 例外”多数是 `likely`/`uncertain`，不要当成完整格式规范。

## 3. 轴的总体图

```text
                     时间/拍轴（数据 time）
                              │
                              │  RealTimeToChartTime / ChartTimeRageToRealTime
                              ▼
 屏幕 y（Input.mousePosition）──相机 ScreenToWorldPoint──► 世界 y
                                                              │
                                                              │ 画布/线/音符的垂直绘制
                                                              ▼
                     横向：世界 x ◄── canvasFrame + xPosition × slope
                                        ▲
                                        │
                             画布 frame（由播放头 + canvasMoves 决定）
                                        ▲
                                        │
                              xPosition（画布局部横坐标）

  canvasIndex：每个节点的“所属画布”，不是 x/y 数值；决定用哪个画布 frame。
  floorPosition：速度加权的累计时间/滚动坐标，不是世界 y，也不是普通拍数。
```

## 4. 谱面数据里的轴相关字段

来源：`docs/CHART_FORMAT.md` 的字段表；结构由 `research/STAGE5_IL2CPP_METADATA.md` 的 `M1` 恢复。

### 4.1 `lines[].linePoints[]`

| 字段 | 业务轴 | 单位/含义 | 状态 |
|---|---|---|---|
| `time` | 时间轴 | 节点所在拍数 | `likely`（名称和样例；实际加载/保存一致） |
| `xPosition` | 横向 | **节点所属画布坐标系中的局部横坐标**，样例 -1.85–1.95 | `likely`；跨画布实验支持 |
| `canvasIndex` | 画布轴 | 节点使用的画布编号，样例 0–35；不是世界 y | 引用关系 `confirmed`，坐标行为 `likely` |
| `color` | 非空间 | 线节点基础/初始颜色，可形成渐变 | `confirmed` |
| `easeType` | 非空间 | 当前点到下一点的缓动/线形 | `confirmed` |
| `floorPosition` | 时间/滚动轴 | 速度加权的累计位置；不是普通拍数，也不是 y | 语义 `unknown`；与速度积分相关 `likely` |

关键约束：

- 节点数组**不保证按 `time` 递增**（样例中真实存在 116.0 后接 115.0）；兼容工具**不要自动排序**，连线按数组顺序。
- 一段线属于**左端节点的画布**（`C1`），所以同一线跨画布时，段的画布可能与右端点 `canvasIndex` 不同。
- 线、判定环、音符接近画布可视范围左右边缘会淡出；大约 `abs(xPosition)=0.4` 开始，中心越过约 `0.5` 完全不可见（精确函数/纵向阈值 `uncertain`）。这是渲染淡出，不是颜色 Alpha。

### 4.2 `lines[].notes[]`

| 字段 | 业务轴 | 含义 | 状态 |
|---|---|---|---|
| `type` | 非空间 | 0 Tap / 1 Drag / 2 Hold | `confirmed` |
| `time` | 时间轴 | 音符开始拍 | `likely` |
| `floorPosition` | 时间/滚动轴 | 可能对应音符开始位置；不是世界 y | `unknown` |
| `otherInformations` | 混合 | Tap/Drag 为空；Hold 三项 | 结构 `confirmed`，语义 `likely` |

Hold 的三项当前结论（`H1`、`C1`、`F1`）：

```text
otherInformations = [endTime, endCanvasIndex, endFloorPosition]
```

- `[0]` 结束拍；`[1]` 用于结束位置计算的画布编号（取严格早于 `endTime` 的最后一个线节点画布）；`[2]` 结束点的 `floorPosition`。
- Hold 没有缓动选项，形状始终垂直于 `y=0` 的判定线；跨多个线节点时显示高度随节点速度改变。

### 4.3 `canvasMoves[]`

| 字段 | 业务轴 | 含义 | 状态 |
|---|---|---|---|
| `index` | 画布轴 | 画布编号，必须与 `linePoints[].canvasIndex` 对应 | 引用关系 `confirmed` |
| `xPositionKeyPoints[].time` | 时间轴 | 画布横向位置变化拍 | `likely` |
| `xPositionKeyPoints[].value` | 横向（绝对） | 画布在固定参考系中的横向位置；不是节点局部 x | `likely` |
| `xPositionKeyPoints[].easeType` | 非空间 | 到下一节点的横向运动缓动 | `confirmed` |
| `speedKeyPoints[].value` | 时间/滚动 | 画布流速；样例可为负、可达 999 | `likely` |
| `speedKeyPoints[].time` | 时间轴 | 流速改变拍 | `likely` |
| `speedKeyPoints[].floorPosition` | 时间/滚动 | 疑似速度积分结果 | `unknown` |

- 画布位置会带动它的坐标系和节点；节点的纵向位置由速度随时间的积分决定（`U1`，`likely`）。
- `canvasMoves[].xPositionKeyPoints[].value` 是**画布 frame 的绝对位置**；节点 `xPosition` 是相对这个 frame 的局部值。画布事件的 `value` 也是绝对画布坐标，粘贴时用 `worldX / 5.5625` 反算，不要再减画布 frame。
- 样例的 `canvasIndex` 连续从 0 开始，但这只是样例事实，不能断言格式强制连续。

### 4.4 `cameraMove`

| 字段 | 业务轴 | 含义 | 状态 |
|---|---|---|---|
| `scaleKeyPoints[].value` | 缩放 | 摄像机缩放倍数；编辑器可输入 0.1–10（样例只覆盖 1–3） | `likely` |
| `scaleKeyPoints[].time` | 时间轴 | 缩放变化拍 | `likely` |
| `xPositionKeyPoints[].value` | 横向（相机） | 摄像机横向位置 | `likely` |
| `xPositionKeyPoints[].time` | 时间轴 | 横向移动拍 | `likely` |
| `floorPosition` | 时间/滚动 | 多为 0；语义 unknown | `unknown` |

- **方向陷阱**：摄像机向右移动，画面内容相对摄像机向左。不要直接根据屏幕内容方向判断 JSON 的 x 正方向。
- 摄像机缩放改变可见坐标范围、判定圈和音符大小，但不改变线宽。

### 4.5 `floorPosition`

在 BPM 和速度不变的区间：

```text
ΔfloorPosition = Δbeat × 60 / BPM × speed
```

- `speed = 1` 时等于拍数换算的秒数；
- 速度不为 1 时是速度加权的等效秒数/滚动坐标；
- 有效 BPM：`effectiveBPM = bPM × currentBpmShift.value`；
- 单次 BPM 变化采用分段积分（`P1`，30/30 在 1e-6 内）。
- 少量锚点/重置例外，**不要批量删除、归零或自行重算**；统一交给编辑器的 `SetFloorPosition()`。

## 5. 坐标空间

| 空间 | 原点/方向 | 用途 | 关键事实 |
|---|---|---|---|
| 屏幕/输入 | Unity `Input.mousePosition`：左下为原点，x 右、y 上 | 鼠标、框选 | `ScreenToWorldPoint` 转世界；UI 缩放会混入 |
| 世界 | 编辑器相机正交世界；x 右、y 上（渲染上 `y<0` 在判定线下方） | 线、画布、相机、粘贴 | `LineRenderer` 的 `startLineTransform.x` 等可直接测 |
| 画布局部 | 以当前画布 frame 为参考；`xPosition` 局部 | 线节点、音符横向 | `canvasIndex` 选择 frame |
| 画布/时间 | 画布 frame = `canvasX(playhead)` | 线/音符整体随播放头定位 | 不是 `canvasX(target beat)` |
| UI/RectTransform | Canvas 的锚点/轴心/`scaleFactor` | 工具栏、面板、颜色预览 | `ScreenSpaceCamera`；`scaleFactor=1.05`（探针） |
| 数据轴 | `time` 拍、`xPosition` 局部、`canvasIndex` 编号、`floorPosition` 滚动 | JSON | 不要混成世界 x/y |

补充：

- **世界 x = 0** 是编辑器大地/判定中心的横向基准。颜色编辑用它做路由：框选整体在 x=0 左侧只取线色覆盖，整体在右侧只取判定环，跨过 x=0 两者都取（D-019）。这不是谱面字段。
- **屏幕 x/y** 与 **世界 x/y** 都是“x 右、y 上”（Unity 约定）；画布局部 `xPosition` 的正方向与画布 frame 相加后进入世界 x，**正负号以实测映射为准**。摄像机横移会让画面内容反向移动，不能拿屏幕方向反推相机 JSON 的正方向。
- **拍数增加方向与屏幕 y 的严格对应**尚未单独做成实验结论；渲染到判定线以下的像素 `y<0` 有观察证据，但“拍增加一定向下/向上”不要在交接代码里硬编码，除非原版状态机/实验已确认。

### 5.1 画布 frame 与节点世界 x

对一条画布 `c` 上的节点，当画布在播放头拍的绝对位置为 `frame(c, playhead)`：

```text
worldX ≈ xPosition × slope + frame(c, playhead)
```

- `slope` 用同一画布上两个已渲染节点反解：`slope = ΔworldX / ΔxPosition`；现场实测约为 **5.625**。
- `intercept` 是 `worldX - slope × xPosition`；当 frame 为 0 时通常接近 0。
- 编辑器实际的绘制基准可从 `LineInformation.startLineTransform.x` 取值，不要只靠常数。
- **画布 frame 的确定**：优先从已渲染的线节点反算；退回到 `canvasMoves[].xPositionKeyPoints` 的缓动求值。`TryGetCanvasFrame` 使用**播放头**的 frame，不是目标拍。
- 代码参考：`CrossCanvasLineClipboard.TryMeasureCanvasMapping`、`TryGetCanvasFrame`、`TryGetRenderedCanvasFrame`。

### 5.2 鼠标局部 x（跨画布线粘贴）

```text
localRaw = (mouseWorld.x − intercept) / slope
```

- 映射不可用时退化为：

```text
localRaw = mouseWorld.x / 5.5625 − canvasFrame
```

- 然后按横向步长吸附（见第 7 节）。
- 这里的 `5.5625` 是旧单位常数，仅作为兜底；实测映射优先。

### 5.3 鼠标到拍（画布事件/时间轴）

`EditorOperations.GetCanvasPasteAnchor` 复现原版：

```text
mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition)

targetRealTime = nowTime + (mouseWorld.y + 2.4) / project.speed
beatDelta      = RealTimeRangeToChartTime(nowTime, targetRealTime)
rawBeat        = nowChartTime + beatDelta
targetBeat     = max(0, SnapToSubdivision(rawBeat, SnapLinesSplitCount))
```

- `nowTime`：秒；`nowChartTime`：拍。
- `2.4`：编辑器时间轴/鼠标世界 y 的固定偏移（`EditorOperations.EditorTimelineWorldYOffset`）。
- `project.speed`：编辑器垂直缩放/速度系数；无穷小会拒绝。
- `RealTimeRangeToChartTime` 是原版时间换算；不要自己用 `60/BPM` 直接替代，除非确认 BPM 变化和 delay。

### 5.4 框选鼠标 y（流速/画布/颜色等）

`SpeedClipboardController.CompleteSelection` 的实测公式：

```text
secondRealTime = mouseY × state._scale / 108 / project.speed + state._yOffset
secondBeat     = chart.RealTimeToChartTime(secondRealTime)
```

- `108`：`SelectionPixelsPerRealSecond`，经验像素/秒换算。
- `state._scale`：框选协程里的 UI 缩放；`state._yOffset`：原版框选协程的纵向偏移；`state._startPositionY`：起点拍。
- 这与 5.3 的 `worldY + 2.4` 是两条不同路径：前者是原版框选状态机，后者是画布/事件粘贴锚点。不要互换。

### 5.5 屏幕/UI 与 `_scale` 陷阱

- 框选起点 `_selectionStart_5__2` 存储在被 `_scale` 缩放过的坐标里；与渲染屏幕坐标比较前要：

```text
start /= state._scale
```

- 当 `_scale == 1` 时是空操作，所以问题只在缩放/流速改变后暴露。
- `RectTransform` 通过 interop 直接 `transform as RectTransform` 可能返回 `null`；必须用项目的 `CustomToolbarTool.FindComponent<RectTransform>` / `RectOf`。
- `InputControl` 的四个 `Nullable<Vector2>` 鼠标字段在本构建不可靠（`.Value` 丢分量、`.value/.hasValue` 可能全零）；从 `Input.mousePosition` 和 `_camera.ScreenToWorldPoint` 自己算。

## 6. 时间轴：秒 vs 拍

- `ProjectInformation.nowTime`：当前播放头的**秒**。
- `ProjectInformation.nowChartTime`：当前播放头的**拍**。
- `chart.RealTimeToChartTime(realTime)`：秒 → 拍。
- `chart.RealTimeRangeToChartTime(start, end)`：秒区间 → 拍差。
- `chart.ChartTimeRageToRealTime(start, end)`：拍区间 → 秒（原版拼写 `Rage` 不是笔误）。
- `chartDelayMs`：谱面相对音乐的时间偏移，毫秒；可为负（`likely`）。
- `bPM` / `bpmShifts`：有效 BPM 和分段积分；见第 4.5 节。
- `canvasMoves[].speedKeyPoints[].value`：画布流速，影响 `floorPosition` 和纵向显示，不是普通 x/y 缩放。

**注意**：`nowTime` 和 `nowChartTime` 是两套单位；修改一个不一定自动同步另一个。插件在切换工程时曾尝试同时归零，但末尾索引问题仍未完全解决（不属于本参考文档）。

## 7. 吸附规则

### 7.1 通用离散吸附

`CanvasEventEditing.SnapToSubdivision`：

```text
result = round(value × subdivision, MidpointRounding.ToEven) / subdivision
```

- 不是简单的 `Math.Round(value, n)`；细分值决定栅格。
- `subdivision <= 0` 抛异常。

### 7.2 横向（x）吸附

- **基础格距**：世界 `0.5625` = 局部 `0.1`。
- `snapHLinesSplit.value` 是**细分数**：1=基础格，2=二等分，4=四等分；UI 显示 `1/2` 时内部值通常为 2。
- 对线节点（跨画布粘贴）：

```text
baseStepLocal = 0.5625 / slope      // measured ≈ 0.1
stepLocal     = baseStepLocal / subdivision
snapCount     = max(1, round(1 / stepLocal))
snappedX      = SnapToSubdivision(localRaw, snapCount)
```

- 例：subdivision=2 → step=0.05；subdivision=4 → step=0.025。
- 代码参考：`CrossCanvasLineClipboard.HorizontalSubdivision`、`BaseGridStepLocal`。

### 7.3 纵向（拍）吸附

- `ProjectInformation.SnapLinesSplitCount`：垂直细分；垂直吸附关闭时返回 1。
- 数值来源是 `snapLinesSplit` UI 值，`SnapLinesSplitCount` 会取整；不要把它当成“每拍像素”。
- 画布事件粘贴使用 `SnapLinesSplitCount` 吸附 `rawBeat`；线/音符/颜色也按同一口径。
- `VerticalSnapDensity` / `verticalSnapDensityWithoutSnapLine` 是另一个 UI/网格密度状态；`GetCanvasPasteAnchor` 却把 `VerticalSnapDensity` 当作**画布事件横向细分**使用（见第 9 节），跨画布线则优先用 `snapHLinesSplit`。不要把两者混为一谈。

## 8. 跨画布线的坐标行为

来源：`docs/features/CROSS_CANVAS_LINES.md`、`CrossCanvasLineEditing.cs`。

### 8.1 选择

- 原版框选只看 **Y 轴/拍**；插件 `Shift` 跨画布模式额外用渲染屏幕坐标判断**当前画布**节点是否**两轴都落在矩形内**。
- 命中即整条线（含其它画布节点）；其它画布上碰巧在矩形内的线不选。

### 8.2 粘贴锚点

- 全剪贴板中**节拍最小**的节点；同拍并列取 **x 最大**者。
- 时间平移：`Δtime = targetBeat − anchor.Time`。
- 普通粘贴 x：`x + (targetX − anchor.XPosition)`。
- 镜像粘贴 x：

```text
x = targetX − (point.XPosition − anchor.XPosition)
```

- 画布：每个节点 `canvasIndex += targetCanvas − sourceCanvas`；Hold 的 `otherInformations[1]` 同步偏移；目标画布不存在则**整体放弃**。

### 8.3 颜色关键点 / Hold

- 颜色关键点 `time` 随 `Δtime` 平移；颜色值不随 x 镜像（原版镜像只改 x）。
- Hold 结束拍 `other[0] += Δtime`；结束画布 `other[1] += canvasOffset`；结束 `floorPosition` 由编辑器重算，不手工算。
- 写入后调用 `SetFloorPosition()`；一次粘贴/剪切放在一个 `Stepping` 内。

## 9. 常数、单位和已知不一致

| 量 | 值/来源 | 说明 |
|---|---|---|
| `SelectionPixelsPerRealSecond` | `108f` | 框选鼠标 y 的经验像素/秒 |
| `CanvasEditorWorldUnitsPerChartUnit` | `5.5625f` | 画布事件 `worldX/value`；对线节点旧常数 |
| 实测线 x 斜率 | ≈ `5.625` | 同一画布两个渲染节点反解；比 5.5625 大约 1.1% |
| 基础格距 | `0.5625` 世界 / `0.1` 局部 | 由 `SnapLines/SnapLine(Clone)` 实测 |
| 时间轴鼠标 y 偏移 | `2.4` | `EditorOperations.EditorTimelineWorldYOffset` |
| 整体缩放范围 | 40%–200%，步长 10% | `ZoomStepping`；`Ctrl+Shift+滚轮`；`Ctrl+M` 复位 |
| 相机投影 | `halfHeight/factor`、`halfWidth=halfHeight×aspect` | `EditorNavigation.WriteHorizontalProjection` |
| UI Canvas | `ScreenSpaceCamera`，`scaleFactor=1.05` | 探针；具体 UI 层级以场景为准 |

**5.5625 vs 5.625 是最容易踩的坑**：

- 画布事件（`CanvasEditor.Update`）仍使用 `5.5625`；
- 线节点/跨画布粘贴使用**实测映射**（约 `5.625`）；
- 两者不能无脑互换；如果新任务只做线，优先实测；如果只做画布事件，先确认原版路径。

**`VerticalSnapDensity` 的双重角色**：

- `EditorOperations.GetCanvasPasteAnchor` 用它作为画布事件的**横向细分**；
- `CrossCanvasLineClipboard.HorizontalSubdivision` 主要用 `snapHLinesSplit`，只在读取失败时回退 `VerticalSnapDensity`；
- 写跨工具逻辑时不要假设二者语义相同。

### 9.1 播放流速（显示值）↔ `project.speed`（2026-11-01，`confirmed`）

`ProjectInformation.set_RelativeSpeed`（token `0x0600013D`，RVA `0x2BEBE0`）的反汇编直接给出换算：

```asm
cmp  ebx, 0xfffffff7        ; 钳制下限 -9
cmp  ebx, 0x12              ; 钳制上限 18
mov  [rdi+0x17c], ebx       ; RelativeSpeed（int，偏移 0x17c）
cvtdq2ps xmm0, ebx
mulss xmm0, 0.5             ;   rel × 0.5
divss xmm0, 9.0             ;   ÷ 9
mulss xmm0, 7.0             ;   × 7
addss xmm0, 6.0             ;   + 6
movss [rdi+0x180], xmm0     ; ProjectInformation.speed（float，偏移 0x180）
```

显示值 S0（设置界面/状态栏里的「播放流速」）是**分段**的：

| 区间 | 显示值 | `speed`（记作 S1） |
| --- | --- | --- |
| `rel ≥ 0`（S0 ≥ 1） | `S0 = 1 + rel / 2` | `S1 = 7·rel/18 + 6` = **`7(S0 − 1)/9 + 6`** |
| `rel < 0`（S0 < 1） | `S0 = 1 + rel / 10` | `S1 = 7·rel/18 + 6` = `3.889(S0 − 1) + 6` |

数值（实测代入验证过换算一致性）：

| S0 | rel | S1 |
| --- | --- | --- |
| **1.5（默认）** | 1 | **6.3889** |
| 2.0 | 2 | 6.7778 |
| 1.0 | 0 | 6.0 |
| 0.5 | −5 | 4.0556 |
| 0.1（原版下限） | −9 | 2.5 |
| −0.5（插件放宽后） | −15 | ≈ 0.1667（仍 > 0，故耦合的编辑器缩放保持有效） |

几何关系（缩放 = 1 时，`orthographicSize = 5` → 可见高 10 世界单位）：

```text
Δ秒 = Δ世界y / S1                    // 即 Δ世界y = S1 × Δ秒
每屏秒数 = 2 × orthographicSize / S1 = 10 / S1
每屏拍数 = (10 / S1) × 有效BPM / 60
S0 = 1.5、BPM 175  →  10/6.3889 = 1.565 s  →  ≈ 4.56 拍/屏
```

`floorPosition` 已经包含**画布流速**的积分，所以「可视高 = S1 × floorPosition」只在画布流速 = 1 时成立；
否则应为 `世界y = S1 × floorPosition / speed_canvas`。

> `NoteSpeedLimit` 改的两个立即数正是这里的 `cmp ebx,-9` 与 `mov ebx,-9`（偏移 `0x32` / `0x42`），
> 所以把下限放宽到 −15 后 `S1` 仍为正（0.167）✓。

## 10. 工具/代码入口对照

| 主题 | 入口 |
|---|---|
| 画布事件粘贴锚点 | `EditorOperations.GetCanvasPasteAnchor`（`worldY+2.4`、`5.5625`、`VerticalSnapDensity`、`SnapLinesSplitCount`） |
| 跨画布线 x 映射 | `CrossCanvasLineClipboard.TryMeasureCanvasMapping` / `TryGetCanvasFrame` / `BaseGridStepLocal` / `HorizontalSubdivision` |
| 框选 y 换算 | `SpeedClipboardController.CompleteSelection`（`108`、`_scale`、`_yOffset`） |
| 通用吸附 | `CanvasEventEditing.SnapToSubdivision` |
| 跨画布放置/镜像/画布偏移 | `CrossCanvasLineEditing.TryPlace` |
| 整体缩放投影 | `EditorNavigation.WriteHorizontalProjection`（`halfHeight/factor`） |
| 数据字段语义 | `docs/CHART_FORMAT.md` |
| 跨画布复制/剪切/粘贴 | `docs/features/CROSS_CANVAS_LINES.md` |
| 画布事件剪贴板 | `docs/features/CANVAS_EVENT_CLIPBOARD.md` |
| 线/判定环颜色路由 x=0 | `docs/DECISIONS.md` D-019、`SpeedClipboardController.ClassifyColorSelection` |

## 11. 交接时最容易犯的错误

1. 把 `canvasIndex` 当世界 y 或深度；它只选画布 frame。
2. 把 `xPosition` 当世界 x；它是画布局部 x。
3. 用 `canvasX(targetBeat)` 作为画布 frame；实际绘制用 `canvasX(playhead)`。
4. 用 `5.5625` 算线节点 x；应用实测 `5.625`（或直接从渲染节点反解）。
5. 把 `snapHLinesSplit` 的值当步长；它是细分数，步长是 `0.1/值`。
6. 把 `snapLinesSplit` 和 `VerticalSnapDensity` 混用；它们在不同路径承担不同角色。
7. 用 `nowTime` 当拍，或把 `nowChartTime` 当秒。
8. 对线节点数组按 `time` 排序；数组顺序就是连线顺序，样例存在倒序。
9. 认为摄像机 x 正方向 = 屏幕内容右移；相机参考系是反的。
10. 直接 `transform as RectTransform`；本构建 interop 会返回 `null`，用 `FindComponent`。
11. 读取 `InputControl` 的 `Nullable<Vector2>` 鼠标字段；会丢分量/全零。
12. 用 `floorPosition` 当普通拍数或自行重算；它受速度/BPM 分段积分影响，交给 `SetFloorPosition()`。

## 12. 仍然不确定/需要实验的问题

- **屏幕 y 正方向与拍增加方向的严格对应**：世界 y 正方向向上、`y<0` 在判定线下方有证据；但“拍增加一定向下/向上”的完整符号关系尚未单独写成实验结论。
- **`VerticalSnapDensity` 的完整语义**：它既出现在画布事件横向细分，又作为线横向吸附的 fallback；尚未确认原版 UI 的准确命名/单位。
- **`5.5625` 在画布事件之外的使用范围**：摄像机 x 是否同一常数、缩放后是否仍成立，尚未逐项验证。
- **`floorPosition` 的锚点/重置例外**：速度关键点、跨 BPM、同刻变化的边界规则仍不完整。
- **`linePoints[].time` 是否始终是拍、是否受 `chartDelayMs` 影响**：名称和样例支持，但未做专门的时间基准实验。
- **画布/线淡出的精确阈值与函数**：`0.4/0.5` 是观察值，不是完整公式。
- **摄像机缩放/横移的完整屏幕→世界公式**：现有公式用于正交投影；非 100% 缩放、aspect 变化和 UI Overlay 搬移时需重新验证。
- **`cameraMove.scaleKeyPoints` 与整体缩放（插件 40%–200%）的关系**：两者都影响可见范围，但不是同一个量。

## 13. 证据与源码索引

- `docs/CHART_FORMAT.md`：`linePoints`、`notes`、`canvasMoves`、`cameraMove`、`floorPosition` 的字段/证据。
- `docs/features/CROSS_CANVAS_LINES.md`：选择、锚点、x 映射、5.5625/5.625、基础格距、画布偏移。
- `docs/features/CANVAS_EVENT_CLIPBOARD.md`：画布事件 `worldY+2.4`、`5.5625`、`VerticalSnapDensity`。
- `docs/features/CANVAS_SPEED_EDITING.md`：`SnapLinesSplitCount`、流速采样。
- `src/RhythmEditorMod/EditorOperations.cs`：`CanvasEditorWorldUnitsPerChartUnit=5.5625`、`EditorTimelineWorldYOffset=2.4`、`GetCanvasPasteAnchor`。
- `src/RhythmEditorMod/CrossCanvasLineClipboard.cs`：`TryMeasureCanvasMapping`、`TryGetCanvasFrame`、`BaseGridStepLocal`、`HorizontalSubdivision`。
- `src/RhythmEditorMod/SpeedClipboardController.cs`：`SelectionPixelsPerRealSecond=108`、框选 y 换算。
- `src/RhythmEditorMod.Core/CanvasEventEditing.cs`：`SnapToSubdivision`。
- `src/RhythmEditorMod.Core/CrossCanvasLineEditing.cs`：`TryPlace`、锚点、镜像、画布偏移。
- `src/RhythmEditorMod/EditorNavigation.cs`：`WriteHorizontalProjection`。
- `docs/DECISIONS.md` D-019（颜色 x=0 路由）、D-030（跨画布）、D-031（整体缩放）。

## 14. 给下一个任务的最小阅读顺序

1. 本文第 3–5 节（空间/轴）。
2. `docs/CHART_FORMAT.md` 的 `linePoints` / `canvasMoves` / `floorPosition`。
3. `docs/features/CROSS_CANVAS_LINES.md` 第 3.5–3.6 节。
4. `src/RhythmEditorMod/CrossCanvasLineClipboard.cs` 的 `TryMeasureCanvasMapping`、`TryGetCanvasFrame`。
5. 再改代码；任何坐标改动都要用“同一画布两个已渲染节点反解”先量斜率，而不是先套常数。
