# 额外缓动曲线（研究结论与实现方案）

状态：**已实现并验收**（2026-09-23）。当前支持**编辑画布**与**编辑摄像机·横向移动**；摄像机缩放与编辑线经使用者决定取消，见第 1 节的实现范围表。
相关：`CHART_FORMAT.md` 的 `easeType 映射`、`DECISIONS.md` D-023、`CODEMAP.md` 的「相机、速度、颜色与主题」。

## 1. 需求

原编辑器「编辑画布」工具的缓动下拉框只有 19 条曲线，希望增加更多（例如震荡）。

**当前实现范围（2026-09-23 定稿，使用者逐项验收）**

| 工具 | 状态 | 说明 |
| --- | --- | --- |
| 编辑画布 | ✅ 支持 | 首选范围，落点是当前画布的 `xPositionKeyPoints`；拦截 `CanvasEditor.OnEaseTypeChange` |
| 编辑摄像机·横向移动 | ✅ 支持 | 与画布同为 `KeyPoint`；因摄像机接线与画布不同，改在 `Dropdown.set_value` 处拦截 |
| 编辑摄像机·缩放 | ❌ 取消 | 使用者选择后出现过 `IndexOutOfRangeException`，按要求移除；缩放完全保持原版行为，不做任何写入 |
| 编辑线 | ❌ 取消 | 曾实现（写入选中线节点的 `linePoints`），但线工具靠**拖动创建线、Ctrl+拖动改节点属性**，下拉框驱动的拟合与操作习惯割裂，使用者决定改回原版 |

非法 `easeType`（≥ 19）的安全网见 `src/RhythmEditorMod/EaseTypeGuard.cs`：即使某处仍写入了越界缓动类型，`GetEase` 也会按 Linear 求值并告警，不再抛 `IndexOutOfRangeException`。

## 2. 已确认的机制（实测，2026-09-23）

### 2.1 识别链路

```text
场景 Dropdown 选项名 ──选中──> OnEaseTypeChange() ──写入──> KeyPoint.easeType / LinePoint.easeType（JSON 整数）
                                                                    │
                                              渲染/预览时 ──> GetEase.GetEaseWithProgress(t, easeType)
```

| 环节 | 载体 | 证据 |
| --- | --- | --- |
| 19 个名字 | **只有场景里有**：`Rizline Editor_Data\level0` 中每个名字出现 4 次（三个编辑器的 Dropdown 序列化选项 + 1 份副本） | `GameAssembly.dll` 里一个缓动名都没有；重建的 `global-metadata.dat` 字符串堆里没有 `InOut Quart`、`In Circle` |
| 下拉框 | `CanvasEditor.easeType` / `LineEditor.easeType` / `CameraEditor.easeType`，各 19 项、顺序完全一致；`CanvasEditor.easeTypeEditor` 指向场景对象 `Canvas Ease Editor`（未聚焦关键点时 `active=False`） | 运行探针转储 |
| 选项切换 | 三个编辑器各有 `LoopEase(int step)`（私有）、`CanvasEditor`/`CameraEditor` 另有 `OnEaseTypeChange()`（公开） | 类型转储 |
| 求值 | `GetEase`：静态 `float[] EaseInfors` + `GetEaseWithIndex(int index, int type)` + `GetEaseWithProgress(float progress, int type)` + `Instantiation()` | 类型转储 |
| 预览 | `CanvasEditor.SetCanvasKeyPoint()`（RVA `0x2E1B70`）调用 `GetEase.GetEaseWithIndex`；`SpeedEditor.easeMarkContainer` 指向 `EaseMarks` | CODEMAP 既有记录 + 探针 |

### 2.2 缓动表容量：**恰好 19 条，没有隐藏类型**

