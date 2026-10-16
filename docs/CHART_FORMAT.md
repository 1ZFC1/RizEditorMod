# 谱面 JSON 格式

本文档记录 Rizline Editor 谱面 JSON 的已知结构、语义、证据和未知事项，是阶段 4 的核心成果。项目现已进入阶段 5；本文覆盖现有 18 个样例、用户人工勘察、已提取的 Unity 场景字符串和多轮隔离运行/保存实验，但仍不是完整格式规范。

## 可信状态

- `confirmed`：由代码、可控运行实验或多个直接证据确认。
- `likely`：有较强间接证据或用户操作观察，但尚未形成可重复验证。
- `uncertain`：存在合理推测，但证据不足或有其他解释。
- `unknown`：当前不知道。

结构、类型和样例取值可以是 `confirmed`，同时字段的作用、单位或合法范围仍可能是 `unknown`。

## 证据来源

- `S18`：`#Samples/` 下 18 个现有 JSON 的只读静态统计。
- `U1`：用户人工勘察记录 `baseline/json文件各项含义.txt`。
- `U2`：用户于 2026-08-28 明确确认音符类型编号和主题颜色数组下标。
- `U3`：用户于 2026-08-28 根据字段命名补充 `judgeRingColor` 与 `lineColor` 的基本角色判断。
- `U4`：用户于 2026-08-28 补充实际颜色渲染行为，包括初始颜色、判定环存在条件、覆盖优先级和 Alpha 约束。
- `U5`：用户于 2026-08-28 确认编辑器没有 Hold 缓动选项，并描述 Hold 的方向和跨节点速度行为。
- `U6`：用户于 2026-08-29 补充谱面对象接近画面左右边缘时的可见性淡出行为。
- `U7`：用户于 2026-08-29 明确确认 `judgeRingColor` 直接改变判定环 RGBA，而不是作为基础色上的覆盖层，并确认 RGBA 四通道使用线性插值。
- `U8`：用户于 2026-08-29 补充 `judgeRingColor` 的首尾保持、事件间线性变化、同刻突变和判定环与 `y=0` 交点的关系。
- `R1`：`Rizline Editor_Data/level0` 中的 Unity 场景序列化字符串。
- `B1`：`baseline/RUNTIME_BASELINE.md` 中的启动、打开和未编辑另存实验。
- `H1`：`baseline/HOLD_PARAMETER_EXPERIMENT.md` 中的隔离 Hold 结束点实验和静态相关性检查。
- `F1`：`baseline/FLOOR_POSITION_ANALYSIS.md` 中的拍数、BPM、画布速度和 `floorPosition` 公式检查。
- `X1`：`baseline/UNKNOWN_FIELD_EXPERIMENT.md` 中的未知顶层字段读取、实际编辑、保存和自动保存实验。
- `P1`：`baseline/BPM_SHIFT_EXPERIMENT.md` 中的单个 BPM 变化点保存和派生位置重算实验。
- `C1`：`baseline/CROSS_CANVAS_HOLD_EXPERIMENT.md` 中的跨画布 Hold 静态检查和端点移动实验。
- `K1`：`baseline/CHECKPOINT_EXPERIMENT.md` 中的 `checkPoint` 预览、保存和 Riztime 区间编辑实验。
- `G1`：`baseline/COLOR_EVENT_RGB_EXPERIMENT.md` 中的 `lineColor` RGB 插值区间、渲染和保存实验。
- `A1`：`baseline/COLOR_EVENT_ALPHA_EXPERIMENT.md` 中基础 Alpha=255 时的 `lineColor` Alpha 混色、渲染和保存实验。
- `A2`：`baseline/BASE_ALPHA_EXPERIMENT.md` 中基础 Alpha=0/128/255 与 `lineColor.a` 的联合对照实验。
- `D1`：`baseline/LINE_COLOR_BOUNDARY_EXPERIMENT.md` 中 `lineColor` 不连续颜色边界、同刻事件和保存实验。
- `O1`：`baseline/LINE_COLOR_REVERSE_ORDER_EXPERIMENT.md` 中 `lineColor` 倒序数组的渲染和保存实验。
- `T1`：`baseline/LINE_COLOR_SAME_TIME_PRIORITY_EXPERIMENT.md` 中 `lineColor` 同刻事件的边界、优先级和保存实验。
- `J1`：`baseline/JUDGE_RING_BOUNDARY_EXPERIMENT.md` 中 `judgeRingColor` 不连续边界、同刻优先级和保存实验。
- `J2`：`baseline/JUDGE_RING_REVERSE_ORDER_EXPERIMENT.md` 中 `judgeRingColor` 倒序数组的渲染和保存实验。
- `M1`：从当前版本受保护的 IL2CPP 元数据中恢复的 `Chart`、`Theme`、`ChallengeTime`、`Line`、`LinePoint`、`Note`、`CanvasMove`、`CameraMove`、`ColorKeyPoint` 和 `KeyPoint` 类型/字段定义；恢复过程见 `research/STAGE5_IL2CPP_METADATA.md`。
- `E1`：`baseline/SPEED_EASING_PROTOTYPE.md` 中的画布流速离散采样、位置复算、编辑器打开/播放/保存实验。

重要限制：本文中的“观察范围”只是 18 个样例实际出现的最小值和最大值，不表示编辑器允许范围或格式合法范围。

## 当前语料库

- JSON 文件：18
- 根目录 Sample：2
- 随歌曲提供的 Chart：9
- 自动保存：7
- 所有文件均可解析为 JSON。
- 所有文件均具有相同的 9 个顶层字段，顺序也相同。
- 本轮枚举到的对象均包含下文列出的对应字段，没有发现缺失字段或 `null`。

主要对象数量：

