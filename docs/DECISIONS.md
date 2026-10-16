# 设计决策记录

除非出现新的可靠证据或项目目标变化，不轻易推翻已接受的决定。需要改变时，新增决定说明替代关系，不直接删除历史。

## D-001：保留不可修改的原始副本

- 日期：2026-08-27
- 状态：Accepted

决定：`RhythmEditor_original/` 永远保持只读；研究、运行和修改只使用 `RhythmEditor_working/`。

原因：当前只有闭源 Unity 发布包，任何二进制或资源损坏都可能难以恢复。

## D-002：保持原谱面格式兼容

- 日期：2026-08-27
- 状态：Accepted

决定：默认保持与原游戏和原编辑器的 JSON 互操作；未经证据确认，不删除、改名或重新解释现有字段。

原因：仍需要使用原有谱面生态和工具链。

## D-003：一次只处理一个明确问题

- 日期：2026-08-27
- 状态：Accepted

决定：每个功能任务具有独立的调查、实现、测试、文档和 Git checkpoint。

原因：降低闭源二进制修改的排错和回滚成本。

## D-004：格式知识必须带证据等级

- 日期：2026-08-27
- 状态：Accepted

决定：谱面字段使用 `confirmed`、`likely`、`uncertain`、`unknown` 状态，并记录判断依据。

原因：字段名和样例值不足以证明业务语义、单位和范围。

## D-005：Git 使用补丁/文档覆盖层模式

- 日期：2026-08-27
- 状态：Accepted

决定：普通 Git 初期跟踪文档、脚本、补丁、测试和哈希清单；大型原始发布文件保留在磁盘并由 `.gitignore` 排除。

原因：当前发布包约 236 MB，包含大量无法有效 diff 的二进制。原始 sibling 副本负责恢复干净输入，Git 负责记录我们自己的知识和可重放改动。

如果以后无法用可重放补丁表达必要的二进制修改，再单独评估 Git LFS 或二进制补丁存储方案。

## D-006：当前仅限个人本地修改

- 日期：2026-08-27
- 状态：Accepted

决定：不公开分发原软件二进制、Unity 资源、音乐或嵌入式手册。

原因：当前没有整个编辑器的开源许可或再分发授权证据。

## D-007：不得擅自排序谱面事件数组

- 日期：2026-08-29
- 状态：Accepted

决定：读取、分析、转换和保存谱面时默认保留所有数组的原始顺序。特别是不得按 `time` 自动排序 `lineColor` 或 `judgeRingColor`；其他尚未验证的事件数组也不得为了“整洁”而排序。

原因：运行实验确认 `lineColor` 和 `judgeRingColor` 的原数组顺序直接参与渲染语义。倒序项可能形成空区间，并使后续事件延迟生效；重新排序会改变用户实际看到的谱面效果。原编辑器普通保存和自动保存也会保留这些倒序顺序。

## D-008：流速缓动先验证离散算法，再接入闭源编辑器 UI

- 日期：2026-08-30
- 状态：Accepted

决定：第一版先用可重放的外部转换工具，在隔离谱面副本中把缓动曲线展开为普通突变流速节点。只有算法、位置重算、打开、播放和保存都验证通过后，才修改 IL2CPP 发布程序或加入运行时注入。

原因：当前没有 Unity/C# 源工程。算法和二进制/UI 注入同时发生会增加排错变量；先验证原格式离散结果，可以确认核心方案是否被编辑器和游戏正确接受，同时保持原程序和输入谱面不变。

## D-009：缓动采样密度复用垂直细分

- 日期：2026-08-30
- 状态：Accepted

决定：画布流速缓动不增加独立的采样间隔设置，而是读取编辑器现有的 `ProjectInformation.SnapLinesSplitCount`。`1/N` 垂直细分对应每 `1/N` 拍生成一个速度节点；垂直吸附关闭时按 getter 的既有行为使用 1 拍。

原因：使用者已经通过垂直细分表达当前需要的时间精度。复用同一状态能减少 UI 和设置重复，也让生成节点与当前编辑网格保持一致。

## D-010：流速区间删除使用闭区间并保护首节点

- 日期：2026-08-30
- 状态：Superseded by D-012

决定：普通右键继续删除单个速度节点；`Shift + 右键` 删除当前拍与点击拍之间闭区间内、当前画布上的速度节点。无论区间如何，首个流速节点始终保留。整个批量删除只形成一个 Undo/Redo 步骤。

原因：点击节点需要明确包含在删除结果中，而首节点承担画布初始流速，继续沿用原编辑器的保护规则可以避免生成无初始状态的画布。

后续：该交互在交付前被流速框选复制/剪切需求取代，插件中没有保留 `Shift + 右键` 补丁；首节点保护规则继续用于剪切。

## D-011：大量缓动采样只警告，不静默截断

- 日期：2026-08-30
- 状态：Accepted

决定：缓动展开前先计算预计节点数。超过 4096 个时显示数量警告，由使用者选择取消或继续；不得静默减少采样、改变垂直细分或只生成部分节点。终点不在采样序列上时仍精确保留终点，允许最后一段较短。

原因：`1/64` 与长区间组合可能显著影响编辑器性能，但擅自改变采样密度会使结果偏离使用者当前设置。

## D-012：流速剪贴板复用原矩形框并区分两种粘贴

- 日期：2026-08-30
- 状态：Accepted

决定：`Ctrl+C/X` 复用原 `LineEditor.SelectLines` 矩形，只按上下端拍数闭区间选择当前画布流速；`Ctrl+V` 保持原拍数，`Ctrl+Shift+V` 把最早节点对齐播放头。粘贴覆盖目标同拍节点，剪切保护首节点，每次剪切或粘贴形成一个 Undo/Redo 步骤。

原因：沿用已有框选视觉和标准快捷键可减少新 UI；分离原拍与相对粘贴同时满足精确复现和快速平移，覆盖同拍旧节点可以避免产生含义不明确的重复速度事件。

## D-013：画布事件剪贴板沿用流速剪贴板语义

- 日期：2026-08-31
- 状态：Accepted

决定：画布事件剪贴板只处理当前画布的 `xPositionKeyPoints`，复用原矩形框的纵向闭区间和 `Ctrl+C/X/V`、`Ctrl+Shift+V` 两种粘贴。完整保留事件的 `value/easeType/floorPosition`，粘贴覆盖同拍旧事件，剪切保护首事件，并使用独立于流速的内存剪贴板。

原因：画布事件和流速都沿时间轴排列，统一交互能降低学习成本；保留 `easeType` 可继续使用原编辑器已有缓动，不需要重复实现；独立剪贴板可避免切换标签后误把不同类型数据互相覆盖。

## D-014：使用固定 Mod 文件夹和统一 EXE 管理安装、更新与卸载

- 日期：2026-08-31
- 状态：Accepted

决定：正式导出使用版本化 ZIP、固定内层 `RhythmEditorMod/` 文件夹和单一用户入口 `RhythmEditorMod.Manager.exe`。管理器自动识别上一级编辑器目录，通过精确游戏哈希和 payload 哈希保护安装，统一处理首次安装、更新、启停、插件卸载和完整移除。

原因：干净编辑器还需要 BepInEx、预生成 interop 和标准 metadata，手工复制两个 DLL 不足以运行。固定目录和统一管理器能让后续普通更新只替换变化文件，同时保留安装记录、回滚和安全卸载能力，不要求使用者理解 PowerShell 或 BepInEx 目录结构。