- `EaseInfors.Length = 1919 = 19 × 101`：每条曲线 101 个采样（progress `0.00`–`1.00`，步长 `0.01`）。
- 行布局：值等于 1 的下标为 `100, 201, 302, …`，即第 `type` 行从 `type * 101` 开始、到 `type * 101 + 100`。
- `GetEaseWithIndex(index, type)`：`index` 是 0–100 的采样下标，`index ≥ 101` 被钳到 1（不抛错）。
- `GetEaseWithProgress(t, type)`：`type ≥ 19` **抛 `Il2CppException`**（数组越界）。t=0.25/0.5/0.75 的实测值与公式逐一对上：

| type | 0 | 1 | 2 | 5 | 13 | 14 | 15 | 17 | 18 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 名称 | Linear | In Quad | Out Quad | Out Cubic | Zero | One | In Circle | Sin | Cos |
| t=0.5 | 0.5 | 0.25 | 0.75 | 0.875 | 0 | 1 | 0.134 | 0.7071 | 0.2929 |

因此 `CHART_FORMAT.md` 的 19 项映射由「字符串证据」升级为**数值实测 `confirmed`**。

### 2.3 硬约束

谱面里存的是 `easeType` 整数，**插值由游戏自己执行**。使用者已确认新缓动只能编辑器内自用，所以：

- 不能简单地把 `easeType` 写成 19+：编辑器自身会立刻抛异常，游戏侧更是未知行为。
- 新增曲线必须以**已有缓动（建议 Linear，`easeType = 0`）拟合**成一段密集节点写入谱面，游戏照旧只看到它认识的 19 种。

这与流速缓动的既有做法一致（D-008 / D-009：曲线展开为普通节点），区别是这里要在编辑器的画布/线/相机数据上直接做。

## 3. 方案：把额外缓动「烘焙」成 Linear 密集节点

### 3.1 数据模型

对一段「起点节点 → 下一个节点」的区间：

```text
t0, v0 = 起点.time / 起点.value        （value 在画布工具是画布 x，在线工具是线 x）
t1, v1 = 下一个节点.time / 下一个节点.value
u(t)   = (t - t0) / (t1 - t0)
目标   value(u) = v0 + (v1 - v0) · f(u)   ← f 是缓动函数，f(0)=0、f(1)=1
```

在 `t0` 与 `t1` 之间插入若干个新节点（位置由自适应细分决定），每个节点的 `easeType = 0`（Linear），并把**起点自己的 `easeType` 也改成 0**，使整段成为折线逼近目标曲线。终点节点不动（它的 `value`/`easeType` 属于下一段）。

- 端点精确：`f(0)=0`、`f(1)=1` 保证 `t0`/`v0`/`t1`/`v1` 不变，相邻区间不受影响。
- 拟合误差：折线与目标曲线的偏差由细分容差控制（见 4.2）；Linear 节点的直线段在视觉上等价于稠密折线（流速缓动已验证 80 段即可「阶梯感不明显」，见 `CHART_FORMAT.md` `E1`）。

### 3.2 落点与刷新（复用既有管线）

| 工具 | 列表 | 备注 |
| --- | --- | --- |
| 画布 | `CanvasMove.xPositionKeyPoints`（`KeyPoint`） | `EditorOperations.RequireCanvas(project, canvasIndex)` 已有 |
| 相机 | `CameraMove.xPositionKeyPoints`（`KeyPoint`） | `EditorOperations.RefreshCameraMove` 已有；**只用横向移动列表**，缩放列表按使用者要求不碰（判定复用 `EditorOperations.IdentifyCameraList`） |
| 线 | `LinePoint`（含 `xPosition`/`color`/`canvasIndex`/`floorPosition`） | **已实现后取消**：线工具靠拖动创建线、Ctrl+拖动改属性，下拉框驱动与操作习惯割裂（2026-09-23 使用者决定） |

写入流程（沿用 `EditorOperations` 现有模式）：

