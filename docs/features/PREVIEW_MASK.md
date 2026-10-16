# 预览蒙版（`PreviewMaskControl`）

状态：**已实现并验收**（2026-09-23）。相关：`DECISIONS.md` D-027、D-017、`CHART_FORMAT.md`（背景色数据在谱面里的位置）。

## 1. 需求与语义

在**光标**工具的「基础设置」面板新增 `预览蒙版：开 / 关`。

- **开（默认）**：保留原版行为，插件**零干预**。
- **关**：仅在**带预览的播放**期间抑制蒙版，方便看清全谱面。
- 使用者备注：原版蒙版默认开启，所以新开关默认也是「开」。

## 2. 实测结构（2026-09-23 运行期探针）

| 对象 | 路径 | 静止编辑 | 带预览播放 |
| --- | --- | --- | --- |
| `editorMask` | `Editor/GameSceneCanvas/Main Camera/EditorMask` | **显示** | 隐藏（原版自己收起） |
| `blackMask` | `Editor/Canvas/Black mask` | 隐藏 | **显示** |
| `playMask` | `Editor/GameSceneCanvas/Main Camera/PlayMask` | 隐藏 | **显示** |

- 判据：`ProjectInformation.isPlaying && !ProjectInformation.isPlayingWithoutPreview`（与「预览细线」同一判据）。**无预览播放**时三者都保持静止态。
- `blackMask`：编辑器 UI 画布上两片 `Image`（`Image (8)` 锚点 `(0.5,0)-(1,1)`、`Image (9)` 锚点 `(0,0)-(0.5,1)`，各约 646×1080），颜色 `RGBA(0.157,0.157,0.157,1)` —— 就是它遮住屏幕**两侧**。
- `playMask`：两个子物体 `Mask`、`Mask (1)` **正好等于 `ProjectInformation.backgroundColors`**（两个主题组，与谱面普通 / Riztime 两套主题对应），每组结构相同：

```
Mask（或 Mask (1)）
├─ Background           2000×2000           ← 整屏背景色（必须保留）
├─ TopSide              └ WhiteBackground   ← 侧边遮挡
├─ BottomSideBack       └ WhiteBackground   ← 名称以 Back 结尾，规则上不隐藏
├─ BottomSideForward    └ WhiteBackground   ← 侧边遮挡
├─ LeftSide             └ WhiteBackground   ← 侧边遮挡
└─ RightSide            └ WhiteBackground   ← 侧边遮挡
```

**关键结论：背景色住在 `playMask` 里面**，因此整组隐藏 `Mask` 会连带失去背景色；要「保留背景色 + 显示两侧内容」，只能**只隐藏侧边遮挡**。

## 3. 实现

| 项 | 做法 |
| --- | --- |
| 隐藏对象 | `blackMask` ＋ `playMask` 两个主题组中**名字命中显式白名单**的侧板：`TopSide`、`BottomSideForward`、`BottomSideBack`、`LeftSide`、`RightSide`（每组 5 块 × 2 组 = 10 块，加 `blackMask` 共 **11 个对象**） |
| 保留对象 | 各组的 `Background`（背景色）、`editorMask`、相机、谱面、主题等一切其它内容 |
| 判据 | **显式白名单 + 精确名字比较**（`string.Equals(..., Ordinal)`），**不用名字后缀**；作用域固定为 `ProjectInformation.playMask` 的孙辈两层，不做全场景查找 |
| 手段 | `SetActive(false)`，并在 `SpeedEasePanel.LateUpdate` 中**每帧重申**（原版自己会写这些对象） |
| 恢复 | 播放停止 → 不还原（原版已自行复原，避免留下幽灵遮挡）；播放中拨回「开」→ 按隐藏前记录的 `activeSelf` 恢复 |
| 面板 | 光标面板第三行 `预览蒙版：开 / 关`（默认开），下方一行状态文案；面板高度 300 → 336 |

实现文件：`src/RhythmEditorMod/PreviewMaskControl.cs`；面板接线在 `SpeedEasePanel.cs`（`_previewMaskRect`、`TogglePreviewMask`、`Update`/`LateUpdate` 调用）。

### 3.1 为什么用显式白名单而不是名字后缀

- 这些节点的名字是 `TopSide`、`BottomSideForward`、`BottomSideBack`、`LeftSide`、`RightSide`——**只有三个以 `Side` 结尾**。第一版用 `EndsWith("Side")`，两个底部板块从未被隐藏，这正是「播放头以下内容被盖住」的原因。
- 后缀规则还会在将来误伤：若原版新增 `GuideSide`、`BackSide` 之类非遮挡节点，会被静默隐藏。白名单的语义是「**不在名单上的节点永不隐藏**」，只少藏、不多藏。
- 作用域本身已经很窄（只遍历 `playMask` 两个主题组的直接子物体），所以白名单之外没有额外风险面。

### 3.2 其它可选的关闭方式（评估过，未采用）

| 方式 | 说明 | 结论 |
| --- | --- | --- |
| 关闭渲染组件 `Renderer.enabled = false` | 对象保持 active，不占用原版的激活开关；还原只需改回 `true` | 更精细，但探针未能解析出这些节点的组件类型名，需运行时 `TryCast<Renderer>`；作为将来可选的改进保留 |
| `CanvasGroup.alpha = 0` | 不改激活状态 | 这些节点**没有** `CanvasGroup`，不适用 |
| 改颜色 / 材质 alpha | 需写 `sharedMaterial` 或渲染器颜色 | **风险高**：`WhiteBackground` 可能共用材质，会连带影响其它物体；也可能被动画覆盖 |
| 移动 / 缩放 / 换 layer / 相机剔除 | 不动激活开关 | 改动几何或相机剔除面，与原版每帧写入及与其它对象互相干扰的风险更高 |
| `SetActive(false)`（**采用**） | 简单彻底，含子物体 | 与原版共用激活开关，故必须每帧重申；这些是纯四边形，无脚本副作用 |

## 4. 失败/弯路记录

1. **整组隐藏 `playMask`** → 背景色消失（因为 `backgroundColors` 就是它的两个子物体）。使用者实测：「关闭 playMask 会导致带预览播放不显示背景色」。
2. **只隐藏 `Mask` 或只隐藏 `Mask (1)`**（逐个隔离测试）→ 两侧遮挡**依旧**：因为两个主题组各有一套完全相同的侧边遮挡，隐藏一组另一组仍挡着。使用者实测三次均报「两侧遮挡在」。
3. **只隐藏 `blackMask`** → 背景色保留，但两侧仍被 `playMask` 的侧边遮挡挡住。
4. **`blackMask` + 以 `Side` 结尾的 3 块侧板** → 两侧通过，但**播放头以下（拍数小于播放头）的内容仍被盖住**：后缀规则漏掉了 `BottomSideForward` 与 `BottomSideBack`，而这两块的 `WhiteBackground` 是所有侧板里最大的两块（高 748.5 / 648.7）且位于 y 负方向。
5. **改为显式白名单并补齐 5 类侧板** → 验收通过：背景色保留、四周与播放头以下的内容全部可见，无副作用。

## 5. 兼容性与风险

- 开关默认「开」＝零干预；只有显式关闭后才在预览期间动手。
- 不触碰 `editorMask`，D-017 的横向缩放逻辑不受影响。
- 若将来的编辑器版本更名或去掉这些节点，白名单自然匹配不到（只少藏、不报错），日志会打印实际隐藏的对象个数与名字，便于发现结构变化。