| 对象 | 数量 |
|---|---:|
| `themes[]` | 36 |
| `themes[].colorsList[]` | 108 |
| `challengeTimes[]` | 22 |
| `bpmShifts[]` | 18 |
| `lines[]` | 2589 |
| `lines[].linePoints[]` | 9336 |
| `lines[].notes[]` | 4061 |
| `lines[].judgeRingColor[]` | 4095 |
| `lines[].lineColor[]` | 5013 |
| `canvasMoves[]` | 152 |
| `canvasMoves[].xPositionKeyPoints[]` | 4114 |
| `canvasMoves[].speedKeyPoints[]` | 334 |
| `cameraMove.scaleKeyPoints[]` | 39 |
| `cameraMove.xPositionKeyPoints[]` | 24 |

统计可以用以下只读工具重复生成：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Analyze-ChartCorpus.ps1 `
  -ChartRoot .\#Samples
```

`-ExecutionPolicy Bypass` 只作用于这次 PowerShell 进程，不修改系统执行策略。

## 完整结构树

以下树覆盖 `S18` 中实际出现的全部字段：

```text
root
├─ fileVersion: number
├─ chartDelayMs: number
├─ themes: array<object>
│  └─ colorsList: array<Color>
│     ├─ r: number
│     ├─ g: number
│     ├─ b: number
│     └─ a: number
├─ challengeTimes: array<object>
│  ├─ checkPoint: number
│  ├─ start: number
│  ├─ end: number
│  └─ transTime: number
├─ bPM: number
├─ bpmShifts: array<KeyPoint>
│  ├─ time: number
│  ├─ value: number
│  ├─ easeType: number
│  └─ floorPosition: number
├─ lines: array<object>
│  ├─ linePoints: array<object>
│  │  ├─ time: number
│  │  ├─ xPosition: number
│  │  ├─ color: Color
│  │  ├─ easeType: number
│  │  ├─ canvasIndex: number
│  │  └─ floorPosition: number
│  ├─ notes: array<object>
│  │  ├─ type: number
│  │  ├─ time: number
│  │  ├─ floorPosition: number
│  │  └─ otherInformations: array<number>
│  ├─ judgeRingColor: array<ColorEvent>
│  │  ├─ startColor: Color
│  │  ├─ endColor: Color
│  │  └─ time: number
│  └─ lineColor: array<ColorEvent>
│     ├─ startColor: Color
│     ├─ endColor: Color
│     └─ time: number
├─ canvasMoves: array<object>
│  ├─ index: number
│  ├─ xPositionKeyPoints: array<KeyPoint>
│  └─ speedKeyPoints: array<KeyPoint>
└─ cameraMove: object
   ├─ scaleKeyPoints: array<KeyPoint>
   └─ xPositionKeyPoints: array<KeyPoint>
```

JSON 的 `number` 不区分业务上的整数、枚举和浮点数。下文会分别注明。

## 顶层字段

| 字段 | 类型 | `S18` 观察 | 作用 | 语义状态 | 证据 |
|---|---|---|---|---|---|
| `fileVersion` | number，样例为整数 | 全部为 `0` | 很可能是格式版本 | 字段存在 `confirmed`；用途 `likely` | `M1`、字段名；尚无其他版本样例 |
| `chartDelayMs` | number | 全部为 `0` | 谱面相对音乐的时间偏移，单位毫秒；用户观察可为负 | `confirmed`（单位和用途）；负值能力 `likely` | `M1`、`U1`；`R1` 中存在 `Chart Delay (ms)` |
| `themes` | array<object> | 每个文件固定 2 项 | 常规状态和 Riztime/challengeTime 状态的颜色组 | `confirmed` | `M1`、`S18`、`U1`、`U2` |
| `challengeTimes` | array<object> | 每个文件 1–2 项 | Riztime/challengeTime 时间段 | `confirmed` | `M1`、`S18`、`U1`、`K1` |
| `bPM` | number | `105.0`、`150.0`、`180.0`、`195.0` | 歌曲基础 BPM | `confirmed` | `M1`、`S18`、`U1`、场景 BPM 输入文本 |
| `bpmShifts` | array<object> | 每个文件固定 1 项，均为默认值 | BPM 变化序列 | `confirmed` | `M1`、`U1`、`P1` |
| `lines` | array<object> | 每文件 4–537 条 | 谱面的线、节点、音符和颜色事件 | `confirmed` | `M1`、`S18`、`U1` |
| `canvasMoves` | array<object> | 每文件 1–36 项 | 各画布的位置和流速变化 | `confirmed` | `M1` 的 `CanvasMove.index/xPositionKeyPoints/speedKeyPoints`、`S18`、`U1` |
| `cameraMove` | object | 每文件 1 个 | 摄像机缩放和横向位置变化 | `confirmed` | `M1` 的 `CameraMove.scaleKeyPoints/xPositionKeyPoints`、`S18`、`U1` |

字段名大小写必须按原样记录，尤其是 `bPM` 与 `bpmShifts` 的大小写并不一致。尚未验证读取器是否大小写敏感。

## 颜色对象 `Color`

结构：

```json
{"r": 255, "g": 255, "b": 255, "a": 255}
```

| 字段 | 类型 | 作用 | `S18` 观察 | 状态 |
|---|---|---|---|---|
| `r` | number，样例为整数 | 红色通道 | 0–255 | 结构 `confirmed`；合法范围 `likely` |
| `g` | number，样例为整数 | 绿色通道 | 0–255 | 结构 `confirmed`；合法范围 `likely` |
| `b` | number，样例为整数 | 蓝色通道 | 0–255 | 结构 `confirmed`；合法范围 `likely` |
| `a` | number，样例为整数 | Alpha/透明度通道 | 0–255 | 结构 `confirmed`；合法范围 `likely` |

