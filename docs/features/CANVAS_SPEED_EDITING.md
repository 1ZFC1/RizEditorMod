# 画布流速编辑增强

状态：已通过 BepInEx 运行时插件接入；缓动端点、框选复制/剪切和两种粘贴已完成运行验证。缓动类型 UI 已改为与原版一致的英文名下拉框。

本功能只处理当前聚焦画布的 `speedKeyPoints`。BPM 变化、画布横向移动、相机事件和其他画布不在本次修改范围内。

## 当前行为

- 画布流速在谱面中保存为普通突变关键点。
- `SpeedEditor` 同时显示 BPM 与画布流速标签。
- 当前删除入口一次只删除一个标签；首节点受到保护。
- 编辑器已有垂直细分，使用者可在 `1/1` 至 `1/64` 之间设置。
- 垂直细分的实际状态由 `ProjectInformation.SnapLinesSplitCount` 提供；预设按钮和 `Shift + 滚轮` 都修改同一个值。

## 功能 A：流速缓动离散采样

### 用户需求

不增加独立的“采样间隔”设置。缓动展开间隔直接采用编辑器当前的垂直细分：

| 垂直细分 | 生成节点间隔 |
| --- | --- |
| `1/1` | 1 拍 |
| `1/4` | 1/4 拍 |
| `1/16` | 1/16 拍 |
| `1/64` | 1/64 拍 |

一般公式：

```text
sampleInterval = 1 / ProjectInformation.SnapLinesSplitCount
```

当垂直吸附关闭时，原 getter 返回 1，因此采样间隔自然退化为 1 拍。

### 已确定行为

- 缓动只是一种编辑操作，最终仍生成原格式的普通 `speedKeyPoints`。
- 生成节点的 `easeType` 仍为 0；不依赖游戏解释新的缓动字段。
- 原区间终点必须保留，并精确采用终点原速度。
- 整次展开作为一个 `EditControl.Stepping` 事务，Undo/Redo 一次撤销或恢复全部生成节点。
- 展开后调用 `EditControl.SetFloorPosition`，再刷新 `SpeedEditor` 标签和预览。
- 不新增第二套采样密度状态，避免垂直细分与缓动采样设置不一致。

### 已确认边界

- 从起点开始按 `start + k / splitCount` 生成中间节点。
- 若终点不落在该序列上，保留精确终点，最后一段允许短于 `1/splitCount`。
- 在修改谱面前计算预计生成数量；超过 4096 个节点时弹出警告，显示预计数量，并允许使用者取消或继续，不设置静默硬上限。
- 插入内部节点前过滤因 float 精度塌缩到起点/终点拍的样本，避免同一拍出现两个速度事件。

## 功能 B：流速剪贴板

### 用户需求和已确认行为

- `Ctrl+C` / `Ctrl+X` 复用 `LineEditor.SelectLines` 的原矩形框选。
- 忽略矩形横向范围，只选择上下端拍数闭区间内、当前聚焦画布的全部流速节点。
- 剪切始终保留画布首个流速节点，但仍把该节点复制到剪贴板。
- `Ctrl+V` 保持节点原拍数；`Ctrl+Shift+V` 把最早节点对齐当前播放头。
- `Ctrl+D` 框选后只删除，不写入软件剪贴板（删除前剪贴板内容保持不变）。
- 目标同拍节点直接由粘贴内容覆盖，不保留重复节点。
- 剪切和每次粘贴分别形成一个 Undo/Redo 步骤，之后重算 `floorPosition` 并刷新标签与预览。
- 每次启动后需要先切换一次“线编辑”标签来初始化原框选组件；未初始化时插件给出明确提示。
- 原定 `Shift + 右键` 区间删除已取消，未保留在插件中。
- 面板上的“导出到剪切板”打开后，成功的复制和剪切会把同一份流速节点写入系统剪切板。“从剪切板导入”只替换内部流速剪贴板，不直接修改谱面。

## 已定位代码

| 类型/方法 | token / RVA | 作用 |
| --- | --- | --- |
| `ProjectInformation.get_SnapLinesSplitCount` | `0x06000144` / `0x2BE890` | 读取垂直细分；垂直吸附关闭时返回 1 |
| `ProjectInformation.set_SnapLinesSplitCount` | `0x06000145` / `0x2BED70` | 修改垂直细分 UI 状态 |
| `SnapControl.ApplyPreset` | `0x06000216` / `0x2C4370` | 预设按钮直接转发到上述 setter |
| `ShortcutKeysControl.KeyWithShift` | `0x060001CB` / `0x2C1B30` | `Shift + 滚轮` 对当前细分值执行 ±1 |
| `ShortcutKeyControl.get_ShiftPressed` | `0x0600018A` | 统一的 Shift 状态入口 |
| `SpeedEditor.DeleteCurrentTag` | `0x06000375` / `0x2E0910` | 调用当前标签绑定的删除操作，并保护首节点 |
| `<>c__DisplayClass24_2.<ReloadTags>b__1` | `0x06000384` / `0x2EB300` | 实际单节点删除事务；调用 `EditControl.Stepping`、销毁标签并刷新位置 |
| `ProjectInformation.nowChartTime` | 字段 token `0x0400014B` | 当前谱面拍位置候选来源 |
| `SpeedEditor.nowChartTime` | 字段 token `0x04000374` | 速度编辑器缓存的当前谱面拍位置 |
| `ProgressControl.GetBeats` | `0x060000F2` / `0x2B9F20` | 当前拍的公共读取入口候选 |

`SpeedEditor` 使用 `LinkedList<NodeInfo>` 维护显示标签顺序；`NodeInfo` 保存 `KeyPoint` 和对应 `ValueTag`。删除动作是在 `ReloadTags` 创建标签时绑定的闭包，不是 `DeleteCurrentTag` 自身直接操作 JSON 列表。

## 实现约束

- 不修改谱面 JSON 结构，不写入自定义字段。
- 不自动排序与当前操作无关的数组。
- 只修改当前画布的 `speedKeyPoints`。
- 所有批量操作必须经过 `EditControl.Stepping`。
- 实现必须可禁用、可回滚，并与当前 `GameAssembly.dll` 哈希绑定。
- 正式实现前需要针对多画布、BPM 变化和大量节点建立测试谱面。

## 待确认

交互边界已确认。进入实现前仍需确定运行时注入方式和新增 UI 的具体布局；它们不得改变上述用户行为。

## 交付文档

功能完成并通过运行测试后，必须新增 `docs/USER_GUIDE.md`，至少说明：

- 如何设置垂直细分以及它与采样间隔的关系；
- 如何选择流速缓动的起止节点和缓动类型；
- 如何预估生成节点数量以及处理超过 4096 个节点的警告；
- 流速框选复制/剪切、两种粘贴及首次初始化限制；
- Undo/Redo、保存和兼容性注意事项；
- 如何禁用或卸载本项目增加的功能。
