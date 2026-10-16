# `lineColor` Alpha 混色实验

日期：2026-08-29（Asia/Shanghai）

状态：已完成第一轮（基础 Alpha=255）。

## 目标

只调查不透明基础线下，`lines[].lineColor[]` 的 Alpha 如何随时间变化，以及覆盖色如何与基础 RGB 混合。本轮固定基础色、覆盖 RGB 和事件时间模型，不测试基础线自身 Alpha 的限制，也不改 `judgeRingColor`。

## 隔离文件

目录：`baseline/runtime-artifacts/2026-08-29-color-alpha-01/`

- 原始来源：`#Samples/Sample.json`
- 来源 SHA-256：`1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`
- 测试输入：`test-chart/Color Alpha Test.json`
- 输入 SHA-256：`48B8CA8888FB75622427D9FABE2A9FE67C7C26E7175509945E6523DFE5FDE918`
- 音频：`test-chart/Color Alpha Test.ogg`

第 4 条长线的 4 个 `linePoints[].color` 固定为不透明绿色 `(0,255,0,255)`。`lineColor` 的覆盖 RGB 固定为红色 `(255,0,0)`，只改变 Alpha：

| `time` | `startColor.a` | `endColor.a` | 控制区间 |
|---:|---:|---:|---|
| 20 | 0 | 0 | 20–22 拍保持 0 |
| 22 | 0 | 128 | 22–24 拍从 0 变化到 128 |
| 24 | 128 | 255 | 24–28 拍从 128 变化到 255 |
| 28 | 255 | 255 | 末项控制值 |

上一轮已确认颜色事件在本项 `time` 取 `startColor`，并在下一项 `time` 到达 `endColor`。因此本轮只观察 Alpha。

## 待验证模型

候选的常规 Alpha 覆盖公式为：

```text
a = lineColor.a / 255
finalRGB = baseRGB * (1 - a) + lineColorRGB * a
```

如果使用该公式，则 Alpha 0 应完全显示基础绿色，Alpha 128 应显示红绿各约一半的中间色，Alpha 255 应完全显示覆盖红色。这里只是待验证模型，不是当前结论。

## 人工观察步骤

1. 打开 `test-chart/Color Alpha Test.json`。
2. 从第 19 拍之前播放到第 28 拍以后。
3. 在第 19、20、22、23、24、26、28 拍附近暂停，记录整线颜色。
4. 记录颜色是否连续变化，以及 Alpha 0/128/255 是否分别对应无覆盖/半覆盖/完全覆盖。
5. 记录黑色判定环是否保持不变。
6. 在第 20、24、28 拍暂停时分别截取同一区域，供像素颜色对照。
7. 完成观察后，把该线 Hold 的结束拍从 28 改为 26，按 `Ctrl+S` 保存并正常退出。

## 观察结果

用户为了便于观察，临时删除了第 24 拍开始的 Hold，并在保存前重新添加。用户报告：

- 第 19、20、22 拍：最终线 Alpha 均保持 255，Alpha 0 时完全显示基础绿色；
- 第 22 拍后：颜色开始由绿色向红色渐变；
- 第 23 拍：最终线 Alpha 仍为 255；
- 第 24 拍：事件 Alpha 为 128，最终线 Alpha 仍为 255，RGB 为绿色与红色的混合色；
- 第 26 拍：最终线 Alpha 仍为 255；
- 第 28 拍：事件 Alpha 为 255，最终线 Alpha 仍为 255，完全显示红色；
- 判定环始终保持黑色。

用户提供了第 20、24、28 拍的截图。三张图分别显示绿色、暗黄/橄榄色和红色；中间色符合在当前 RGB 通道值上进行约一半权重混合，而不是把整条线变成半透明。

## 混色结论

在本轮基础色 Alpha 为 255 的条件下，`lineColor.a` 是覆盖 RGB 的混合权重，不是最终线 Alpha：

```text
w = lineColor.a / 255
finalRGB = baseRGB + w * (lineColorRGB - baseRGB)
finalA = 255
```

其中基础绿 `(0,255,0)` 与覆盖红 `(255,0,0)` 在 `a=128` 时得到暗黄/橄榄色，符合直接逐 RGB 通道线性混合。核心公式在基础 Alpha=255 的测试域内标记为 `confirmed`；具体整数舍入仍未知。

Alpha 事件也沿用相邻颜色事件的时间区间：22 拍从 0 开始，24 拍到达 128，随后到 28 拍到达 255。颜色连续由绿转红，支持 Alpha 通道同样按时间线性插值，标记为 `confirmed`。

## 保存结果

用户重新添加 Hold，将结束拍设为 26，按 `Ctrl+S` 保存并正常退出。

普通保存：

- 文件：`test-chart/Color Alpha Test.json`；
- SHA-256：`276536DD88B7DF881E5AA95E93C5EE2AF149631D1C1241B9DF0A8839198BEF9D`；
- 4 个 `lineColor` 事件与输入逐字段一致；
- 相对输入只有以下两个预期变化：

```text
lines[3].notes[8].otherInformations[0]: 28.0 -> 26.0
lines[3].notes[8].otherInformations[2]: 16.0 -> 14.8571424
```

3 个自动保存文件也都逐字段保留了颜色数组。`auto-save 1` 是没有 Hold 的编辑中间状态；`auto-save 2`、`auto-save latest` 与普通保存包含相同的最终 Hold 数据。本轮 `latest` 最后写入并与普通保存字节完全相同。

## 结论

- 当基础 Alpha=255 时，`lineColor.a` 控制基础 RGB 与覆盖 RGB 的线性混合权重：`confirmed`。
- Alpha 0 完全保留基础 RGB，Alpha 255 完全使用覆盖 RGB，Alpha 128 约为等权混合：`confirmed`。
- 本轮所有时刻最终线 Alpha 都保持 255；覆盖 Alpha 不是最终透明度：`confirmed`（仅限基础 Alpha=255）。
- `startColor.a` 到 `endColor.a` 随相邻事件时间线性变化：`confirmed`。
- `lineColor` Alpha 变化不影响黑色判定环：`confirmed`。
- 普通保存和自动保存均保留全部 Alpha 事件：`confirmed`。

## 当前限制

本轮结果只适用于基础 Alpha 为 255 的线。基础 Alpha 为 0 或中间值时的最终透明度与混色公式、`judgeRingColor` Alpha、边界不连续和同刻冲突需要单独实验。