补充：`RhythmEditor_original` 继续作为不可修改基线；正式包安装到独立 `RhythmEditor_modded`。管理器私有运行环境随 Mod 文件夹携带，目标电脑无需预装 .NET。

## D-015：插件面板按工具分工，光标不显示内容

- 日期：2026-09-22
- 状态：Superseded in part by D-018

决定：「突破缩放极限」「预览细线」及面板宽度/透明度滑条只出现在**音符编辑**；**光标**不显示任何插件面板；**发布**只显示谱面统计。发布是否选中，只根据当前 `ToolbarToolButton.toolObject` 上启用的 `PublishControl` 判断，不得因场景中常驻的 `PublishControl` 物体 `activeInHierarchy` 误判。

原因：音符编辑与光标是不同工具；误把常驻发布控件当成已选中会导致光标出现统计、并挡住音符编辑面板。

后续：D-018 把「突破缩放极限」「预览细线」和面板滑条移到光标工具并改名为「基础设置」，音符编辑不再显示插件面板；本决定中关于发布检测「只认当前选中工具、不得按常驻 `PublishControl` 的 `activeInHierarchy` 判断」的部分继续有效，但具体判据已由 D-021 取代（发布的 `toolObject` 实测为 `null`）。

## D-016：Ctrl+D 删除不进软件剪贴板

- 日期：2026-09-22
- 状态：Accepted

决定：`Ctrl+D` 复用原框选与剪切删除路径，但删除后必须恢复删除前的软件剪贴板（含画线工具的 `lineClipboard` / `mirrorLineClipboard`）。Il2Cpp 委托回调通过堆上请求对象传递 `deleteOnly` 等标志，不依赖不可靠的局部量捕获。`Ctrl+X` 仍为剪切。

原因：使用者需要快速删除而不覆盖已有剪贴板内容；深拷贝还原可避免原版 `CutLines` 就地改写同一 Il2Cpp 列表引用。

## D-025：流速列表容忍同拍重复，只拒绝时间倒序

- 日期：2026-09-23
- 状态：Accepted（使用者已验收）

背景：同一拍存在多个流速节点时，插件原先的「必须严格递增」校验直接抛异常，于是**框选删除、复制、粘贴全部失败**（「流速必须已按事件严格排列」）。这类重复是真实存在的（旧版流速缓动生成器可能把两个采样塌到同一拍），而拒绝处理等于让脏数据永远无法清理。

决定：

1. **读取与编辑容忍同拍重复**：校验改为「不得时间倒序」，同拍允许；倒序时明确报出是哪两拍，插件仍不擅自重排数组（`CHART_FORMAT` 要求数组顺序不可自动排序）。
2. **不传播重复**：复制/剪切时把选区内的同拍项按「**后一项优先**」折叠（与游戏对同拍事件取后一项的解析一致），粘贴前也折叠传入剪贴板中的同拍项。
3. **不静默改写谱面**：发现已有重复时只报告（日志列出拍号、状态行提示「可框选删除」），不自动合并——自动合并属于改写用户数据，需另行决定。
4. **堵住插件自身的重复来源**：流速缓动的插入采样必须**严格递增**（同时过滤「塌缩到端点」与「相邻塌缩」两种 float 精度情况）；粘贴覆盖的容差必须与插入容差一致（原为 `1e-6` vs `1e-5`，导致距目标拍 5e-6 的旧节点残留成同拍重复）。

## D-026：插件剪贴板必须携带类型声明的全部数据字段

- 日期：2026-09-23
- 状态：Accepted（使用者已验收）

背景：「编辑线」里 `Ctrl+D` 删除后，编辑器自己的线剪贴板丢失颜色信息。根因是插件的 `LineClipboardEntry` 只保存 `linePoints` + `notes`，而 `Line` **恰好声明四个数据字段**（`linePoints`/`notes`/`judgeRingColor`/`lineColor`，证据见 `CHART_FORMAT.md` 的 `M1`）。`Ctrl+D` 的实现是「快照 → 调用原版 `CutLines` → 写回快照」以模拟「删除但不复制」，残缺快照因此把两个颜色数组抹掉了。

决定：

1. 凡是「快照/还原或导入/导出游戏对象」的剪贴板结构，**必须覆盖该类型声明的全部数据字段**；字段清单以 `CHART_FORMAT.md` 的 `confirmed` 记录为准。
2. 文本格式随之扩展（`lineColor N` / `judgeRing N` 两段），但**向后兼容**：缺少这两段的旧文本仍可解析（颜色事件为空），段存在而格式错误则明确报错，不得静默丢弃。
3. 颜色一律以 `Color32` 字节通道直构，避免 D-020 记录的「经 `Color` 隐式转换被钳成纯白」。

## D-027：预览蒙版开关只隐藏「编辑器蒙版 + 两侧遮挡」，绝不整组隐藏 playMask

- 日期：2026-09-23
- 状态：Accepted（使用者已验收：「达成效果了」）

需求：在**光标**的「基础设置」面板新增 `预览蒙版：开 / 关`。语义由使用者定为**方案 B**：**开 = 保留原版行为；关 = 预览期间抑制蒙版，方便看清全谱面**；原版蒙版默认开启，因此新开关默认「开」。

实测结构（2026-09-23 运行期探针，见 `docs/features/PREVIEW_MASK.md`）：

- 静止编辑只有 `editorMask` 显示；**带预览播放**时原版隐藏 `editorMask`，显示 `blackMask` + `playMask`，停止后自动复原；无预览播放时三者都保持静止态。
- `playMask` 的**两个子物体正好是 `ProjectInformation.backgroundColors`**（两个主题组 `Mask` / `Mask (1)`），每组 = 一个整屏 `Background` + 5 个侧边遮挡（`TopSide`、`BottomSideBack`、`BottomSideForward`、`LeftSide`、`RightSide`，各带 `WhiteBackground` 子物体）。**背景色就住在 `playMask` 里**，所以整组隐藏会让背景色消失——这正是必须避开整组隐藏的原因。

决定：

1. **关** 时隐藏：`blackMask`（编辑器 UI 画布上两片铺满左右半屏的深灰图）＋ `playMask` 两个主题组中**名字命中显式白名单**的侧板（`TopSide`、`BottomSideForward`、`BottomSideBack`、`LeftSide`、`RightSide`；每组 5 块 × 2 组 = 10 块，加 `blackMask` 共 11 个对象）；**保留**各组的 `Background`（背景色）及其它一切。
2. 判据用**显式白名单 + 精确名字比较**，**不用名字后缀**。这些名字里只有三个以 `Side` 结尾（`BottomSideForward`/`BottomSideBack` 分别以 `Forward`/`Back` 结尾）；后缀规则当初正是漏掉这两块，导致「播放头以下（拍数小于播放头）的内容被盖住」。白名单的语义是「**不在名单上的节点永不隐藏**」——只少藏、不多藏。作用域固定在 `ProjectInformation.playMask` 的孙辈两层，不做全场景名字查找，因此不会命中编辑器其它地方的节点。
3. 手段为 `SetActive(false)` + **每帧重申**（原版自己会写这些对象），放在 `LateUpdate`（晚于游戏 `Update`）。
4. 释放规则：**播放停止不还原**（原版已自行复原，避免留下幽灵遮挡）；**播放中拨回「开」**则按隐藏前记录的状态恢复。
5. **不碰 `editorMask`**：它的归属仍是 D-017（横向缩放自动隐藏）。
6. 开关默认「开」，即**默认零干预**。

