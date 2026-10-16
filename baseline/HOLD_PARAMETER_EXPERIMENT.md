# Hold `otherInformations` 参数实验

日期：2026-08-28（Asia/Shanghai）

## 目标

验证 type 2 / Hold 音符的：

```text
otherInformations = [?, ?, ?]
```

实验只使用 `baseline/runtime-artifacts/` 中的隔离副本，没有修改 `#Samples/` 或编辑器发布文件。

## 测试对象

来源：`#Samples/Sample.json`

该文件只有一个 Hold：

- `lines[3].notes[8]`
- 开始拍：24
- 结束拍：28
- 原始 `otherInformations`：`[28.0, 0.0, 16.0]`

原始与隔离 before 文件：

- 长度：9947 字节
- SHA-256：`1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`

## 第一轮：只修改结束点

操作：使用编辑器把唯一 Hold 的结束拍从 28 改为 26，其他可编辑项保持不变，然后 `Ctrl+S`。

保存后文件：

- 长度：9953 字节
- SHA-256：`6399BC166B97C28D6D8A57357DB31F7D726A21C24FBE92CF7467EFA363CCBBFC`

全 JSON 递归比较只发现两处变化：

```text
lines[3].notes[8].otherInformations[0]: 28.0 -> 26.0
lines[3].notes[8].otherInformations[2]: 16.0 -> 14.8571424
```

结论：

- `[0]` 是 Hold 结束拍：`confirmed`。
- `[2]` 是 Hold 结束点对应的 `floorPosition`：`confirmed`。
- 编辑器在移动结束点时自动重算 `[2]`。

## 第二轮：缓动假设检查

原计划只把 Hold 缓动从 0 改为 3，但编辑器没有 Hold 缓动选项。用户同时确认：

- Hold 的形状始终垂直于直线 `y=0`。
- Hold 跨过一条线的多个节点时，其显示高度会受到各节点不同速度的影响。

第二轮测试文件保持原样，SHA-256 与 before 相同。因此不能把 `[1]` 解释为用户可编辑的 Hold 缓动。

## `[1]` 的静态相关性

对现有 18 个 JSON 中全部 512 个 Hold 统计：

- `[1]` 的实际值为整数 0–17，共出现 15 个不同值。
- 512/512 都能在同文件 `canvasMoves[].index` 中找到。
- 512/512 都能在所属线的 `linePoints[].canvasIndex` 中找到。
- 335 个只引用单一画布的线中，335/335 的 `[1]` 都等于该画布编号。
- `[1]` 与 Hold 起点附近前置线节点的 `canvasIndex` 在 490/512 项一致。
- `[1]` 与对应线节点 `easeType` 仅在约 10% 的项目中偶然一致。

后续 `floorPosition` 公式检查使用 `[1]` 指定的画布计算结束位置，512/512 个 Hold 都在 0.01 容差内吻合。因此 `[1]` 确认是用于 Hold 结束位置计算的 `canvasIndex`，而不是 `easeType`。

进一步的全语料检查和跨画布运行实验确认：`[1]` 等于结束拍之前最后一个 `linePoint` 的 `canvasIndex`，判断使用严格 `< endTime`。移动结束点跨过线节点边界时，编辑器会自动更新 `[1]` 和 `[2]`。详见 `baseline/CROSS_CANVAS_HOLD_EXPERIMENT.md`。

## 当前候选结构

```text
otherInformations = [endTime, canvasIndex, endFloorPosition]
```

可信程度：

- `endTime`：`confirmed`
- `canvasIndex`：`confirmed`
- `endFloorPosition`：`confirmed`