所有主题颜色的 `a` 在现有样例中均为 255；其他颜色对象中可以出现 0–255。尚未验证小数、越界值或缺失通道的处理方式。

**写入注意（2026-09-22 确认）**：在 IL2CPP interop 中，`ColorKeyPoint.startColor` / `endColor` 与 `LinePoint.color` 暴露为 `UnityEngine.Color32`，通道是 **0–255 字节**。构造 `UnityEngine.Color` 再赋值会走 Unity 的隐式转换 `(byte)(Mathf.Clamp01(c) * 255)`，把任何大于 1 的通道钳成 255——即真实颜色会变成纯白。插件侧因此一律直接构造 `Color32`（见 `docs/DECISIONS.md` D-020）。

## `themes`

实际 JSON 形状是：`themes` 有 2 个对象，每个对象包含一个长度为 3 的 `colorsList`。并不是一个对象下直接存在两个名为 `colorsList` 的字段。

`U1` 的人工观察已由 `U2` 明确确认：

- `themes[0]`：常规状态颜色。
- `themes[1]`：Riztime/challengeTime 状态颜色。
- `themes[*].colorsList[0]`：背景色。
- `themes[*].colorsList[1]`：音符色。
- `themes[*].colorsList[2]`：UI 色（打击特效、进度条等）。

上述索引映射当前标记为 `confirmed`。

当 `challengeTimes` 有多个时间段时，`themes` 仍然只有 2 项，因此现有样例更像是多个时间段共享同一套 challenge 颜色，而不是每个时间段各有一套主题。这只是结构推断，状态为 `likely`。

## `challengeTimes`

| 字段 | 类型 | `S18` 观察 | 作用 | 状态 |
|---|---|---|---|---|
| `checkPoint` | number | 0–263 | 业务含义未知；编辑器会读取并原样保存，不随区间联动 | 结构和编辑器保存行为 `confirmed`；业务含义 `unknown` |
| `start` | number | 10–264 | challenge/Riztime 开始拍 | `confirmed` |
| `end` | number | 14–328 | challenge/Riztime 结束拍 | `confirmed` |
| `transTime` | number | 22/22 均为 `0.5` | 颜色过渡时长；用户观察单位为秒，且视觉上自下向上变化 | `likely` |

结构事实：

- 22/22 项满足 `start <= end`。
- 只有 3/22 项满足 `checkPoint == start`。
- Gleam 三张谱面为 `checkPoint=263, start=167`。
- Power Attack 三张谱面为 `checkPoint=0, start=264`。
- 4 个自动保存文件含有两个 challenge 时间段，其余 14 个文件含一个。

`K1` 只把 `checkPoint` 从 0 改为 12 时，第 12 拍没有可见变化，Riztime 仍在第 20 拍开始、第 24 拍结束。通过已知 Hold 编辑触发真实保存后，普通保存和自动保存均原样保留 12。

第二轮通过 UI 把 Riztime 区间从 20–24 改为 10–14；预览按新区间正常开始和结束，但普通保存与自动保存仍保持 `checkPoint=12`，递归比较只有 `start` 和 `end` 变化。

因此当前可以确认编辑器把 `checkPoint` 作为独立字段读取和保存，不根据 Riztime 区间重算。它在游戏运行时或其他上下文中的业务含义仍为 `unknown`，兼容工具必须原样保留，不能删除、归零或替换为 `start`。

## BPM 与 `bpmShifts`

`bpmShifts[]` 的结构与通用关键点相同：

`M1` 确认程序以同一个 `KeyPoint` 类型表示这类对象，其字段正是 `time`、`value`、`easeType`、`floorPosition`；`CanvasMove` 和 `CameraMove` 的关键点列表也引用该类型。

| 字段 | 类型 | `S18` 观察 | 用户人工观察 | 状态 |
|---|---|---|---|---|
| `time` | number | 全部为 `0.0` | BPM 变化开始拍数 | `confirmed`；精确量化规则 `unknown` |
| `value` | number | 全部为 `1.0` | 相对基础 `bPM` 的倍数；界面显示 `bPM × value` | `confirmed` |
| `easeType` | number，枚举 | 全部为 `0` | UI 新增项也保存为 0；用户观察行为是瞬间变化 | 保存值 `confirmed`；行为 `likely` |
| `floorPosition` | number | 全部为 `0.0` | 变化点对应的累计位置 | `confirmed` |

`P1` 保持基础 `bPM=105`，在界面第 8 拍新增 210 BPM。JSON 新增项保存为 `time=8.000001`、`value=2.0`、`easeType=0`、`floorPosition=4.57142925`。因此：

```text
effectiveBPM = bPM × bpmShift.value
```

界面输入第 8 拍而实际保存为 `8.000001`，很可能是浮点或界面定位精度，不应把该差异解释为业务偏移。多次变化、同刻冲突和精确边界归属尚未验证。

## `lines`

每个 `lines[]` 对象在 `S18` 中都固定包含以下 4 个数组：

- `linePoints`
- `notes`
- `judgeRingColor`
- `lineColor`

`M1` 进一步确认程序中的 `Line` 类型恰好声明 `linePoints`、`notes`、`judgeRingColor`、`lineColor` 四个数据字段。它与用户人工确认及全部样例结构一致；这里的“四个字段”现为 `confirmed`。

### `linePoints`

每条线在样例中包含 2–214 个节点。