## D-028：缩放改为等比（投影矩阵横竖同比例），并取消 editorMask 自动隐藏

- 日期：2026-09-24
- 状态：Accepted（使用者已验收：「正常缩放」）；其中**范围与步进部分已由 D-031 取代**（改为 40%–200%、步长 10 个百分点、可放大）

背景：「突破缩放极限」下的 `Ctrl+Shift+滚轮` 缩放原实现只改水平方向（`halfWidth = orthographicSize × aspect / scale`，`halfHeight` 保持 `orthographicSize`），属于**非等比拉伸**：画面横向被压扁、纵向不动；并且为了保住 UI 可见性，`ProtectUiCanvases` 会把 `worldCamera == 编辑器相机` 的 ScreenSpaceCamera 画布搬成 Overlay，代价是**点击命中错位**（bug 清单第 4 条）。

决定：

1. **改成等比**：`halfHeight = orthographicSize / factor`、`halfWidth = halfHeight × aspect`，即横竖按同一比例放大；`camera.orthographicSize` 本身不动（原版仍继续使用它）。缩放范围仍为 **40%–100%**、步进仍 `1.12`、仍只能缩小、`Ctrl+M` 复位、`Ctrl+Shift+滚轮` 触发条件与正交相机检查均不变。
2. **取消 D-017 的「横向缩小时自动隐藏 editorMask」**：整段 `ApplyEdgeMasks()`、状态字段与 `SetMaskActive()` 删除（`D-017` 随之 **Superseded**）。使用者实测该自动隐藏没有实际作用——预览播放时原版自己就会关闭 `editorMask`。
3. 展示随之调整：右上角读out 由「横向缩放 N%」改为「**缩放 N%**」；光标面板底部提示改为「**Ctrl+Shift+滚轮整体缩放，Ctrl+M 复位。**」
4. **仍未解决（另立任务）**：自定义投影矩阵下 ScreenSpaceCamera 画布依旧必须搬 Overlay，所以**「缩放后点不到工具栏」仍然存在**。若将来改成直接改 `camera.orthographicSize` 的真·相机缩放，UI 无需搬动、该问题会一并消失（当前按使用者要求只改现有写法，未采用）。

## D-029：游玩区域边框的几何、颜色与绘制路径

- 日期：2026-09-24
- 状态：Accepted（使用者已验收：「很好，这版符合预期」→「验收通过」）

需求：关闭预览蒙版时，用一个**长方形边框**标出「原本未被遮住、也就是游玩时实际可见」的区域。要求：颜色跟随主题 UI 色（Riztime 会变色）、不随播放移动、不受流速影响、随整体缩放与窗口变化、**线宽固定 2px**。

几何（**全部取世界固定值**，再经 `camera.WorldToScreenPoint` 投影，因此自动随整体缩放与窗口/投影变化；`playMask` 是相机子物体，故不随播放移动）：

| 边 | 来源（`playMask` 与 `editorMask` 取最外/最内） | 世界值 |
| --- | --- | --- |
| 左 / 右 | `LeftSide.min.x` / `RightSide.max.x` 的**外侧**边 | ∓2.813 |
| 下 | `BottomSideForward.max.y`（**排除** `BottomSideBack`，后者上沿 −1.9 高了一格） | −2.9 |
| 上 | `TopSide.max.y` 的**外侧**边（与左右同口径；曾试过「相机可视顶 +5」，使用者要求改为 +4.1） | +4.1 |

颜色：`themes[inChallengeTime ? 1 : 0].colorsList[2]`（谱面 **UI 色**，见 `CHART_FORMAT.md`）——通道是 **0–255**（交叉验证：`themes[0].colorsList[0]` 等于实测背景色 190,185,204），故写入 `GUI.color` 前除以 255。常规 = (179,144,179)、Riztime = (69,46,153)。

绘制时机（满足其一）：**`预览蒙版：关` 且 带预览播放**；**或** 整体缩放 ≠ 100%。挂在 `SpeedEasePanel.OnGUI` 早期（与 `缩放 N%` 同级），不受面板 F8 显隐影响。

硬约束（实测）：**`GUI.DrawTexture` 在本构建被裁剪**（调用报 `NotSupportedException：Method unstripping failed`），`GUIContent(Texture)` 构造也不存在。因此边框只能用 `GUI.Box`：优先「自建 1×1 白纹理 + 纯色 `GUIStyle`」，该重载不可用时自动回退「默认 `GUI.Box` + `GUI.color` 着色」并记一次日志。

## D-030：跨画布线的复制与粘贴由插件接管（Shift 框选整条线、画布整体偏移、锚点取最早拍的最大 x）

- 日期：2026-10-15/16
- 状态：Accepted（使用者验收：「很好，吸附没有问题了」「没有问题了」）

背景：一条线的 `linePoints[].canvasIndex` 是**逐节点**的，所以一条线可以跨多个画布。原版既选不中也粘不对——选择按渲染分组、命中只用拍数（反汇编 `<SelectLines>d__35.MoveNext`），粘贴把每个点直接改写成当前画布且**完全不做画布换算**（反汇编 `LineEditor.Paste`：没有鼠标读取、没有时间换算；实测画布 11 复制、画布 12 粘贴后时间不变、画布被改写）。

决定：

1. **线编辑内的 `Ctrl+C/X` 改由插件启动原版框选协程**（`SpeedEasePanel.Instance.StartCoroutine`，与原版其它工具同一写法，矩形视觉与拍数换算全部复用原版）。
2. **只有拖动期间按住 `Shift`** 才进入跨画布模式：命中判定**限定当前编辑画布**的节点（两轴都要落在矩形内），命中即**整条线**（含其它画布的节点）入选；拖动过程中**实时高亮**（直接写 `LineRenderer.startColor/endColor` 并逐帧重设，因为原版 `LineInformation.SetColors` 在本构建调用即抛异常、且原版每帧重写线色），选完/取消即恢复原色。`Ctrl` 在框选时无作用（不追加）。
3. **剪贴板归属**：Shift 复制 → 写插件剪贴板并**清空原版两个线剪贴板**；原版复制/剪切或系统剪切板导入 → **清空插件剪贴板**。即「最后复制者拥有剪贴板」，避免 `Ctrl+V` 粘到旧内容。
4. **粘贴**（`Ctrl+V` / `Ctrl+Shift+V`，排队到下一帧执行）：锚点 = 全剪贴板中**节拍最小**的节点、同拍并列时取 **x 最大**者（原版口径）；每个节点 `canvasIndex += (目标画布 − 源画布)`、Hold 的 `otherInformations[1]` 同步偏移，新画布不存在则**放弃粘贴**；时间按锚点差平移（音符、Hold 结束拍、颜色关键点一起平移）；x 平移使锚点落在鼠标处，镜像则**以鼠标为对称轴反射**（等价于原版「复制取负 + 对齐鼠标」的合成）。写入包在 `EditControl.Stepping` 内 + `SetFloorPosition()`，**不再**用 `IncOrDecCanvasIndex`/`ChangeCanvasIndex` 强制刷新（会改动画布聚焦甚至失败），改为 0.5 秒后只读复核渲染。
5. **吸附与换算**：局部 x 由**实测映射**求得（同一画布两个已渲染节点反解，实测斜率 **5.625**；插件旧用的单位常数 **5.5625 偏约 1.1%**，已弃用）；基础格距从编辑器**实际画出的** `SnapLines/SnapLine(Clone)` 量得（世界 0.5625 = 局部 0.1）；**吸附步长 = 基础格距 ÷ 水平细分**（`snapHLinesSplit` 的值是细分数：1＝基础格、2＝二等分、4＝四等分），拍吸附沿用垂直细分口径（`GetCanvasPasteAnchor`）。
6. 不按 `Shift` 时**零干预**：原版复制/剪切/粘贴与手感完全不变；`Shift` 剪切暂按复制处理。

