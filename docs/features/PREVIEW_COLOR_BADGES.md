# 预览区域颜色角标（D-035）

最后更新：2026-10-29

## 1. 目的

颜色预览区域（基础色/色条、线色/判定环的水滴形）在低 alpha 时同样难以分辨原色。给这些预览加角标：

- 矩形预览：14px 直角边的颜色三角形；
- 水滴形预览：以右下角为圆心的圆，与水滴遮罩取交集，显示为圆 ∩ 水滴的颜色角标。

## 2. 探针确认的结构

| 对象 | 字段 | 尺寸/类型 | 说明 |
| --- | --- | --- | --- |
| 矩形预览 | `ColorSelectorControl.colorPreview` | 98×28，`Image` | 截图里的「基础色」预览 |
| 矩形预览 | `ColorSelectorControl.externalColorPreview` | 98×42，`Image.Type.Sliced` | 色条预览 |
| 水滴形 | `ColorTag.colorBefore` | 60×30，`Image` | 父级下半区，右下角所属颜色 |
| 水滴形 | `ColorTag.colorAfter` | 60×30，`Image` | 父级上半区 |
| 遮罩 | `ColorTag` / `ColorTag/Mask` | `Mask`，sprite=`Mask` | 水滴形裁切 |

- `ColorTag` 有 `Start`，`SetColor(Color)`、`SetColor(Color, Color)`、`SetColor(Color, bool)` 三个重载。
- `ColorSelectorControl` 有 `Start`、`OnEnable`、`SetColor(Color)`。

## 3. 矩形预览（三角）

- 复用调色板的“正方形 `RectMask2D` + 旋转 45° 方块”无纹理方案。
- 固定直角边 **14 px**，内缩 1 px；颜色取预览 `Image.color` 的 RGB，alpha 固定 1。
- 目标：`colorPreview` 与 `externalColorPreview`；没有改预览本身的颜色、尺寸或点击。
- 每个目标下挂 `PreviewColorTriangle`，存在则复用。

## 4. 水滴形（圆 ∩ 水滴）

- 在 `colorBefore`/`colorAfter` 的同一父级下挂 `PreviewColorCircle`：
  - 圆半径 = **水滴圆的半径**（父级短边的一半；60×60 时半径 30，直径 60），与水滴圆弧同尺度；
  - 按“两圆相交面积 = 预览面积 **1/6**”反解两圆心距离；
  - **线色**水滴：圆心放在父级中心到右下对角线；**判定环**水滴：镜像到左下，两者对称；
  - 判定环识别：命名 tag 用 `ColorEditor.currentJudgeRingColorTag` / `judgeRingColorTagPrefab`；事件克隆没有 current 引用，按 `rtl` **反向**判断（日志确认 `LineColorTag.rtl=true`、`RingColorTag.rtl=false` → `rtl=true` 线色右、`rtl=false` 判定环左）；
  - 刷新只在 `ColorTag.Start` / 三个 `SetColor` 重载与 `ColorSelectorControl.Start` / `OnEnable` / `SetColor` 上做；**不再**给 `ColorEditor` 的属性 setter 挂补丁（它们是字段访问器，Il2CppInterop 会回退并打印 `field accessor, it can't be patched` 警告；角标方向靠 `rtl` 回退已足够）。
  - `Image` 使用圆形 Sprite，`raycastTarget=false`，颜色 alpha 固定 1；
  - 父级/祖先的 `ColorTag` `Mask` 会把圆裁成水滴形状，可见部分就是 **圆 ∩ 水滴**。
- 圆心不在矩形角上：矩形右下/左下角通常落在水滴圆弧外；按面积反解后圆心会落在对应对角线靠近圆弧的位置。
- 颜色取 `colorBefore` 的 RGB（右下半区）；`colorBefore` 完全透明时改用 `colorAfter`。
- 圆形 Sprite 优先 `Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd")`；不可用时在已加载 `Sprite` 中找名字含 `knob`/`circle` 的；都没有则记录一次警告并跳过水滴角标。

## 5. 刷新

- `ColorSelectorControl.Start` / `OnEnable` / `SetColor` → 刷新该 selector 的两个矩形预览。
- `ColorTag.Start` + 三个 `SetColor` 重载 → 刷新该水滴。
- `SpeedEasePanel.LateUpdate` 调用 `PreviewColorBadge.Tick()`：只有 `ColorEditorIsActive` 为真且角标父级激活时才显示水滴圆；切到“编辑时间与速度”、线编辑、主题或项目等工具时隐藏，回到颜色工具再显示。
- 只更新角标颜色/尺寸；`Tick` 只遍历已创建的水滴圆列表，不做全场景遍历。

## 6. 风险与边界

- 水滴角标依赖 `ColorTag` 的遮罩形状和圆形 Sprite；缺少圆形 Sprite 时水滴角标不显示。
- 圆形与水滴的交集完全交给现有 `Mask`，不自己计算几何。
- 预览角标是纯显示层：不改 `ProjectInformation.colorPanels`、预览本体、谱面或 Undo；`raycastTarget=false`。
- 6 个 selector 的 2 个矩形预览 + 7 个 `ColorTag` 实例都可能在一次会话中挂角标；刷新只在颜色/初始化事件。

## 7. 代码

- `src/RhythmEditorMod/PreviewColorBadge.cs`：矩形三角、水滴圆、三个 `ColorSelectorControl` Postfix、四个 `ColorTag` Postfix。
- 临时探针：`src/RhythmEditorMod/PreviewUiProbe.cs.disabled`（不参与编译）。

## 8. 验收

已由使用者验收（「没问题了」）。

1. 打开画线/颜色/主题的颜色选择器：基础色预览与色条右下角出现 14px 三角形。
2. 打开颜色编辑，选中线色/判定环事件：两个水滴形预览出现圆 ∩ 水滴的颜色角标。
3. 低 alpha 预览的角标仍为不透明原色。
4. 修改颜色/alpha 后角标同步更新。
5. 预览点击、拖拽、输入 alpha 不受影响。