| 字段 | 类型 | `S18` 观察 | 作用 | 状态 |
|---|---|---|---|---|
| `time` | number | 0–399 | 节点所在拍数 | `likely` |
| `xPosition` | number | -1.85–1.95 | 节点在所属画布坐标系中的横坐标 | `likely` |
| `color` | `Color` | 通道观察范围 0–255 | 线节点的初始/基础颜色；相邻节点颜色不同可形成渐变 | `confirmed` |
| `easeType` | number，整数枚举 | 样例使用 0、1、2、3、4、5、6、7、8、11、12、15 | 当前点到下一点之间的缓动/线形 | `confirmed` |
| `canvasIndex` | number，样例为整数 | 0–35 | 节点引用的画布编号 | `confirmed`（引用关系）；具体坐标行为 `likely` |
| `floorPosition` | number | -195.625–106893 | 未知，疑似派生/缓存位置 | `unknown` |

`U6` 观察到线、判定环、音符等对象接近画布可视范围左右边缘时会逐渐变透明，属于渲染淡出而不是对象自身颜色 Alpha 的变化。淡出大约从 `abs(xPosition)=0.4` 开始，并在中心越过约 `0.5` 后完全不可见；这两个精确阈值暂为 `likely`。有宽度的对象即使中心位于 0.5，仍可能因内侧部分尚在可视范围而看到约一半形状。用户在 `A2` 中还观察到渲染到判定线以下、编辑器渲染坐标 `y<0` 的少量像素会透明化。淡出行为本身为 `confirmed`；精确函数、纵向阈值、对象间差异以及画布移动后的坐标关系仍为 `unknown`。

引用关系：18 个文件中的所有 `canvasIndex` 都能在同文件 `canvasMoves[].index` 中找到；每个文件的画布编号均从 0 连续排列。但这只是现有样例事实，尚未验证格式是否强制连续编号。

数组顺序不能擅自改变。`Pastel Lines/Chart_IN.json` 的一条线中存在 `time: 116.0` 后接 `time: 115.0` 的节点，说明至少现有文件不保证 `linePoints` 按时间递增。结合 `U1` 所述“按数组顺序连接节点”，以后兼容工具不得自动按 `time` 排序。

### `notes`

| 字段 | 类型 | `S18` 观察 | 作用 | 状态 |
|---|---|---|---|---|
| `type` | number，整数枚举 | 只出现 0、1、2 | 音符类型 | 映射 `confirmed` |
| `time` | number | 4–392 | 音符时刻，可能使用拍数 | `likely` |
| `floorPosition` | number | 1.23076928–10285.3848 | 未知，疑似派生/缓存位置 | `unknown` |
| `otherInformations` | array<number> | 长度只出现 0 或 3 | 类型相关附加参数 | 结构 `confirmed`；语义 `likely` |

音符类型分布：

| `type` | 名称 | 数量 | `otherInformations` |
|---:|---|---:|---|
| 0 | Tap / 单点 | 2862 | 2862/2862 均为空数组 |
| 1 | Drag / 拖动 | 687 | 687/687 均为空数组 |
| 2 | Hold / 长条 | 512 | 512/512 均为 3 项 |

`R1` 中可以找到 `Tap`、`Drag` 和 `Hold` 字符串，`U2` 明确确认了上述数值映射，因此音符类型编号当前标记为 `confirmed`。

`M1` 还确认程序数据模型中的 `Note` 类型恰好声明 `type`、`time`、`floorPosition`、`otherInformations` 四个字段，与样例结构逐项对应。

type 2 的 `otherInformations` 当前结论：

- `[0]` 是结束拍。`H1` 中只把结束拍从 28 改为 26，该值同步从 28.0 变为 26.0：`confirmed`。
- `[1]` 是用于结束位置计算的 `canvasIndex`。使用该画布的速度数据可以在 0.01 容差内解释 512/512 个 Hold 的 `[2]`：`confirmed`。
- `[2]` 是结束点的 `floorPosition`。`H1` 中随结束拍自动从 16.0 重算为 14.8571424：`confirmed`。

当前候选结构为：

```text
otherInformations = [endTime, canvasIndex, endFloorPosition]
```

`U5` 确认编辑器没有 Hold 缓动选项，Hold 的形状始终垂直于直线 `y=0`。当 Hold 跨过所属线的多个节点时，其显示高度会因节点所用速度不同而改变。因此此前“`[1]` 是 Hold 缓动”的假设已撤销。

`C1` 确认跨画布选择规则：

```text
otherInformations[1]
  = canvasIndex of the last linePoint whose time < endTime
```

全语料 512/512 匹配。147 个 Hold 恰好结束在线节点时，147/147 都使用严格位于结束拍之前的最后一个节点，而不是结束拍处的节点。隔离实验将结束拍从 103.5 移到 104.5 后，`canvasIndex` 自动从 1 更新为 0，结束 `floorPosition` 也按画布 0 自动重算。

同刻多个线节点、时间倒序节点或 Hold 结束早于首节点时的行为仍未知。

### `judgeRingColor` 与 `lineColor`

两个数组的元素结构完全相同：

```json
{
  "startColor": {"r": 0, "g": 0, "b": 0, "a": 255},
  "endColor": {"r": 0, "g": 0, "b": 0, "a": 255},
  "time": 0.0
}
```

`M1` 确认这两个数组的元素对应程序中的 `ColorKeyPoint`，其数据字段正是 `startColor`、`endColor`、`time`；该类型还声明 `CanMerge`、`ColorsAreEqual` 和 `CompareTo` 方法。方法名可以证明程序存在合并、颜色相等和排序比较能力，但不能据此断言保存或渲染前一定会调用它们；运行实验已明确表明这两个数组不会自动按 `time` 排序。

| 数组 | 每条线长度 | `time` 观察范围 | 业务含义 |
|---|---:|---:|---|
| `judgeRingColor` | 0–67 | 0–396 | 直接设置判定环 RGBA 的颜色事件；`confirmed` |
| `lineColor` | 0–69 | 0–394 | 覆盖整条线基础颜色的颜色事件；`confirmed` |