原因：原版的数据模型（选择按渲染分组、粘贴压平画布）无法承载跨画布线；而使用者要的是「按可见内容框选、整条线连带跨画布结构一起搬走」。把换算与吸附建立在**实测的渲染几何与真实网格对象**上，可以避开单位常数与细分属性语义的坑。

## D-031：整体缩放范围 40%–200%、步长 10 个百分点（加法），放大与缩小共用同一门闩

- 日期：2026-10-15
- 状态：Accepted（使用者验收：「没有问题」）

背景：D-028 把缩放改成等比投影，但保留了旧的范围与步进方式——`40%–100%`、每格 **乘 1.12**，且 `ApplyHorizontalZoom` 把「scale ≥ 0.999」当作「不缩放」，因此**只能缩小、无法放大**。使用者要求改成 `40%–200%`、步长 10%。

决定：

1. **范围 40%–200%，步长 10 个百分点**，共 17 档（40、50、…、100、110、…、200）。`Ctrl+Shift+滚轮` 每格 ±10 个百分点，**不再是乘系数**；步进后按 10% 栅格取整，避免浮点累积漂移。
2. **放大与缩小共用「突破缩放极限」门闩**：关掉门闩立即复位 100%（同时把音符流速下限恢复 0.1）。判定改为「只有真正 100% 才恢复原版投影」（`ZoomStepping.IsNeutral`），>100% 现在会真正放大。
3. 纯逻辑移入 `src/RhythmEditorMod.Core/ZoomStepping.cs`（`Minimum/Maximum/Step`、`Next`、`Notches`、`Clamp`、`IsNeutral`）并加自动测试（加法步进、跨 100% 双向、40%/200% 夹取、滚轮量→整格）。
4. 文案：面板底部提示固定为「**Ctrl+Shift+滚轮 整体缩放，Ctrl+M 复位。**」；门闩与复位提示统一用「整体缩放」并写明范围与步长。
5. 投影公式本身不变（`halfHeight = orthographicSize / factor`、`halfWidth = halfHeight × aspect`），只是 `factor` 的上限从 1.0 放宽到 2.0。

本决定**取代 D-028 中关于范围与步进的部分**（D-028 的「等比投影」与「删除 D-017 的 editorMask 自动隐藏」两条结论继续有效）。

副作用（已知）：放大时可见世界变小、UI 画布仍会改 Overlay，因此缩放状态下的 UI 点击偏移问题（bug 清单第 4 条）**放大方向同样存在**；缩放不为 100% 时「游玩区域边框」（D-029）照常显示。不改谱面与格式。

## D-032：插件日志规范（信息尽量全、只在事件发生时输出），项目 body 状态行独立

- 日期：2026-10-15/16
- 状态：Accepted（使用者验收：「没什么问题」）

背景：① 插件的公共状态出口 `SpeedEasePanel.ShowStatus`（缓动、剪贴板、缩放复位等所有功能都用它）会**无条件**把同一句话写进自定义「项目」工具的原生 body 状态行，导致在画布流速里设定缓动后，项目面板显示缓动文字 ✗。② 为修此问题而加的「每帧重设标题」把 `SetAllTexts` 的**逐组件日志**也变成了每帧输出（一轮会话 1052 行里 718 行）✗。③ 另有若干日志过于简洁（只写「开始绘制…」「已清空…」），排障时信息不足。

决定：

1. **项目 body 的状态行与标题自持**：`NativeProjectPanel` 保存自己的 `_status` 与标题节点，`Refresh()`（工具选中时每帧）重写两者以抵御外部写入；项目消息（切换结果、槽位为空、选择文件）直连 `NativeProjectPanel.SetStatusText`，**不再**借道插件全局状态；`SpeedEasePanel.ShowStatus` **不再**写项目 body。→ 项目工具与其它功能**互不影响**。
2. **日志规范**：日志信息要尽量完整（包含可复现问题所需的上下文），但**只在动作、状态变化、错误或绘制/抑制的开始与停止时输出**；每帧运行的方法（`ApplyHorizontalZoom`、`Refresh`、高亮重设等）一律不记日志；文本/颜色等周期性重设只在**值真的变化**时记一行。
3. 已按此补全：整体缩放（值/范围/步长/门闩/已保护 UI 画布数/相机正交与尺寸）、项目 body 显隐、状态行变化、游玩区域边框（屏幕矩形/世界边界/颜色/缩放）、预览蒙版隐藏（播放状态）、跨画布剪贴板清空（丢弃量）。
4. 更细的日志取舍（哪些字段值得带上）留待后续按需调整，记为低优先（见 `BACKLOG.md`）。

## D-017：横向缩小时自动隐藏 editorMask

- 日期：2026-09-22
- 状态：**Superseded（由 D-028 取代：整段删除）**

决定：取消手动「关闭边缘遮罩」开关。当横向缩放小于 100% 时自动隐藏 `editorMask`；不改动 `blackMask` / `playMask`。横向范围固定为 40%–100%，由「突破缩放极限」门闩控制是否允许缩小。

原因：缩小后边缘遮罩会挡住内容；自动隐藏比额外开关更符合预期，且避免再次误伤其它遮罩。

## D-018：基础设置面板移到光标工具

- 日期：2026-09-22
- 状态：Accepted

决定：「突破缩放极限」「预览细线」及面板宽度/透明度滑条从**音符编辑**移到**光标**工具，面板标题由「音符编辑」改为「基础设置」。音符编辑工具不再显示任何插件面板；发布仍只显示谱面统计。面板内容与行为不变。

原因：这三项控制的是编辑器全局的显示与缩放，不涉及音符数据；放在「光标」这个未选中具体对象的工具下更符合语义。由使用者明确要求。

取代关系：D-015 中「这些控件只出现在音符编辑」和「光标不显示任何插件面板」两条被本决定取代；D-015 关于发布检测只认当前 `toolObject` 的部分继续有效。本决定中发布检测的具体判据随后被 D-021 取代（发布的 `toolObject` 实测为 `null`），「不留任何已知编辑器激活」这条守卫保留。

光标工具识别（2026-09-22 由运行日志确认）：光标是工具栏中唯一**没有 `toolObject`** 的按钮。插件在每次选择工具时记录

```text
已选择工具：按钮 <按钮名>，toolObject <toolObject 名>
```

实测：光标按钮的 `toolObject` 为 `null`，其余工具的 `toolObject` 分别名为 `Note Editor`、`Line Editor`、`Color Editor`、`Canvas Editor`、`Speed Editor`、`Camera Editor`。场景 `Rizline Editor_Data/level0` 中的 `Cursor`、`Edit Note`、`Edit Line`、`Edit Color & Theme`、`Edit Camera`、`Edit Canvas`、`Edit Speed`、`Edit Time & Speed` 是**工具栏按钮**的名字，按钮指向的工具体才叫 `<X> Editor`；场景引用的 `Assembly-CSharp` 脚本里没有光标专用类型，因此光标没有工具体。

