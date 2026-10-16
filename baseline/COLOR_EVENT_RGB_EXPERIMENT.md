# `lineColor` RGB 插值与区间实验

日期：2026-08-29（Asia/Shanghai）

状态：已完成。

## 目标

只调查 `lines[].lineColor[]` 中 `startColor`、`endColor` 与 `time` 的 RGB 插值方向和生效区间。本轮所有 Alpha 都固定为 255，不调查透明度混合，也不改 `judgeRingColor`。

## 隔离文件

目录：`baseline/runtime-artifacts/2026-08-29-color-rgb-01/`

- 原始来源：`#Samples/Sample.json`
- 来源 SHA-256：`1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`
- 测试输入：`test-chart/Color RGB Test.json`
- 输入 SHA-256：`8949E364BC8EAE365C85C1A1A81E1162441ECDF65772D617DFEBEAC27E2E0800`
- 音频：`test-chart/Color RGB Test.ogg`

与来源相比只给第 4 条线，即线节点位于 20、22、24、28 拍的长线，添加了 3 个 `lineColor` 事件：

| `time` | `startColor` | `endColor` | Alpha |
|---:|---|---|---:|
| 20 | 红 `(255,0,0)` | 红 `(255,0,0)` | 255 |
| 24 | 红 `(255,0,0)` | 蓝 `(0,0,255)` | 255 |
| 28 | 蓝 `(0,0,255)` | 蓝 `(0,0,255)` | 255 |

该线所有 `linePoints[].color` 均为不透明黑色，原有 `judgeRingColor` 保持不透明黑色。因此观察到的整线 RGB 变化应来自新增 `lineColor`。

## 人工观察步骤

1. 打开 `test-chart/Color RGB Test.json`。
2. 从第 19 拍之前开始播放，慢速观察第 4 条长线。
3. 分别在第 19、20、22、24、26、28 拍附近暂停，记录整线颜色。
4. 记录颜色是连续渐变还是瞬间跳变，以及暂停时整条线是否为单一颜色。
5. 观察判定环是否仍为黑色。
6. 完成观察后，把该线 Hold 的结束拍从 28 改为 26，按 `Ctrl+S` 保存并正常退出，以验证保存是否保留颜色事件。

## 观察结果

用户报告：

- 第 19 拍：红色；
- 第 20 拍：红色；
- 第 22 拍：红色；
- 第 24 拍：红色，并从此开始变化；
- 第 26 拍：紫色；
- 第 28 拍：蓝色；
- 变化方式：连续渐变；
- 暂停时：整条线为单一颜色，而不是沿线渐变；
- 判定环：仍为黑色。

这与以下时间模型一致：

```text
event[i].time
    颜色 = event[i].startColor
          ↓ 连续插值
event[i + 1].time
    颜色 = event[i].endColor
```

在本例中，`time=20` 的红→红事件控制 20–24 拍，`time=24` 的红→蓝事件控制 24–28 拍。因此第 22 拍仍为红色，第 26 拍为中间紫色。第 19 拍也显示为首事件的红色，说明在本次测试范围内，首事件之前使用首事件起始色。

用户根据连续变化和中点颜色进一步确认，RGB 使用逐通道线性插值。对第 `i` 个事件，在 `t_i <= t <= t_(i+1)` 时：

```text
u = (t - t_i) / (t_(i+1) - t_i)
C(t) = startColor.C + u * (endColor.C - startColor.C)
C ∈ {r, g, b}
```

核心插值公式标记为 `confirmed`。整数舍入/截断、范围钳制以及内部色彩空间没有单独测量，仍未知。

## 保存结果

用户把 Hold 结束拍从 28 改为 26，按 `Ctrl+S` 保存并正常退出。

普通保存：

- 文件：`test-chart/Color RGB Test.json`；
- SHA-256：`99FBA2090555375A0CF3FEA459A01E9EA7DCA2498B2692AF4C9159063FD0FD8C`；
- 3 个 `lineColor` 事件与输入逐字段一致；
- 相对输入只有以下两个预期变化：

```text
lines[3].notes[8].otherInformations[0]: 28.0 -> 26.0
lines[3].notes[8].otherInformations[2]: 16.0 -> 14.8571424
```

3 个自动保存文件也都逐字段保留了颜色数组。其中：

- `auto-save 2` 与普通保存具有相同的两项 Hold 差异；
- `auto-save 1` 中旧 Hold 已不存在、新 Hold 尚未出现，表现为一次编辑中间状态；
- `auto-save latest` 已出现结束拍为 26 的 Hold，但该音符的两个派生位置仍为 0，也表现为尚未完成重算的中间状态；
- 按写入时间，`auto-save 2` 和普通保存晚于 `auto-save latest`，且包含最终正确派生位置。

这说明颜色事件保存正常；同时，本轮旁证显示自动保存目录可能保留编辑操作的瞬时中间状态，文件名 `latest` 也不一定代表最后写入的完整状态。对产生时机的解释属于推测，该现象需要另立任务验证，不能仅凭本轮断言普遍规则。

## 结论

- `lineColor[i].time` 是该颜色事件插值的开始拍：`confirmed`。
- 从 `lineColor[i].time` 到 `lineColor[i+1].time`，整线颜色由 `startColor` 连续变化到 `endColor`：测试模型 `confirmed`。
- 同一暂停时刻整条线使用单一覆盖色，不沿线位置显示本事件的 RGB 渐变：`confirmed`。
- 本次 `lineColor` 变化不影响黑色判定环：`confirmed`。
- RGB 的 `r`、`g`、`b` 三个通道使用线性插值：`confirmed`。
- 首事件之前使用首事件起始色：仅在第 19 拍测试，暂记 `likely`。

## 当前限制

本轮不会回答 Alpha 混合、`judgeRingColor` 是否使用相同时间模型、相邻事件颜色不连续时的边界归属、同刻冲突和事件数组倒序时的优先级；这些需要分别做单变量实验。