`judgeRingColor` 和 `lineColor` 的名称分别直接指向“判定环颜色”和“线颜色”；`U4`、`U7`、`U8` 进一步提供了实际渲染行为，因此基本角色升级为 `confirmed`。

两者虽然具有相同的 JSON 元素结构，但合成语义不同：

```text
judgeRingColor：直接改变判定环的颜色和透明度
lineColor：使用自身 Alpha 作为权重，混合并覆盖线的基础 RGB
```

因此不能把已经确认的 `lineColor` Alpha 覆盖公式直接套用到 `judgeRingColor`。`judgeRingColor.a` 表示判定环被直接设置到的透明度，而不是覆盖强度：`confirmed`（`U7`）。

用户进一步确认 `judgeRingColor` 的 RGBA 四通道均使用线性插值。以独立的插值进度 `u` 表示：

```text
C(u) = startColor.C + u * (endColor.C - startColor.C)
C ∈ {r, g, b, a}
```

逐通道线性公式为 `confirmed`（`U7`）。`U8` 进一步确认其时间边界规则；这里的“颜色”均包含 Alpha：

1. 从第 0 拍到第一个颜色事件之前，保持第一个事件在该边界指定的 RGBA。
2. 在两个不同时间的颜色事件之间，RGBA 四通道随时间线性变化。
3. 从最后一个颜色事件到谱面结束，保持最后一个事件在该边界指定的 RGBA。
4. 同一时刻可以放置两个事件来表达瞬间突变。

以上首尾保持、事件间线性变化和同刻突变能力由 `U8` 确认。`U8` 当时将精确边界描述为“绝大部分左闭右开”，但 `J1` 使用三处不连续边界进行隔离后，24.0、26.0、28.0 拍均显示旧颜色，越过边界后才显示新颜色。因此精确可见边界修正为旧段右闭、新段左开：

```text
在 t = event[i].time：仍为前一事件的 endColor
在 t > event[i].time：采用本事件的 startColor
```

`J1` 的 24 拍同刻事件按蓝、绿排列：24.0 拍仍为前一红色，24.1 拍为同刻最后一项绿色，蓝色没有出现。对两个同刻 `judgeRingColor` 事件，边界瞬间属于前一不同时间事件、越过边界后最后一项生效、第一项没有可观察正长度区间，均为 `confirmed`。内部代码比较符仍未定位。

`J2` 将恒定颜色事件按 `20 红、26 绿、24 蓝、27 青` 的数组顺序保存。实际判定环颜色为：22、24.1、25.9、26.0 拍红，26.1、26.9、27.0 拍蓝，27.1、27.9 拍青；绿色没有出现。这确认 `judgeRingColor` 同样不会先按 `time` 排序，原数组顺序参与语义。可见行为支持 `26→24` 使绿色项没有正长度区间、24 拍蓝色项被延迟到越过 26 拍后显示；具体内部扫描算法仍为 `unknown`。

主保存和两个自动保存均保留 `20、26、24、27` 及全部 RGBA，确认编辑器不会在保存时自动排序 `judgeRingColor`。

### 判定环出现条件

结合 `U4` 与 `U8`，判定环的几何出现需要同时满足：

```text
judgeRingColor 非空
且
当前线与 y=0 相交
```

在上述条件下，每个交点都可产生判定环；一条线不应被假定至多只有一个判定环。通过让画布流速依次取正、负、正等不同符号，同一条线可以多次经过 `y=0`，从而形成多个交点：`confirmed`（`U8`）。最终像素是否可见还会受到 `judgeRingColor.a` 和视口边缘淡出的影响，这与判定环的几何出现条件是不同层次的问题。

### 颜色来源、优先级与 Alpha

用户观察到的渲染关系：

1. `linePoints[].color` 提供线节点的初始/基础颜色。
2. 相邻节点颜色不同时，未被覆盖的线可以显示节点之间的颜色渐变。
3. 判定环的初始颜色与线的初始颜色相同。
4. 当 `judgeRingColor` 数组为空时，该线没有判定环；数组非空时，判定环只在线与 `y=0` 的交点处出现，一条线可以产生多个交点。
5. `judgeRingColor` 事件直接改变判定环 RGBA，不与初始颜色执行 `lineColor` 式覆盖混合。
6. `lineColor` 的优先级高于 `linePoints[].color`：生效时会覆盖整条线的基础颜色，即使原线是渐变，也会显示为单一覆盖色。
7. `lineColor` 的 Alpha 会影响覆盖强度：Alpha 越低，影响越弱；Alpha 越高，影响越强。
8. 线的初始 Alpha 会限制最终覆盖效果。特别地，当初始 Alpha 为 0 时，修改 `lineColor` 不会产生可见效果。

可以将当前已确认的概念关系表示为：

```text
linePoints[].color（基础色与基础 Alpha）
              ↓
      相邻节点之间的基础渐变
              ↓
lineColor（更高优先级的整线颜色覆盖，受双方 Alpha 约束）
```

上述顺序和特殊情况标记为 `confirmed`（`U4`）。

### `lineColor` Alpha 混色

`A1` 将基础线固定为不透明绿色 `(0,255,0,255)`，将覆盖 RGB 固定为红色 `(255,0,0)`，只让 `lineColor.a` 从 0 变化到 128、再到 255。用户观察及第 20、24、28 拍截图分别显示绿色、暗黄/橄榄色和红色；所有时刻最终线 Alpha 都保持 255。

因此在基础 Alpha=255 的测试域内：

```text
w = lineColor.a / 255
finalRGB = baseRGB + w * (lineColorRGB - baseRGB)
finalA = 255
```

`lineColor.a` 是覆盖 RGB 的线性混合权重，而不是最终线透明度。Alpha 0 完全保留基础 RGB，Alpha 255 完全使用覆盖 RGB，Alpha 128 约为等权混合，均为 `confirmed`（`A1`）。准确整数舍入仍为 `unknown`。