判定条件：按钮非空、`button.toolObject` 为 `null`、**没有直接子物体 `Body`**（这一条由 D-021 补上），且没有任何已知编辑器或发布控件处于激活。

第三条守卫是必要的：`ProgressControl.PlayOrPause` 会调用多次 `GameObject.SetActive`（RVA `0x2BA1F0`），无法静态排除播放或切换工具时编辑器组件被停用；若只按「没有编辑器激活」判断，那种时刻面板会误跳到基础设置。

**更正（2026-09-23，见 D-021）**：本节此处原写「发布不会被误判成光标，因为发布的 `toolObject` 非空」。工具栏探针证明**发布的 `toolObject` 就是 `null`**，因此「无 `toolObject`」这条守卫单独使用会把发布判成光标，而原 `IsPublishToolActive()`（要求 `toolObject` 非空）恒为假、发布统计永不显示。现改为再加一条「没有 `Body` 子物体」才能算光标，发布按「有 `Body`」判定。本节的失败记录与保留 `已选择工具：…` 日志行的做法依然有效，该日志行已在 D-021 中扩展。

失败记录：首版实现把 `Cursor` 误当成 `toolObject` 名来判断，结果光标下始终不显示面板。`LogOutput.log` 里的 `已选择工具：…` 行直接给出了实际的按钮名与 toolObject 名，据此改为按「无 toolObject」判断。保留该日志行正是为了这种情况。

## D-019：颜色剪贴板按框选横向路由，粘贴跟随上次复制

- 日期：2026-09-22
- 状态：Accepted

决定：颜色编辑下的 `Ctrl+C/X` 不再依据 `ColorEditor.editingLineColor` 或当前颜色页签决定处理哪个数组，而是按框选矩形相对**大地 x=0** 的横向位置路由：

| 框选矩形 | 处理 |
| --- | --- |
| 整体在 x=0 左侧 | 只取 `lineColor`（线色覆盖） |
| 整体在 x=0 右侧 | 只取 `judgeRingColor`（判定环） |
| 跨过 x=0 | 两者都取，在同一个 `Stepping` 内完成 |

节拍范围仍只看框选的上下端。`Ctrl+V` 与 `Ctrl+Shift+V` 的锚点语义不变（保持原拍 / 最早事件对齐播放头），但**粘贴哪个数组改为跟随「上一次真正复制到内容的那次复制」**：只复制了线色就只粘线色，两者都复制就都粘。同时拦截原编辑器的 `ColorEditor.CopyColors` / `PasteColors`，由插件独占颜色复制粘贴。

原因：`ColorEditor.editingLineColor` 是 `Nullable<bool>`，经 interop 读取会抛 `NullReferenceException`。`GetActiveTarget()` 把它 catch 掉并**静默回退成** `LineColor`，所以复制必然失败；`TryCopyColorRange()` 没有 catch，直接抛错。2026-09-22 的运行日志里连续 6 次失败，堆栈为 `at ColorEditor.get_editingLineColor()`。原编辑器另有一套颜色复制粘贴（`CopyColors`/`PasteColors`，使用独立的 `clipboard` 列表），两边同时运行会在同一拍写入两条事件，而同刻规则下后者取胜、渲染颜色随之改变。

依据：`LineEditor._SelectLines_d__35._selectionStart_5__2` 是拖动起点的**屏幕坐标**；它与松开时的 `Input.mousePosition` 一起经 `InputControl._camera.ScreenToWorldPoint` 换算，得到框选两端的大地 x。实测 15 组自洽（大地 x=0 投影在屏幕 x=950，110 px = 1 世界单位，相机 `pos=(0,0,-10)`、正交 `size=5`）。`_startPositionX_5__6` 是画布空间量（实测约 `(worldX + 0.157) / 5.5226`），**不得**用于此判断；`_startPositionY_5__7` 存的已经是拍数，因此不能按字段名类推。

没有聚焦画布时原编辑器不会显示框选矩形，插件此时**拒绝开始**并给出提示，同时**不拦截**原编辑器动作，避免 Ctrl+C/X/V 两头都不可用。

## D-020：颜色字段是 Color32；粘贴塌缩同拍并重定向尾部 endColor

- 日期：2026-09-22
- 状态：Accepted

决定：

1. 写入 `ColorKeyPoint.startColor` / `endColor` 与 `LinePoint.color` 必须直接构造 `UnityEngine.Color32`，不得构造 `UnityEngine.Color`。通道语义是 **0–255 字节**。
2. 粘贴到某一拍时，把该拍上已有的**全部**事件塌缩成一条：用粘贴内容改写第一条，删除其余。整个粘贴仍只形成一个 `Stepping`。
3. 粘贴块中**最后一条**事件的 `endColor` 重定向到目标位置：后面还有事件就取它的 `startColor`，否则取自身的 `startColor`。

原因：

1. interop 把这三个字段暴露为 `Color32`（`prop startColor: UnityEngine.Color32`）。把 `Color` 赋给 `Color32` 会走 Unity 的隐式转换 `(byte)(Mathf.Clamp01(c) * 255)`，**任何真实颜色都会被钳成 `(255,255,255,255)` 纯白**。实测：剪贴板里是 `(118,135,229,255)`，写进数组后变成 `(255,255,255,255)`；`time` 是 `float`，所以拍数正确、只有颜色坏掉。同一个 `Channels` 也被 `CreateLine` 用于重建线的节点颜色，因此线剪贴板还原（`Ctrl+D` 恢复路径）同样受影响。
2. `CHART_FORMAT.md` 记录同刻 `lineColor` / `judgeRingColor` 事件**数组靠后者取胜**；旧版本产生的同拍重复会盖掉刚粘贴的颜色，必须一起塌缩才能让粘贴结果真正可见。
3. 按 `CHART_FORMAT.md` 的插值模型 `C(t) = startColor + u * (endColor - startColor)`（`t_i < t <= t_(i+1)`），`endColor` 表示的是**下一个事件时刻**的颜色，也就是选区**之外**那一段。原样搬到别处会把源谱面选区后面的过渡带进来，表现为「复制紫色、粘贴后变白」。

取代关系：`CHANGELOG.md` 早期「颜色框选粘贴只插入不重排」的说法由本决定取代——同拍现在改为覆盖（塌缩为一条），数组其余顺序仍不重排。

## D-021：发布与光标按「有无 Body 子物体」区分，不再要求 toolObject

- 日期：2026-09-23
- 状态：Accepted（代码已改并装入 `RhythmEditor_working`；面板显示待使用者验收）

决定：工具识别分两步，全部只看**当前选中按钮自身**，不做全场景查找。

| 按钮特征 | 判定 |
| --- | --- |
| `toolObject != null` | 对应编辑器工具（`GetPanelMode()` 按已激活编辑器继续分流） |
| `toolObject == null` 且有直接子物体 `Body` | 发布 → `PanelMode.Publish`（谱面统计） |
| `toolObject == null` 且没有 `Body` | 光标 → `PanelMode.Cursor`（基础设置） |

`IsPublishToolActive()` 用「无 `toolObject` 且存在直接子物体 `Body`」判定，并以「该按钮子树内存在 `PublishControl`」作为附加确认（`GetComponentInChildren<PublishControl>(true)`，仍然只在选中按钮子树内查找，因此场景里常驻的 `PublishControl` 不会造成误判）。`IsCursorToolActive()` 增加「没有 `Body` 子物体」这一条守卫。`LogSelectedTool()` 的证据行扩展为按钮名、toolObject 名、`Body` 有无、`PublishControl` 名和最终面板，便于换版本后一次定位。

