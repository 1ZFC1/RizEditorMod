# 调色板导入与清空（D-033）

最后更新：2026-10-29

## 1. 作用

画线、颜色编辑、主题编辑三个面板里的「一键加入调色板」共用同一套实现：把当前谱面的颜色按**出现频率**排到调色板最前面，原有颜色按原顺序紧接在前置块之后；另外提供「清空调色板」。

调色板是编辑器**全局**的 60 个槽位（`ProjectInformation.colorPanels` + `PlayerPrefs` 的 `colorPanels0…59`），切换工程不会清空。

## 2. 收集范围

按 `ChartColorCollection` 的既有来源收集**每一次出现**：

| 来源 | 说明 |
| --- | --- |
| `themes[*].colorsList[*]` | 2 个主题 × 3 色（背景、音符、UI） |
| `lines[*].linePoints[*].color` | 每条线的每个节点 |
| `lines[*].lineColor[*].startColor` / `endColor` | 线色覆盖事件的两端 |
| `lines[*].judgeRingColor[*].startColor` / `endColor` | 判定环事件的两端 |

**跳过 `alpha = 0`**：完全透明色不参与频率统计，也不进入调色板；旧调色板中已有的 `alpha = 0` 槽位不额外清理。（2026-10-29 二次确认：不跳过时透明端点会占据前置区，效果不好。）

## 3. 排序与前插

1. 按 RGBA 四字节精确聚合出现次数（已跳过 `alpha = 0`）。
2. 按次数**降序**；次数相同按**首次出现顺序**（稳定）。这一步决定超过 60 色时哪些颜色能进入。
3. 前置集合 = 去重后的全部 **`alpha > 0`** 谱面颜色，取前 60 个；`x = 前置集合大小`。
4. 新颜色写槽位 `0..x-1`。
5. 旧调色板中不属于前置集合的**非空颜色**保持原顺序，紧接在前置块之后；空槽不参与顺延，统一落在末尾；总容量不足时从尾部丢弃。
6. **去重**：旧槽中与前置集合相同的颜色直接移除，计为「原已存在，已移到前面」。因此同一颜色不会同时出现在前置区和旧区。旧槽之间既有的重复不做额外清理；旧的非前置非空颜色保持相对顺序，**不做整体平移**。

### 示例

容量 6，旧调色板 `[A, B, C, D, -, -]`；谱面颜色按频率为 `[C, E, F]`：

- `x = 3`，前置 `[C, E, F]`；
- 旧槽里的 `C` 被移除，其余旧槽保持顺序 → `[A, B, D, -, -]`；
- 前置块 + 旧的非前置颜色 → `[C, E, F, A, B, D]`；
- 结果：`[C, E, F, A, B, D]`；
- 统计：前插 3、原已存在 1、容量不足移出 0、丢弃 0。

### 重复导入

同一组前置色再次导入时，旧的非前置颜色仍按原顺序紧接在前置块之后，结果与上一次完全相同；**不会再次移动或挤出旧色**。

## 4. 纯白 `FFFFFFFF`

不特殊处理，也不改哨兵值：

- 原版用 `FFFFFFFF` 表示**空槽**；
- 谱面中的纯白仍会参与频率排序并被写到前置区，但写出的值仍是 `FFFFFFFF`，外观与空槽相同；
- 因此纯白无法被“记住”：重复导入时会被再次当作前置色。

## 5. 清空调色板

三个面板各有一个按钮：

- 第一次点击进入确认态，按钮变为「确认清空？」，状态行提示不可撤销；
- 第二次点击把全部槽写回 `FFFFFFFF`，同步 `ProjectInformation.colorPanels` 与 `PlayerPrefs`，并重绘调色板；
- 切换工具、点击面板外、再次点导入或按 `F8` 会取消确认态；
- **全局、不可撤销、不写谱面、不产生 Undo**。

## 6. 状态行

导入结果示例：

```text
已写入调色板：前插 46 色（按频率；4 色原已存在，已移到前面），容量不足移出旧色 0 色，丢弃 0 色（可导入 46 色，已跳过 alpha = 0）。
```

- `前插`：前置集合大小 `x`。
- `原已存在`：旧槽中因与前置色重复而被移除的数量。
- `容量不足移出旧色`：旧的非前置非空颜色因 `front + 旧色` 超过 60 槽而从尾部丢弃的数量。
- `丢弃`：`alpha > 0` 的谱面颜色超过 60 个时，频率排名在 60 名之后的数量。
- `可导入`：跳过 `alpha = 0` 后的去重颜色数。

## 7. 已知副作用

- 第一次导入若 `front + oldRest` 超过 60，会一次性丢弃尾部旧色；**重复导入同样的内容不会再移动或丢弃旧色**。
- 频率排序只影响谱面颜色的先后；旧颜色之间保持原有相对顺序。
- 完全透明的谱面颜色会被跳过；旧调色板中已有的 `alpha = 0` 槽位不会自动清理。
- 清空无法用 `Ctrl+Z` 恢复；需要重新导入或手动重建。
- 纯白与空槽同形，无法在调色板中区分。

## 8. 代码与测试

- 纯逻辑：`src/RhythmEditorMod.Core/ChartColorCollection.cs`
  - `PlanFrequencyInsert(existingSlots, occurrences)` → `PaletteInsertionPlan`
  - `BuildEmptySlots(capacity)`
- 写入/清空：`src/RhythmEditorMod/EditorOperations.cs` 的 `ImportChartColorsToPalette` / `ClearPalette`
- 面板按钮与确认态：`src/RhythmEditorMod/SpeedEasePanel.cs`
- 自动测试（`tests/RhythmEditorMod.Core.Tests/Program.cs`）：
  - `palette orders colours by frequency`
  - `palette frequency ties keep first seen order`
  - `palette reinserts an existing colour without duplicating it`
  - `palette keeps old colours after the front block`
  - `palette caps the front block at the palette size`
  - `palette skips alpha zero colours`
  - `palette repeated import is stable`
  - `palette treats white as a normal chart colour`
  - `palette clear builds empty slots`

## 9. 证据状态

- 调色板 60 槽、`PlayerPrefs` 键名与空槽 `FFFFFFFF`：`confirmed`（CODEMAP/注册表实测）。
- 收集字段与通道：`confirmed`（`CHART_FORMAT.md`）。
- 新策略的排序、前插、旧色顺延、去重、跳过 `alpha = 0`、清空、重复导入幂等：由使用者 2026-10-29 明确指定并修正（D-033），纯逻辑测试覆盖。
- 运行时 UI 行为（按钮位置、确认态、状态行）已由使用者验收（「ok没有问题了」）。
