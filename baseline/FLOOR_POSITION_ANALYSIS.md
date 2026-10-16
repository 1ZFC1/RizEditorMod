# `floorPosition` 静态公式分析

首次记录：2026-08-28（Asia/Shanghai）

变 BPM 补充：2026-08-29（Asia/Shanghai）

## 起点

用户提出：`floorPosition` 可能表示某个拍数对应的秒数，即拍数与秒之间的转换结果。

`#Samples/Sample.json` 提供了直接例子：

- BPM：105
- 拍数 24：`24 × 60 / 105 = 13.7142857`，样例为 `13.7142859`
- 拍数 28：`28 × 60 / 105 = 16`，样例为 `16.0`
- Hold 实验将结束拍改为 26 后：`26 × 60 / 105 = 14.8571429`，编辑器保存为 `14.8571424`

在画布速度为 1 时，`floorPosition` 确实等于拍数换算得到的秒数。

## 速度加权公式

当 BPM 和画布速度在一个区间内保持不变时，样例支持：

```text
ΔfloorPosition = Δbeat × 60 / BPM × speed
```

更一般的候选形式是：

```text
floorPosition(t) = ∫ speed(beat) × 60 / BPM(beat) dbeat
```

现有 18 个样例的 `bpmShifts` 都只有默认值，因此本报告最初只能确认速度积分部分。后续 `baseline/BPM_SHIFT_EXPERIMENT.md` 增加一次 105→210 BPM 的隔离实验，并确认变 BPM 时采用同类分段积分。

因此 `floorPosition` 更准确的描述是“速度加权的累计时间/滚动坐标”，也可以理解为速度为 1 时以秒表示的等效位置。它不总是真实播放秒数。

## 全语料验证

工具：`tools/Analyze-FloorPosition.ps1`

命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Analyze-FloorPosition.ps1 `
  -ChartRoot .\#Samples `
  -Tolerance 0.01
```

结果：

| 检查 | 匹配数 | 说明 |
|---|---:|---|
| Hold 结束位置，使用 `otherInformations[1]` 画布进行速度积分 | 512/512 | 最大误差约 0.0084 |
| Hold 结束位置，使用最近速度关键点作锚 | 512/512 | 最大误差约 0.0041 |
| Hold 结束位置，完全忽略速度只做拍数换秒 | 381/512 | 不能解释全部样例 |
| 线节点，使用最近速度关键点作锚 | 9332/9336 | 4 个例外，最大误差 0.5 |
| 速度关键点，从拍 0 直接积分 | 329/334 | 5 个明显例外，最大误差约 23.33 |

共有 109 个 Hold 的 `endFloorPosition` 与简单拍数换秒相差超过 1。这证明画布速度不能忽略。

## Hold 中间参数

全部 512 个 Hold 使用 `otherInformations[1]` 作为画布编号时，其结束 `floorPosition` 都能在 0.01 容差内由速度积分解释。因此：

```text
otherInformations = [endTime, canvasIndex, endFloorPosition]
```

三个位置的基本含义均可标记为 `confirmed`。跨画布时编辑器如何选择或更新该 `canvasIndex` 仍未知。

## 变 BPM 补充验证

在画布速度恒为 1 的 `Sample.json` 中，第 `8.000001` 拍新增 `value=2.0` 的 BPM 变化点。基础 `bPM=105`，因此有效 BPM 为 210。编辑器自动重算了变化点后的 30 个位置字段，全部在 `10^-6` 容差内符合：

```text
ΔfloorPosition = Δbeat × 60 / (bPM × bpmShift.value)
```

最大绝对误差约 `8.93 × 10^-7`。这确认单次 BPM 变化采用分段积分。多次变化、与画布速度同刻变化以及精确边界规则仍未验证。

## 例外与风险

- 少量速度关键点无法由拍 0 的默认状态直接积分得到，可能存在显式位置锚、重置、编辑遗留值或尚未识别的规则。
- 少量线节点即使用最近关键点作锚仍有偏差。
- 多次 BPM 变化、与画布速度同刻变化及精确边界归属尚未验证。
- 不应把所有 `floorPosition` 简化为 `beat × 60 / bPM`。
- 不应在没有掌握锚点、重置、多次 BPM 变化和 BPM/速度组合规则前批量重算或覆盖原值。
- `cameraMove`、横向位置关键点和默认 BPM 关键点中的 `floorPosition` 全为 0，可能只是共享数据结构中的未使用字段。

## 可信程度

- 速度为 1 时的拍数换秒：`confirmed`
- 恒定 BPM 下的速度加权公式：`confirmed`
- Hold 中间参数是用于结束位置计算的 `canvasIndex`：`confirmed`
- 单次变 BPM、恒定画布速度时使用同类分段积分：`confirmed`
- 多次或与画布速度同刻变化时的组合规则：`unknown`
- 所有锚点、重置和异常值规则：`unknown`
