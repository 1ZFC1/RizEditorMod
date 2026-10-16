# BPM 变化点实验

操作时间：2026-08-28（Asia/Shanghai）

分析记录：2026-08-29（Asia/Shanghai）

## 目标

验证：

- `bpmShifts[].time` 是否记录 BPM 变化开始拍；
- `bpmShifts[].value` 保存界面 BPM 还是相对基础 BPM 的倍数；
- `bpmShifts[].floorPosition` 的含义；
- BPM 变化后其他 `floorPosition` 是否按分段 BPM 自动重算。

实验只使用 `baseline/runtime-artifacts/2026-08-28-bpm-shift-01/` 中的隔离副本，没有修改 `#Samples/` 或编辑器发布文件。

## 测试对象和操作

来源为 `#Samples/Sample.json`：

- 基础 `bPM`：105
- 初始 `bpmShifts`：只有拍 0、`value=1.0` 的默认项
- 画布速度：全程为 1
- 输入文件长度：9947 字节
- 输入文件 SHA-256：`1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`

用户保持基础 BPM 为 105，通过 BPM 变化功能在第 8 拍新增 210 BPM，按 `Ctrl+S` 保存并正常退出。

## 保存结果

保存文件：

- 长度：10072 字节
- SHA-256：`A64683203D872E47E95453EFF85C213CF44D179C3D7EBA4D7C8E61CEE2D4FA0D`
- 基础 `bPM`：仍为 105
- `bpmShifts`：由 1 项增加为 2 项

新增项为：

```json
{
  "time": 8.000001,
  "value": 2.0,
  "easeType": 0,
  "floorPosition": 4.57142925
}
```

界面输入的第 8 拍被保存为 `8.000001`，应视为浮点或界面定位精度现象；目前不能据此断言编辑器的时间量化规则。

## 结构差异

完整递归比较共发现 31 处差异：

- 新增 1 个 `bpmShifts` 项；
- 第 8 拍之后的 12 个 `linePoints[].floorPosition` 被重算；
- 第 8 拍之后的 17 个 `notes[].floorPosition` 被重算；
- 唯一 Hold 的 `otherInformations[2]` 被重算；
- 其他字段没有变化。

变化点实际存储在 `8.000001`，因此正好位于第 8 拍的节点保持原值，从第 9 拍起的现有节点发生变化。

部分例子：

```text
第 9 拍：  5.142857   -> 4.8571434
第 14 拍： 8.0        -> 6.28571463
第 24 拍： 13.7142859 -> 9.142858
Hold 结束点第 28 拍：16.0 -> 10.2857151
```

## 公式验证

210 / 105 = 2，因此 JSON 中的 `value=2.0` 确认是相对基础 `bPM` 的倍数：

```text
effectiveBPM = bPM × bpmShift.value
```

变化点自身位置：

```text
8.000001 × 60 / 105 = 4.57142914285714
实际 floorPosition = 4.57142925
绝对误差约 1.07 × 10^-7
```

变化点之后、速度为 1 时：

```text
floorPosition(t)
  = shift.floorPosition
  + (t - shift.time) × 60 / (bPM × shift.value)
```

被重算的 30 个位置字段全部在 `10^-6` 容差内符合该公式，最大绝对误差约 `8.93 × 10^-7`。

## 结论

- `bpmShifts` 是 BPM 变化序列：`confirmed`。
- `time` 是变化开始拍数：`confirmed`；精确存储和量化规则仍 `unknown`。
- `value` 是相对基础 `bPM` 的倍数，界面显示 `bPM × value`：`confirmed`。
- `easeType` 在本次 UI 新增项中保存为 0：结构和值 `confirmed`；是否始终被忽略以及准确边界行为仍未验证。
- `floorPosition` 是变化点对应的累计位置：`confirmed`。
- 单次 BPM 突变、画布速度为 1 时，后续位置按有效 BPM 分段积分并自动重算：`confirmed`。
- 多次 BPM 变化、BPM 与画布速度在同一时刻变化、精确边界归属仍是 `unknown`。