原因：**发布按钮的 `toolObject` 实际是 `null`**，此前「发布只认 `toolObject` 上的 `PublishControl`」的判定恒为假，产生两个缺陷：选「发布」时发布统计永不显示，且该按钮因 `toolObject == null` 被误判成光标、弹出「基础设置」面板。D-018 里「发布的 `toolObject` 非空」是被探针推翻的假设。

依据：2026-09-23 的工具栏探针转储（`RhythmEditor_working/BepInEx/LogOutput.log`，4 次转储一致）给出 8 个按钮的完整结构：

| 按钮 | sibling | toolObject | 子物体 |
| --- | --- | --- | --- |
| Cursor | 0 | `<null>` | `Icon, Title` |
| Edit Line | 1 | `Line Editor` | `Icon, Line Editor, Title, Body(7)` |
| Edit Note | 2 | `Note Editor` | `Icon, Note Editor, Title, Body(5)` |
| Edit Color & Theme | 3 | `Color Editor` | `Icon, Title, Body(4)` |
| Edit Canvas | 4 | `Canvas Editor` | `Icon, Canvas Editor, Title, Body(5)` |
| Edit Speed | 5 | `Speed Editor` | `Icon, Title, Body(4)` |
| Edit Camera | 6 | `Camera Editor` | `Icon, Title, Body(4)` |
| Publish | 7 | `<null>` | `Icon, Control, Title, Body(5)` |

即只有「光标」和「发布」没有 `toolObject`，两者的唯一结构差异是 `Body` 子物体（发布另有 `Control` 子物体，`PublishControl` 在该子树内）。类型转储另确认 `PublishControl` 没有任何指回按钮的字段（成员只有 `formCanvasGroup`、`titleInput` 等表单控件与 `projectInformation`），因此按钮侧结构是唯一可靠入口；`ToolbarToolButton.bodyHolders` 只包含 `Template`、`ColorSelectorPanel`、`HoverMark` 这类共享面板，不能用来识别工具。

未采用：按按钮名（`"Publish"`）判定。名字是场景对象名，换版本可能变；`Body` 子物体是结构事实，且 8 个按钮全部已验证。

失败记录（保留）：`Body` 这条判定若在别的版本失效，`LogSelectedTool()` 会打出 `Body=无/有` 与 `PublishControl=…`，加上探针转储即可重新定位，不需要再猜。

## D-022：插件工具栏入口用「运行时克隆光标按钮」实现

- 日期：2026-09-23
- 状态：Accepted（已由使用者在 `RhythmEditor_working` 副本验收：标题、鼠标点击、快捷键 `9`、与其它工具互斥均正常；工具栏入口后改名为「项目」并承载工程切换，见 D-024）

后续（2026-09-23 使用者要求）：工具栏入口标题、body 标题栏均由「插件」改为**「项目」**，其 body 改为**克隆真实工具 body**（`Edit Canvas/Body`）并用原生控件搭建，布局参数放进 `BepInEx/config/RhythmEditorMod/layout.json`；**不跟随鼠标**（悬停淡出与交给原版托管两条路线均已撤回，原因见 `docs/features/PROJECT_SWITCH.md` 5.2）。原「面板内容是占位的」说法不再适用。

决定：插件自己的工具栏入口 `CustomToolbarTool`（`src/RhythmEditorMod/CustomToolbarTool.cs`）在运行时克隆**光标按钮**，追加到 `Tools` 容器末尾（`sibling = 8/8`，即「发布」之后），标题「插件」，快捷键 `9`（原工具占 `1`–`8`）。

1. **克隆源是光标按钮**：它是唯一「无 `toolObject` 且无 `Body`」的按钮（D-021 的结构判据，`FindCursorButton` 复用同一规则），子物体只有 `Icon`、`Title`，正好是插件工具需要的全部外观，且不含任何原工具面板。
2. **克隆后调用原版 `Start()`**，让原代码自己完成初始化；随后做选择自检（直接 `SelectTool()` 并检查 `ActiveToolButton` 命中克隆体），自检结束立刻恢复原来选中的工具，保证启动时不抢选择。
3. **快捷键由插件自己处理**：见下方证据 5。按 `9` 时插件调用克隆体的 `SelectTool()`，与鼠标点击走同一条原版路径。
4. **面板按对象身份路由**：`GetPanelMode()` 用 `ActiveToolButton == CustomToolbarTool.Button` 判断，而不是按名字或结构，因此不会被其它工具误命中；路由到新增的 `PanelMode.CustomTool`。当前面板只是占位内容（标题、快捷键、版本、自检结论），后续功能放进去。本次目标仅为机制验证。
5. **一切都是运行时对象**：不写任何游戏文件，编辑器关闭即消失；重启后重建，不做持久化。

原因：编辑器没有任何工具扩展点——工具栏没有被注册表，只有 `ToolbarToolButton.activeTool` 这个静态自引用；改 `GameAssembly.dll` 或 Unity 资源都被项目规则禁止。克隆一个原版按钮是唯一能同时获得正确外观、布局、悬停与选择行为的做法。

依据（2026-09-23 运行日志，`RhythmEditor_working/BepInEx/LogOutput.log`，两次独立运行）：

| # | 证据 | 结论 |
| --- | --- | --- |
| 1 | `ToolbarToolButton 实例数：9`；克隆体 `sibling=8`、`子物体：Icon(0), Title(1)`、`titleCanvasGroup=ok` | 克隆成功并被 `VerticalLayoutGroup` 正常排版 |
| 2 | `自定义工具按钮：标题文本组件是 uGUI Text` | 标题用 uGUI `Text`（不是 TextMeshPro）；标题已改为「插件」 |
| 3 | `自定义工具按钮：图标 Cursor（Image.sprite）` | 图标是 `Image`，克隆自光标，sprite 名 `Cursor` |
| 4 | `自定义工具按钮：光标按钮组件 Transform, Component, Component, Component, ToolbarToolButton`；`GetComponent<Button>()` 返回 `null` | 工具栏按钮**没有 `UnityEngine.UI.Button`**，鼠标点选由 `ToolbarToolButton.Update()` 自己处理，克隆体天然继承，无需插件挂接回调 |
| 5 | `[Warning] 自定义工具按钮：按下 Alpha9 后克隆按钮没有被选中，键盘选择未生效。`（随后由鼠标点击选中） | 原工具栏的键位映射在它启动时就已确定，晚创建的克隆体不在其中，快捷键必须由插件负责 |
| 6 | `已选择工具：按钮 RhythmEditorTool，toolObject <无 toolObject>，Body=无，…，面板 CustomTool`；与 `按钮 Publish，…，Body=有，PublishControl=Control，面板 Publish` 交替出现 | 选择与面板路由正确，且与「发布」互不干扰；`PublishControl` 确认挂在发布按钮的 `Control` 子物体上，D-021 的结构判据得到二次验证 |
| 7 | `选择自检（SelectTool 前 activeTool=Cursor）命中克隆体=True`、`自检后已恢复原工具 'Cursor'` | 克隆体可被原版选择路径选中，且启动时不会抢走选择 |
| 8 | 第三次运行（16:11）按 `9`：`自定义工具按钮：按 Alpha9 选中插件工具（快捷键由插件自行处理）` → `已选择工具：按钮 RhythmEditorTool，…，面板 CustomTool`，随后按 `2` 正常切到 `Edit Line` | 插件自管快捷键生效；与原有工具互斥正常 |