1. `project.editorControl.Stepping((Il2CppSystem.Action)mutation)` —— 整段拟合只占**一个 Undo 步骤**。
2. mutation 内：删除区间内旧的内部节点（若已拟合过）→ 按时间顺序插入新节点 → `project.editorControl.SetFloorPosition()` 重算累计位置。
3. `Refresh(editor, project)`：画布走 `canvasControl.GetInformations()/SetPosition()` + `editor.SetCanvasKeyPoint()`；相机走 `cameraControl.FleshInfo()/SetXPosition()` + `editor.SetCameraKeyPoint()`。
4. 不新增任何 JSON 字段，`easeType` 仍是 0–18。

### 3.3 UI 接入（已选方向 A；本小节保留当时的两个候选）

**方向 A：扩展原下拉框**（贴合「原编辑器里有更多缓动」的直觉）—— **已采用**

- 启动后在 `CanvasEditor.easeType` 追加三个弹性选项（`In` / `Out` / `InOut Elastic`，命名沿用原编辑器风格，位置在原生 19 项之后）。
- Harmony Prefix 拦截 `CanvasEditor.OnEaseTypeChange`：标签匹配**且**下标 ≥ 原生项数时执行烘焙、`return false` 跳过原逻辑；否则完全放行。
- 已验证：运行时 `options.Add` + `RefreshShownValue()` 生效（19 → 22，幂等）；`LoopEase` 按 `options.Count` 取模（无需 patch）；原下拉本身按当前拍取段，不依赖任何「聚焦关键点」。
- 副作用：烘焙后起点节点的真实 `easeType` 是 0（Linear），运动形状由新增节点体现；`Canvas Ease Editor` 里的曲线预览仍只会画出该节点自身的缓动（直线）。

**方向 B：放进插件自己的面板**（「插件」工具栏入口，D-022）—— 未采用，保留为备选

- 面板里列出可用曲线与强度，作用于「当前聚焦的关键点 → 下一个节点」。
- 完全不碰原下拉框与 `LoopEase`，风险最低；但与「在缓动列表里多几个选项」的使用直觉不同。
- 也可以两者都做：先按 B 落地逻辑，再按 A 把入口接到原下拉框。

### 3.4 采样密度

按**曲线弯曲程度自适应**细分（四分点弦误差判据），不跟拍线网格。当前实现、容差旋钮与实测节点数见 4.2；这里只保留结论：采样密度由**曲线**决定，不能由拍线网格决定（原因见 4.2 失败记录 1）。

### 3.5 不可逆性与还原

- 拟合会销毁该区间的原始缓动（起点 `easeType` 被改为 0，中间插入新节点）。可立即用 `Ctrl+Z` 撤销（单步）。
- 另提供「清除本段拟合」：删除区间内由拟合产生的节点并把起点恢复为 Linear/原缓动。是否实现待定。

## 4. 已确定的参数（2026-09-23 由使用者决定）

| 项 | 决定 |
| --- | --- |
| 入口位置 | **方向 A：扩展原缓动下拉框**。在 `CanvasEditor.easeType` 原生 19 项之后追加 `In Elastic`、`Out Elastic`、`InOut Elastic` 三项 |
| 曲线集合 | 三条标准弹性曲线：`In Elastic`、`Out Elastic`、`InOut Elastic` |
| 曲线定义 | `value(u) = v0 + (v1 − v0) · f(u)`，`f` 为下表的缓动函数（`f(0)=0`、`f(1)=1`） |
| 幅度语义 | 过冲是曲线自身的性质，没有独立的振幅参数：`Out Elastic` 第一峰约 **35% × 区间变化量**，`In Elastic` 先反向蓄力约 35%，`InOut Elastic` 两端都偏离但幅度较小。因此首尾数值相同的区间仍然等于不动 |
| 作用范围 | **编辑画布 + 编辑摄像机（横向移动）**。画布与相机共用 `KeyPoint`，共用同一套拟合核心 `BakeKeyPoints`；摄像机缩放与编辑线按使用者要求取消（原因见第 1 节范围表） |
| 采样方式 | **按曲线弯曲程度自适应**（四分点弦误差细分），不跟拍线网格 |
| 预设档位 | 三项各一条曲线，均为原式（无强度混合、无档位） |