Alpha 通道本身也在本项 `time` 到下一项 `time` 之间从 `startColor.a` 线性变化到 `endColor.a`：`confirmed`（`A1`）。普通保存和 3 个自动保存均逐字段保留了 Alpha 事件。

`A2` 使用三条同拍、同几何、同颜色事件的并排线，只把基础 Alpha 设为 0、128、255。左线始终不可见，中线始终保持约半透明，右线始终不透明；中、右两条线的 RGB 同步由绿变红。因此联合公式扩展为：

```text
w = lineColor.a / 255
finalRGB = baseRGB + w * (lineColorRGB - baseRGB)
finalA = baseAlpha
```

`linePoints[].color.a` 决定最终线 Alpha，`lineColor.a` 只决定 RGB 覆盖权重，均为 `confirmed`（`A2`）。基础 Alpha 0 无法被不透明 `lineColor` 恢复；基础 Alpha 128/255 时，RGB 变化相同而最终透明度分别保持约 128/255。整数舍入和视口最终合成的精确舍入仍为 `unknown`。

### `lineColor` RGB 时间模型

`G1` 给第 4 条长线添加 3 个不透明 `lineColor` 事件：20 拍红→红、24 拍红→蓝、28 拍蓝→蓝。用户观察到 20 和 22 拍为红色，24 拍从红色开始连续变化，26 拍为紫色，28 拍到达蓝色。

因此在事件时间递增且相邻颜色连续的本次模型中：

```text
在 event[i].time 附近：颜色从 event[i].startColor 开始
在 event[i + 1].time：颜色到达 event[i].endColor
两者之间：整条线的单一覆盖色连续插值
```

`lineColor[].time` 标记插值段开始边界、事件持续到下一项的 `time`、以及整线在同一时刻使用单一覆盖色，均为 `confirmed`（`G1`）。由于 `G1` 的相邻端点颜色连续，它本身无法区分边界属于旧段还是新段。用户根据连续变化和中点颜色进一步确认，RGB 三个通道使用线性插值。`D1` 补充边界归属后，对普通递增事件的可见模型写为 `t_i < t <= t_(i+1)`：

```text
u = (t - t_i) / (t_(i+1) - t_i)
C(t) = startColor.C + u * (endColor.C - startColor.C)
C ∈ {r, g, b}
```

核心插值公式为 `confirmed`（`G1`）。在 `t=t_i` 且相邻颜色不连续时，仍显示前一事件的 `endColor`；越过 `t_i` 后才瞬间采用本事件的 `startColor`：编辑器可见行为 `confirmed`（`D1`）。这对应旧段右闭、新段左开的边界表现，而不是左闭右开。内部实现可能使用严格大于比较，但界面显示精度、帧采样和浮点误差未被隔离，因此代码级比较符仅为 `likely`。

`D1` 的同刻 26 拍事件按蓝 A、绿 B 排列：26.0 拍显示蓝色，26.1 拍显示绿色，且无渐变。`T1` 进一步用与前一颜色不同的同刻第一项和最后一项，在 24、26 拍重复测试：边界瞬间分别保持前一红色、绿色；越过边界后分别直接采用同刻最后一项绿色、紫红色；同刻第一项蓝色、黄色均未出现。

因此对两个相邻同刻 `lineColor` 事件：边界瞬间仍属于前一不同时间事件，越过边界后同刻最后一项生效，同刻第一项没有可观察正长度区间，均为 `confirmed`（`T1`）。三个或更多同刻事件时“仅最后一项获得正长度区间”是符合当前数组区间模型的推广，但尚未直接测试，状态为 `likely`。

第 19 拍显示首事件的红色，支持首事件前使用其 `startColor`，暂记为 `likely`。判定环在实验中保持黑色，确认本次 `lineColor` 不会同时覆盖 `judgeRingColor`。普通保存和 3 个自动保存文件均逐字段保留了颜色数组。

`D1` 还确认末事件之后保持末颜色。`lineColor` 的不连续边界和双事件同刻优先级已确认。`judgeRingColor` 的首尾保持来自 `U8`，精确边界和双事件同刻优先级已由 `J1` 隔离确认，倒序规则由 `J2` 确认。

`O1` 将四个恒定颜色事件按 `20 红、26 绿、24 蓝、28 黄` 的数组顺序保存。实际渲染为：22、24.1、25.9 拍红，26.1、27 拍蓝，28.1 拍黄；没有观察到绿色、渐变、闪烁或报错。这与“先按 `time` 排序”明显不符，确认原数组顺序参与渲染语义。可见行为支持以下解释：`26→24` 倒序使绿色项没有取得正长度显示区间，后面的 24 拍蓝色项则被前一数组边界延迟到越过 26 拍后显示。具体内部扫描算法仍为 `unknown`。

主保存与 `O1` 输入字节一致，两个自动保存也保留 `20、26、24、28` 及全部颜色，确认编辑器不会在保存时自动排序 `lineColor`。

这两个数组同样不能自动排序。现有样例分别有 2 个 `judgeRingColor` 数组和 3 个 `lineColor` 数组出现时间倒序。

## `canvasMoves`

| 字段 | 类型 | `S18` 观察 | 作用 | 状态 |
|---|---|---|---|---|
| `index` | number，样例为整数 | 0–35 | 画布编号 | `confirmed`（引用关系） |
| `xPositionKeyPoints` | array<KeyPoint> | 每画布 1–141 项 | 画布横向位置关键点 | `likely` |
| `speedKeyPoints` | array<KeyPoint> | 每画布 1–46 项 | 画布流速关键点 | `likely` |

### `xPositionKeyPoints`

