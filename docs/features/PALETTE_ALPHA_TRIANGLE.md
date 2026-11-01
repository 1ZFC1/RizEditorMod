# 调色板颜色三角形角标（D-034）

最后更新：2026-10-29

## 1. 目的

调色板槽位是 `Image.color` 叠加在棋盘格背景上，低 alpha 的颜色会被背景冲淡，很难看出原始 RGB。角标用**不透明的颜色三角形**把原始色相直接画在槽位右下角，不写字、不改槽位本体。

## 2. 原版调色板结构（只读探针）

- `ColorSelectorControl` 共 **6 个实例**：画线的 `Edit Line/Body/ColorSelector (1)`，颜色编辑的线色/判定环，主题的 Note/BG/UI 色；每个都有 **60 个** `colorPresets`。
- 每个槽位是 `UnityEngine.UI.Image`，`sprite = null` 的纯白颜色层，棋盘格在它下面；`Image.color` 的 alpha 决定颜色层透明度。
- 面板未打开时 `colorPresets` 全为 `null`，面板激活 / `SyncColorPanels()` 后才初始化。
- Canvas：`ScreenSpaceCamera`、`scaleFactor = 1.05`、`worldCamera = Main Camera`。
- 使用者提供的 24×25 棋盘格方块对应槽位；110×72「基础色」、108×36 色条、两个水滴形是预览/色条，不加角标。

## 3. 绘制方式

- **不创建 `Texture2D` / `Sprite`**：本构建调用这些方法会报 `Method unstripping failed`，因此改用无纹理方案。
- 每个非空槽位下挂 `AlphaColorTriangle` 容器：
  - 容器是**直角边大小的正方形**，锚定槽位右下角、**内缩 1 px 让开黑色边框**，并挂 `RectMask2D`；
  - 容器内放一个 `sprite = null` 的纯白方形 `Image`，边长 = 直角边 × √2；方形中心放在容器右下角，旋转 45°；
  - `RectMask2D` 把方形裁到正方形容器内，只留下右下角的等腰直角三角形；正方形容器保证槽位宽高不一致时也不会歪斜；
  - `Image.color = (slot.r, slot.g, slot.b, 1)`，固定不透明；
  - `raycastTarget = false`，不挡槽位点击；
  - `SetAsLastSibling()`，画在颜色层之上。
- **面积占槽位面积的 1/6**：直角边 = `sqrt(槽位宽 × 槽位高 / 3)`（24×25 槽约 14 px），随槽位尺寸在刷新时调整。
- `FFFFFFFF` 空槽不画；其他非空槽（包括 `alpha = 0`）都画。

## 4. 刷新

- 插件导入/清空后：`PaintPaletteImages()` 末尾调用 `PaletteColorTriangle.RefreshAll()`。
- 面板打开 / 原版改颜色：
  - `ColorSelectorControl.Start` Postfix；
  - `ColorSelectorControl.OnEnable` Postfix；
  - `ColorSelectorControl.SyncColorPanels` Postfix。
- 已存在角标复用；只有颜色真的变化时才写 `Image.color`，避免无意义重建。

## 5. 空槽与纯白

`FFFFFFFF` 同时是空槽和合法纯白，原版格式无法区分。角标按 `FFFFFFFF` 判空，因此纯白槽位不会显示角标；这是哨兵限制，不新增特殊处理。

## 6. 已知风险

- 槽位或其父级若有 `Mask`/`RectMask2D`，内缩 1 px 后的三角形仍可能被裁切。
- 面板首次打开时，角标在 `SyncColorPanels` 后出现，可能有一帧延迟。
- 6 个面板都打开时约 360 个“遮罩 + 方块”组合；刷新只发生在面板事件和导入/清空后，不做每帧遍历。
- 无纹理方案依赖 `RectMask2D`；若该组件或旋转裁剪在本构建不可用，会记录一次警告并且不显示角标。

## 7. 代码

- `src/RhythmEditorMod/PaletteColorTriangle.cs`：`RectMask2D` + 旋转方块的角标创建/刷新、三个 Harmony Postfix。
- `src/RhythmEditorMod/EditorOperations.cs`：`PaintPaletteImages()` 末尾刷新。
- 临时探针：`src/RhythmEditorMod/PaletteUiProbe.cs.disabled`（不参与编译）。

## 8. 验收

已由使用者验收（「ok」）。

1. 打开画线/颜色/主题的调色板，非空槽右下角应出现颜色三角形。
2. 低 alpha 槽位的三角形仍应为不透明原始色。
3. 空槽（`FFFFFFFF`）不出现三角形。
4. 修改槽位 alpha 或颜色后，三角形颜色应同步更新。
5. 点击槽位选择颜色、拖拽/输入 alpha 的行为不变。
6. 6 个调色板面板（画线、线色、判定环、Note/BG/UI 色）表现一致。