三条曲线的公式（`c4 = 2π/3`，`c5 = 2π/4.5`）：

| 下拉项 | 公式 | 来源 |
| --- | --- | --- |
| `In Elastic` | `f(x) = −2^(10x−10) · sin((10x − 10.75) · c4)` | 标准弹性族；经 [autogui 文档](https://pub.dev/documentation/autogui/1.3.0/autogui/easeInElastic.html)核对 |
| `Out Elastic` | `f(x) = 2^(−10x) · sin((10x − 0.75) · c4) + 1` | **使用者给定**（与标准式逐字一致） |
| `InOut Elastic` | `x < 0.5`：`f = −(2^(20x−10) · sin((20x − 11.125) · c5)) / 2`；否则 `f = (2^(−20x+10) · sin((20x − 11.125) · c5)) / 2 + 1` | 标准弹性族的 InOut 式（专用周期 `c5`，不是把 In/Out 简单拼接） |

三条公式都由两条数学性质在自动测试里锁定，不靠“看起来对”：

- **镜像关系**：`In(x) = 1 − Out(1 − x)`（100 个采样点逐一比对）。
- **点对称**：`InOut(x) + InOut(1 − x) = 1`，且 `InOut(0.5) = 0.5`（100 个采样点逐一比对）。

下拉项命名沿用原编辑器风格（`In Quad`、`Out Circle`…），它们被追加在原生 19 项之后；插件只把「标签匹配 **且** 下标 ≥ 原生项数」的选项当作自己的，所以将来官方若真的加入同名缓动也不会被劫持。

### 4.1 幅度语义的两个细节（已确认，勿再误读）

1. **就是按首尾差值**：曲线整体按 `(v1 − v0)` 缩放，`v1 = v0` 时全程停在同一个值、插件只提示不插节点。2026-09-23 使用者明确选择**保持相对幅度**，不加绝对单位兜底；因此「原地抖动」在当前语义下做不出来，需要时再讨论绝对幅度档位。
2. 偏离量由曲线自身决定（`Out Elastic` 第一峰约 35%、`In Elastic` 先反向约 35%、`InOut Elastic` 两端都偏离），没有「振幅百分比」参数：想更弱就换 `InOut Elastic`，想只在一端弹就用 `In` 或 `Out`。

### 4.2 采样：按曲线自适应（含三次失败记录）

**实现规则**：对 `u ∈ [0,1]` 做**自适应二分细分**——区间内的**四分点**（1/4、1/2、3/4）与弦的最大偏差超过容差就继续细分。容差以「区间变化量的比例」给（`EaseFit.DefaultTolerance = 0.01`）。导数和曲率大的地方自然密、平坦处自然稀；节点位置**不再对齐拍线网格**。

实测（默认容差、2 拍区间的内部节点数）：`In Elastic` **21**、`Out Elastic` **21**、`InOut Elastic` **25**。`Out Elastic` 在 8 拍区间下仍是 18 个左右（采样在归一化 `u` 上进行，**节点数与拍数无关**），其中 15 个落在前 40% 的弯曲段、`u > 0.7` 的平坦尾段 0 个；同样精度下均匀网格需要 **313** 个。容差就是密度旋钮（下表为粗细对比，供调整时参考）：

| 容差 | 0.005 | 0.01（默认） | 0.02 | 0.03 | 0.05 |
| --- | --- | --- | --- | --- | --- |
| 节点数 | 约 28 | 约 21 | 约 19 | 约 15 | 约 8 |

注意：弹性曲线的「抖动形状」按整段归一化，所以长区间的抖更缓、短区间的抖更急。若需要「每拍固定抖动次数」的语义，要把正弦参数按拍数缩放，属另一套公式。

**失败记录 1（网格锁定，2026-09-23，使用者实测）**：第一版按拍线网格采样、固定 4 个周期。被测区间 2 拍、垂直细分 `1/4` → 网格 8 个间隔，`u = k/8`，`sin(2π·4·k/8) = sin(πk) = 0` —— **每个采样点都落在正弦零点上**（每周期恰好 2 个采样，奈奎斯特极限上的最坏情况），烘出来的折线就是直线。日志证据（`LogOutput.log` 2026-09-23 17:24）：

```text
1:v0   2:1.25/v-0.025   3:1.5/v-0.05   4:1.75/v-0.075   5:2/v-0.1
6:2.25/v-0.125   7:2.5/v-0.15   8:2.75/v-0.175   9:v-0.2
```

值完全是线性插值。**教训：采样密度必须由曲线决定，不能由拍线网格决定。**

**失败记录 2（曲线形状不是使用者要的，2026-09-23）**：第一版用「线性插值 + 衰减正弦偏移」`lerp + A·sin(2π·4u)·(1−u)`，与使用者想要的不符；使用者给出 `easeOutElastic` 后整体替换，随后又按要求改为 `In` / `Out` / `InOut Elastic` 三条标准曲线（无强度档位），采样一并从网格改为自适应。

**失败记录 3（自适应盲区，2026-09-23）**：只探测区间**中点**的判据对点对称曲线失效——`InOut Elastic` 在中点处恰好等于弦的中点值（都是 0.5），第一刀就判定「已是直线」，该曲线**一个节点都没插**。修法：改为同时探测四分点并取最大偏差；回归用例 `inOut elastic is point symmetric` 末尾显式断言该曲线必须采出节点。

节点数超过 4096 时仍按 D-011 给提示但不静默截断。

## 5. 仍未决

1. ~~是否把同样的做法扩展到「编辑相机」与「编辑线」~~ **已结**：相机横向移动已支持；相机缩放与编辑线经使用者决定取消（原因见第 1 节范围表）。编辑线若将来重做，需要换一种与拖动交互一致的入口（例如拖动时按修饰键直接烘焙），而不是复用下拉框。
2. **失败记录 4（摄像机缩放的越界异常，2026-09-23）**：为摄像机追加选项后，使用者在缩放列表上选择插件曲线，出现 `[Error : Unity] IndexOutOfRangeException`。分析：`GetEase.EaseInfors` 只有 `19×101`，而摄像机的缓动下拉**不是**走 `OnEaseTypeChange`（画布那条路已验证可用），插件当时没有拦住，游戏于是把 19 写进了关键点，求值时越界。处理：① 取消缩放支持；② 改在 `Dropdown.set_value` 处事件式拦截摄像机下拉，把非法索引换成 Linear 再交给原版；③ 新增 `EaseTypeGuard`，对任何越界 `easeType` 按 Linear 求值并告警，作为最后一道保险。
2. 是否提供「清除本段拟合」动作（删除拟合产生的节点并恢复起点缓动）。
3. 震荡的周期数与衰减曲线是否需要在界面上可调（当前固定 4 个周期、线性衰减）。

## 6. 验证计划（实现后）

1. 在隔离谱面副本上对一段画布 x 区间施加「震荡」，确认：节点数与预期一致、`Ctrl+Z` 一次回到原状、保存后 JSON 只有 `easeType=0` 的普通节点。
2. 在编辑器里播放，确认画布运动呈现预期波形且没有卡顿。
3. 摄像机横向移动做同样验证；缩放列表选择插件曲线时应只提示不支持、谱面不变。
3. 用原版编辑器重新打开该谱面（不加载插件）确认文件仍然可用。
4. 游戏内验证由使用者进行：需要真机确认密集节点的画布运动与编辑器预览一致。