| 字段 | `S18` 观察 | 含义与状态 |
|---|---|---|
| `time` | 0–396 | 节点拍数；`likely` |
| `value` | -1.5–2.3 | 画布在固定参考系中的横坐标；`likely` |
| `easeType` | 使用 0–8、10–14、17、18 | 到下一节点的横向运动缓动；`confirmed` |
| `floorPosition` | 4114/4114 均为 0 | 业务含义 `unknown` |

### `speedKeyPoints`

| 字段 | `S18` 观察 | 含义与状态 |
|---|---|---|
| `time` | 0–390 | 流速改变拍数；`likely` |
| `value` | -4–999 | 画布流速；允许负值的行为来自 `U1`，状态 `likely` |
| `easeType` | 334/334 均为 0 | 用户观察为瞬间改变而非线性插值；行为 `likely` |
| `floorPosition` | -195–78076 | 疑似速度积分结果，当前 `unknown` |

`U1` 描述：画布移动会带动其坐标系和节点；节点的纵向位置由速度随时间的积分决定。该数学模型与样例中的 `floorPosition` 分布相符，但尚无代码或可控实验，因此整体状态为 `likely`。

`value=999` 是现有样例中的真实极值，不应据此推断编辑器合法上限为 999。

`E1` 在单画布、恒定 BPM 谱面中，把 8–28 拍的目标缓动展开为每 0.25 拍一次、共 80 段的普通速度阶跃。编辑器能够正常打开和播放，使用者观察到 0.5→2.0 的逐渐加速且阶梯感不明显；线、音符和 Hold 均正常，没有卡顿或报错。主文件和自动保存都保留全部 82 个流速节点。因此“多个小间隔突变节点可以近似流速缓动”在该测试条件下为 `confirmed`；它不能直接推广到多画布、多 BPM 或任意采样密度。

## `cameraMove`

每个文件固定包含一个 `cameraMove` 对象：

### `scaleKeyPoints`

| 字段 | `S18` 观察 | 含义与状态 |
|---|---|---|
| `time` | 0–263 | 缩放节点拍数；`likely` |
| `value` | 1–3 | 缩放倍数；`likely` |
| `easeType` | 使用 0、1、2、3、5、7、8、13 | 到下一节点的缩放缓动；`confirmed` |
| `floorPosition` | 39/39 均为 0 | 业务含义 `unknown` |

`U1` 观察编辑器可输入的缩放范围为 0.1–10，且缩放会改变可见坐标范围、判定圈和音符大小，但不会改变线宽。因为 `S18` 只覆盖 1–3，合法范围和视觉行为暂标记为 `likely`。

### `xPositionKeyPoints`

| 字段 | `S18` 观察 | 含义与状态 |
|---|---|---|
| `time` | 0–262.001 | 横向移动节点拍数；`likely` |
| `value` | -0.25–0.25 | 摄像机横坐标；`likely` |
| `easeType` | 使用 0、5、13 | 到下一节点的横向运动缓动；`confirmed` |
| `floorPosition` | 24/24 均为 0 | 业务含义 `unknown` |

`U1` 提醒：摄像机向右移动时，画面内容相对摄像机会向左移动。这是正常的相机参考系效果，不应直接把屏幕内容方向当作 JSON 坐标正方向。

## `easeType` 映射

`R1` 中在多个位置出现以下完整且相同顺序的字符串表，`U1` 给出了相同的编号映射，因此当前映射标记为 `confirmed`：

| 值 | 名称 | 值 | 名称 |
|---:|---|---:|---|
| 0 | Linear | 10 | In Quint |
| 1 | In Quad | 11 | Out Quint |
| 2 | Out Quad | 12 | InOut Quint |
| 3 | InOut Quad | 13 | Zero |
| 4 | In Cubic | 14 | One |
| 5 | Out Cubic | 15 | In Circle |
| 6 | InOut Cubic | 16 | Out Circle |
| 7 | In Quart | 17 | Sin |
| 8 | Out Quart | 18 | Cos |
| 9 | InOut Quart |  |  |

注意：虽然 BPM 和画布速度关键点的 `easeType` 在样例中都是 0，但 `U1` 观察它们的行为是瞬间跳变。可能原因包括这些上下文忽略缓动字段，或使用独立的阶跃逻辑。不能把“字段值 0”直接解释为这些上下文一定执行 Linear 插值。

### 缓动求值表（2026-09-23 运行实测）

`GetEase` 把 19 条曲线预计算成一张静态表 `float[] EaseInfors`：

| 项 | 实测值 |
| --- | --- |
| 表长度 | `1919 = 19 × 101` —— 恰好 19 条曲线，**没有未公开的第 20 种及以后** |
| 行布局 | 第 `type` 行是 `EaseInfors[type * 101 … type * 101 + 100]`，对应 `progress = 0.00 … 1.00`（步长 `0.01`） |
| `GetEaseWithIndex(index, type)` | `index` 是 0–100 的采样下标；`index ≥ 101` 被钳到 1，不抛错 |
| `GetEaseWithProgress(progress, type)` | 越界（`type ≥ 19`）**抛 `Il2CppException`** |

数值校验（`t=0.5`）：`type 0 → 0.5`（Linear）、`1 → 0.25`（In Quad）、`2 → 0.75`（Out Quad）、`5 → 0.875`（Out Cubic）、`13 → 0`（Zero）、`14 → 1`（One）、`15 → 0.134`（In Circle）、`17 → 0.7071`（Sin）、`18 → 0.2929`（Cos），与上表名称逐一对上，因此名称映射为 `confirmed`（数值证据）。

同一批实测确认：19 个名称只存在于场景 `Rizline Editor_Data\level0` 的 Dropdown 序列化选项中，`GameAssembly.dll` 与重建的 `global-metadata.dat` 里都没有这些字符串；`CanvasEditor.easeType`、`LineEditor.easeType`、`CameraEditor.easeType` 各有 19 项且顺序一致。详见 `docs/features/EXTRA_EASING.md`。