**Il2CppInterop 陷阱（失败记录，重要）**：

- `GetComponentInChildren<T>(bool)` 在本构建里**必定抛** `System.Security.VerificationException: Method Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase.Cast: type argument 'T' violates the constraint of type parameter 'T'`（堆栈指向 `UnityEngine.Component.GetComponentInChildren[T](Boolean)`）。首版实现用它找标题与图标，日志里是 `设置标题失败：…VerificationException` 与 `图标 <读取失败 VerificationException>`，**标题实际没改成功**；D-021 里作为附加确认的 `PublishControl` 查找也因此一直返回空。
- 非泛型重载 `GetComponentInChildren(Type, bool)` 存在，但参数是 `Il2CppSystem.Type`，托管代码无法从 `typeof` 构造，编译期即 `CS1503`。
- 可用替代（现用）：用**已经验证可用**的 `GetComponents<Component>()` 手动遍历子树，再对每个组件 `TryCast<T>()`（`CustomToolbarTool.FindComponent<T>`）。`SpeedEasePanel.FindPublishControl` 也改走这条路径，发布那条日志现在能打出真实的 `PublishControl=Control`。

已知限制与后续：

- 新入口的图标仍是光标的箭头（`sprite` 名 `Cursor`）。要换图标需要另找 sprite，属外观问题，未处理。
- 标题组件实测是 uGUI `Text`。`RhythmEditorMod.csproj` 仍保留 `Unity.TextMeshPro` 引用，只为 `FindComponent<TextMeshProUGUI>` 这条备用分支（万一其它版本改用 TMP）；本构建不会命中。
- 克隆源按结构判据查找；若将来版本改动光标按钮结构，`FindCursorButton` 会失败并在日志中报错（当前会打印「找不到光标按钮，无法克隆」）。
- 面板占位内容已由 D-024 取代：该入口（现名「项目」）的正式用途是编辑期内切换工程；面板也已从 IMGUI 改为克隆原生 body 搭建，见 `docs/features/PROJECT_SWITCH.md` 5.2。

## D-023：额外缓动只能烘焙成已有缓动（Linear）的密集节点

- 日期：2026-09-23
- 状态：**Accepted**（实现完成，曲线与拟合逻辑由 37/37 自动测试覆盖；作用范围在 2026-09-23 由使用者逐项定稿：**编辑画布 ✅、编辑摄像机·横向移动 ✅、摄像机缩放 ❌ 取消、编辑线 ❌ 取消**）

**作用范围的两次收缩（2026-09-23，均为使用者决定；细节见 `docs/features/EXTRA_EASING.md` 第 1 节与「失败记录 4」）**：

- **摄像机缩放取消**：使用者在缩放列表上选插件曲线后出现 `IndexOutOfRangeException`——摄像机的缓动下拉不走 `CanvasEditor` 那条已验证的 `OnEaseTypeChange`，插件没拦住，游戏把 `19` 写进关键点导致 `GetEase` 求值越界。处理：移除缩放支持（一个字段都不写、只提示不支持）、改在 `Dropdown.set_value` 处事件式拦截摄像机的两个下拉（拦截后把值换成 Linear 再交回原版，非法索引永不入谱）、并新增 `EaseTypeGuard` 对任何越界 `easeType` 按 Linear 求值 + 告警，作为最后一道保险。
- **编辑线取消**：曾实现（写入选中线节点的 `linePoints`：x 按曲线、颜色在两端点线性插值），但线工具靠**拖动创建线、Ctrl+拖动改节点属性**，下拉框驱动的拟合与操作习惯割裂，使用者决定改回原版。若将来重做，入口要与拖动交互一致，而不是复用下拉框。

已确定的参数（2026-09-23 使用者决定，细节见 `docs/features/EXTRA_EASING.md`）：入口扩展原缓动下拉框（追加选项；画布 Prefix 拦截 `CanvasEditor.OnEaseTypeChange`，摄像机在其两个下拉的 `Dropdown.set_value` 处拦截，原生 19 项一律放行）；曲线为三条标准弹性 **`In Elastic` / `Out Elastic` / `InOut Elastic`**（`f(0)=0`、`f(1)=1`，`Out` 为使用者给定公式，`In` 与 `InOut` 取自同一标准族并用 `In(x)=1−Out(1−x)`、`InOut(x)+InOut(1−x)=1` 两条性质在测试中锁定）；曲线整体按 `(v1 − v0)` 缩放（首尾相同则不动）；作用于**编辑画布**与**编辑摄像机·横向移动**（共用拟合核心 `BakeKeyPoints`）；采样**按曲线弯曲程度自适应**（四分点弦误差细分），**不沿用 D-009 的拍线细分**——D-009 仍适用于流速缓动。

决定（待批准的核心部分）：

1. 不在谱面里写入 `easeType ≥ 19`。原编辑器的缓动表是 `GetEase.EaseInfors` 的固定 `19 × 101` 表，`GetEaseWithProgress(t, type)` 对 `type ≥ 19` 直接抛 `Il2CppException`，而谱面的插值由**游戏**执行，游戏不认识的 `easeType` 行为未知。
2. 新增的曲线（震荡、回弹等）以**烘焙**方式实现：在选中区间内插入若干 `easeType = 0`（Linear）的普通节点，把目标曲线逼近成折线；起点自身的 `easeType` 也改成 0，终点保持不变。整段写入只形成一个 `Stepping`（一个 Undo 步骤），随后调用现有的 `SetFloorPosition()` 与各工具的 `Refresh` 管线。
3. 采样密度、曲线集合、幅度语义、作用工具（画布／相机／线）与 UI 入口（扩展原下拉框 vs 插件面板）已于 2026-09-23 确定，见本节开头与 `docs/features/EXTRA_EASING.md` 第 4 节。
4. 曲线按**首尾差值** `(v1 − v0)` 缩放，`v1 = v0` 时全程停在同一个值、插件只提示不插节点；使用者明确选择保持相对语义，不加绝对单位兜底。过冲量由曲线自身决定（强度 1.0 时第一峰约 35% × 区间变化量），不再有独立的「振幅百分比」参数。
5. 采样按**曲线弯曲程度自适应**：区间内四分点（1/4、1/2、3/4）与弦的最大偏差超过容差（默认 0.01 × 区间变化量）就继续二分。实测默认容差下 2 拍区间的内部节点数：`In` 21、`Out` 21、`InOut` 25；`Out` 在 8 拍区间为 18 个（15 个落在前 40% 的弯曲段、平坦尾段 0 个），同样精度均匀网格需要 313 个。节点位置不再对齐拍线网格，节点数与拍数无关（采样在归一化 `u` 上进行）。

原因：使用者确认新缓动只能编辑器内自用，游戏只认已有的 19 种；而 2026-09-23 的实测排除了「表里其实有更多类型、只是 UI 没暴露」的可能，也排除了「直接写新 `easeType`」这条路（编辑器自己就会越界抛错）。烘焙是唯一既保留原格式、又能让游戏正确播放的做法，且与流速缓动既有的离散采样思路一致（D-008、D-009、D-011）。

依据（运行实测，2026-09-23，`RhythmEditor_working/BepInEx/LogOutput.log`）：