## `floorPosition`

`U1` 和 `F1` 共同支持：`floorPosition` 是从拍数映射得到的累计时间/滚动位置，并受所属画布速度影响。

在 BPM 和速度保持不变的区间：

```text
ΔfloorPosition = Δbeat × 60 / BPM × speed
```

当 `speed=1` 时，它等于拍数换算出的秒数；速度不为 1 时，它是速度加权的等效秒数/滚动坐标，而不再等于真实播放秒数。

`P1` 进一步确认单次 BPM 变化采用分段积分。有效 BPM 为：

```text
effectiveBPM = bPM × currentBpmShift.value
```

静态观察如下：

| 路径 | `S18` 观察 |
|---|---|
| `bpmShifts[].floorPosition` | 全部为 0 |
| `lines[].linePoints[].floorPosition` | -195.625–106893 |
| `lines[].notes[].floorPosition` | 1.23076928–10285.3848；`H1` 中移动 Hold 结束点时该值不变，可能对应音符开始位置；Hold 结束位置确认另存于 `otherInformations[2]` |
| `canvasMoves[].xPositionKeyPoints[].floorPosition` | 全部为 0 |
| `canvasMoves[].speedKeyPoints[].floorPosition` | -195–78076 |
| `cameraMove.*KeyPoints[].floorPosition` | 全部为 0 |

`F1` 使用画布速度积分检查 Hold 结束位置，在 0.01 容差内达到 512/512 匹配；完全忽略速度、只做拍数换秒则只有 381/512 匹配。因此速度加权关系标记为 `confirmed`。

`P1` 在速度为 1 时新增一次 105→210 BPM 变化。编辑器自动重算变化点之后的 30 个位置字段，30/30 都在 `10^-6` 容差内符合按有效 BPM 分段积分，最大绝对误差约 `8.93 × 10^-7`。

少量速度关键点和线节点仍存在锚点或重置方面的例外；多次 BPM 变化、BPM 与速度同刻变化和边界归属也尚未验证。除非后续掌握这些规则，否则任何转换工具仍应原样保留此字段，不能批量删除、归零或自行重算。

## 数组顺序与兼容性

静态检查结果：

- 所有 `notes`、画布关键点和摄像机关键点数组在现有样例中按 `time` 非递减。
- 1 个 `linePoints` 数组存在时间倒序。
- 2 个 `judgeRingColor` 数组存在时间倒序。
- 3 个 `lineColor` 数组存在时间倒序。

`O1` 与 `J2` 已分别通过运行观察确认 `lineColor`、`judgeRingColor` 的数组顺序参与渲染语义，且编辑器保存时不会自动排序。`linePoints` 的倒序运行规则尚未验证。未来脚本和保存逻辑不得为了“整洁”而自动排序任何这些数组。

## 运行保存实验

### 未编辑另存

状态：`confirmed`，但只覆盖未编辑样例。

2026-08-28 的运行基线中，通过 `Ctrl+Shift+S` 将隔离的 `Baseline Sample.json` 另存为 `Baseline Saved.json`：

- 两个文件长度均为 9947 字节。
- 两个文件 SHA-256 均为 `1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`。
- 两个文件字节完全一致。

这证明未编辑谱面在本次另存操作中没有被重新格式化或改写。它不能证明实际编辑后仍会保留字段顺序和数值精度。

### 实际编辑后的未知顶层字段

`X1` 在 `Sample.json` 顶层只增加字符串字段：

```json
"codexCompatibilityProbe": "preserve-this-exact-value-2026-08-28"
```

编辑器能够打开该谱面。用户随后执行已经独立验证过的 Hold 结束拍修改并按 `Ctrl+S` 保存。递归比较只发现预期的两个 Hold 数值变化和探针字段消失；保存文件还与不含探针、执行相同编辑的控制文件字节完全一致。两个自动保存 JSON 也都不含探针字段。

结论：编辑器接受额外未知顶层字符串字段，但在实际编辑后的普通保存和本次自动保存中会删除它，状态为 `confirmed`。这项结果只覆盖顶层字符串字段，不能直接推广到未知嵌套字段、对象或数组。

兼容性要求：不能依赖原编辑器保存自定义未知字段。未来扩展元数据应优先使用 sidecar 文件，或使用经过验证的外部保存合并机制。

## 保留的格式问题

以下问题不阻塞进入阶段 5，按实际功能需求再逐项调查：

1. 缺失字段、空数组、未知嵌套字段、越界枚举和异常数值的读取与保存行为。
2. `floorPosition` 的锚点/重置例外、多次 BPM/速度组合规则以及何时重算。
3. BPM 变化点的精确量化、同刻冲突和边界归属。
4. `checkPoint` 在游戏运行时或其他版本中的真实用途。
5. 时间字段是否全部使用拍数，以及 `transTime` 是否是唯一使用秒的时间字段。
6. 除已验证颜色数组外，编辑后保存对一般字段顺序、数组顺序和浮点精度的处理。
7. 三个或更多同刻颜色事件是否仅最后一项取得正长度区间；当前为 `likely` 推广。
8. `linePoints` 倒序时的运行行为；当前只确认现有语料中存在一处倒序。

## 后续实验原则

- 每次只改变一个字段或一个最小对象。
- 从隔离副本运行，不直接改动唯一样例。
- 实验前后记录 SHA-256 和结构 diff。
- 分开记录“编辑器接受文件”“界面显示正确”“播放行为正确”“保存后仍兼容”。
- 只有获得可重复结果后才把 `likely` 或 `uncertain` 升级为 `confirmed`。