| 测量 | 结果 |
| --- | --- |
| `EaseInfors.Length` | `1919 = 19 × 101`，等于 1 的下标为 `100, 201, 302, …` → 行距 101，无隐藏类型 |
| `GetEaseWithIndex(index, type)` | `index ≥ 101` 钳到 1；`[0]=0, [50]=0.5, [100]=1`（type 0） |
| `GetEaseWithProgress(t, type)` | `type 0–18` 正常；`type 19/20/24/31` 抛 `Il2CppException` |
| 名称映射校验 | `t=0.5`：type 1=0.25、2=0.75、5=0.875、15=0.134、17=0.7071、18=0.2929 → 与文档的 19 个名称逐一对上 |
| 名称存放位置 | `GameAssembly.dll` 与重建 `global-metadata.dat` 中都没有缓动名；只有场景 `Rizline Editor_Data\level0` 有（每个名字 4 次） |
| 下拉框 | `CanvasEditor.easeType` / `LineEditor.easeType` / `CameraEditor.easeType` 各 19 项、顺序一致；`CanvasEditor.easeTypeEditor` 指向 `Canvas Ease Editor` |

未决（见 `docs/features/EXTRA_EASING.md` 第 5 节）：是否扩展到相机与线、是否提供「清除本段拟合」、周期数与衰减是否可调。

失败记录（保留，2026-09-23）：

1. **首版取段方式错了**：最初按 `CanvasEditor._focusCanvasKeyPointIndex`（「聚焦关键点」）取段，实测该索引在正常编辑中恒为 `-1`，插件因此只提示「请先聚焦一个点」。原编辑器的规则是**按当前拍落在哪个事件的区间内**取段；已改为「最后一个 `time ≤ 当前拍` 的事件，且它后面还有事件」。同一批日志还确认：原编辑器自己的 `OnEaseTypeChange` 在 `_focusCanvasKeyPointIndex = -1` 时不会改动谱面数据，因此该索引不能作为取段依据。
2. **网格锁定的采样把正弦采成了直线**：首版采样数只用垂直细分（每 `1/N` 拍）。被测区间 2 拍、细分 `1/4` → 8 个间隔，而周期固定 4，于是 `u = k/8` 使 `sin(2π·4u)` 在每个采样点都等于 0，烘出的折线是直线。现改为按曲线自适应细分（见上），细分密度不再参与本功能。
3. **首版曲线形状不是使用者要的**：第一版是「线性插值 + 衰减正弦偏移」`lerp + A·sin(2π·4u)·(1−u)`，2026-09-23 使用者给出明确公式（`easeOutElastic`）后整体替换；随后按使用者要求改为三条标准弹性曲线（`In` / `Out` / `InOut Elastic`，无强度档位），幅度语义随之从「可调振幅百分比」变为「曲线固有偏离」。
4. **自适应采样的中点盲区**：只探测区间中点的细分判据对点对称曲线失效——`InOut Elastic` 在中点处恰等于弦的中点值，被判为「已是直线」，一个节点都没插。改为探测四分点后修复；回归用例 `inOut elastic is point symmetric` 显式断言该曲线必须采出节点。

## D-024：编辑器内切换工程复用原版加载链，并在切换前强制保存

- 日期：2026-09-23
- 状态：**Accepted**（使用者已验收功能与 UI：「功能没有问题」，随后按「项目」工具的原生 body 版本确认「可以了，目前就这样吧」）

决定：

1. 「插件」工具栏入口（D-022）的正式用途是**编辑期内切换工程**：记住最近三个工程（谱面 + 音乐），点击即切换；另有「选择文件」入口直接调用原版 `FileControl.OpenProject()`（两步对话框，标题与筛选同原版）。
2. **切换前强制保存**：当前工程未保存时调用 `FileControl.Save()`（它内部本就会先 `AutoSave`）；从未保存过（`filePath` 为空）则**拒绝切换**并提示，不改任何文件。不使用「另存为」对话框，也不提供放弃改动的选项。
3. **加载复用原版道路**：把目标工程的谱面/音乐路径写入 `PlayerPrefs` 的 `lastFilePath` / `lastSongPath`，再调用 `FileControl.OpenLastProject()`，从而复用原版的读取、泛型反序列化、音乐加载协程、`chartJson` 基线重建与 `CheckIfLoadCompleted`。插件不复制加载逻辑。
4. **记忆槽存在插件自己的文件**：`BepInEx/config/RhythmEditorMod/projects.json`，最多 3 条 `{chartPath, songPath, lastOpenedUtc}`，明文可查看可手改；不把编辑器的 `PlayerPrefs` 当作长期存储。（写入 `lastFilePath` / `lastSongPath` 只是切换的瞬时手段。）
5. **不删除任何工程文件**：旧工程的谱面、音乐与其 `Rizline Editor Auto-Save/` 目录都保持原样；自动保存轮换由原版负责。

原因：使用者需要频繁在多个谱面之间来回改，而原版只能退出重进（或依赖启动时自动打开上次工程）。原编辑器的工程状态是「内存中的 `Chart` + 原路径 + 自动保存目录」，没有第三份工作文件；因此切换的关键不是搬文件，而是**保证当前改动已经落到原路径**，然后把原版的加载链跑一遍。直接调用原版 `OpenProject` / `OpenLastProject` 能避免自己重现泛型反序列化与音乐加载细节，风险最低。

依据：`CODEMAP.md` 的「JSON 谱面读取与保存」（打开/保存/自动保存三条主链与 `PlayerPrefs` 键）、类型转储确认的可调用面（`FileControl.OpenProject` / `OpenLastProject` / `Save` / `SaveAs` / `AutoSave`、`ProjectInformation.filePath` / `notSaved` / `ChartIsDirty` / `songCachePath`、`DataManagerController.SelectOpenPath`）。完整方案与风险表见 `docs/features/PROJECT_SWITCH.md`。

待验证（实现前必须先确认）：
1. 同一运行实例中再次调用 `OpenLastProject()` 是否可靠（原版只在启动时用它）——这是本方案的核心假设。
2. `songCachePath` / `songCacheExtension` 的角色与是否需要随工程清理。
3. `PlayerPrefs` 的真实键名。

上述三条已于 2026-09-23 验证完毕（`ProjectSwitchProbe`，细节见 `docs/features/PROJECT_SWITCH.md` 第 5 节）：

1. **`OpenLastProject()` 可在运行中重复调用**：F9 把 `PlayerPrefs` 指回当前工程后重载成功；F12 切到另一份样例后 `filePath` 变为目标、**内存谱面确实更换**（`chart.lines` 230 → 537）。
2. **`songCachePath` 是音乐的临时副本**：`%TEMP%\tmpXXXX.tmp`，内容为 `.ogg`，每次加载生成新文件且**旧文件残留**（`%TEMP%` 下的 `tmp*.tmp` 全是音频，没有 JSON）。因此决定：**切换成功后删除上一个音乐缓存**，且只删「位于系统临时目录、扩展名 `.tmp`、与新缓存路径不同」的文件；任何不满足条件的路径只记录不删除。谱面的工作副本在内存，磁盘上没有临时谱面文件。
3. **`PlayerPrefs` 键确认**为 `lastFilePath` / `lastSongPath`；未保存标记用 **`notSaved`**（`ChartIsDirty` 编辑后仍为 False，不可靠）。

实现落点与验收步骤见 `docs/features/PROJECT_SWITCH.md`；「最近三个工程」列表的排序/去重逻辑放在 Core（`ProjectSlotList`）并有 4 条自动测试。
