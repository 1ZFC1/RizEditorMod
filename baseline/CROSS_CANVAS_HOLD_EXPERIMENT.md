# 跨画布 Hold 选择规则实验

日期：2026-08-29（Asia/Shanghai）

## 目标

确认 Hold 的 `otherInformations[1]` 在所属线跨越多个画布时，具体选择哪一个 `canvasIndex`，以及移动结束点跨过画布边界时编辑器是否自动更新它。

## 全语料静态检查

工具：`tools/Analyze-HoldCanvas.ps1`

命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Analyze-HoldCanvas.ps1 `
  -ChartRoot .\#Samples
```

对 18 个 JSON 中全部 512 个 Hold 检查：

| 候选规则 | 匹配数 |
|---|---:|
| Hold 起点处或之前最后一个线节点的画布 | 490/512 |
| Hold 结束拍之前最后一个线节点的画布，严格 `< endTime` | 512/512 |
| Hold 结束拍处或之前最后一个线节点的画布，`<= endTime` | 486/512 |

147 个 Hold 恰好结束在线节点时：

- 严格选择结束拍之前的最后一个节点：147/147 匹配；
- 把结束拍处的节点也包含在内：121/147 匹配。

因此现有语料支持的准确规则是：

```text
otherInformations[1]
  = canvasIndex of the last linePoint whose time < endTime
```

也就是选择 Hold 结束点所在区间的前一个线节点；当 Hold 恰好结束在线节点时，仍使用到达该节点之前那一段对应的画布。

## 隔离运行实验

实验只使用 `baseline/runtime-artifacts/2026-08-29-cross-canvas-hold-01/`，没有修改 `#Samples/` 或编辑器发布文件。

来源：`#Samples/Gleam feat. ふわまろ/Chart_EZ.json`

- 来源和 before 长度：422867 字节
- 来源和 before SHA-256：`9EEE115E4DEE00E78028A25BF3B1E0E2DB8C5C446C31978DA38962F020B37F19`
- 目标：`lines[33].notes[0]`
- Hold 开始拍：100
- 原结束拍：103.5
- 原 `canvasIndex`：1
- 原 `endFloorPosition`：34.9230766

相关线节点：

| `time` | `canvasIndex` |
|---:|---:|
| 100 | 0 |
| 101.75 | 1 |
| 103.5 | 0 |
| 105.25 | 4 |

用户只把 Hold 结束拍从 103.5 延长到 104.5，按 `Ctrl+S` 保存并正常退出。

保存后：

- 长度：422867 字节
- SHA-256：`6243E2BAEBEFDD412E85DE55000077B24AD776CFC267707FF576B24FBF8D3B14`

完整递归比较只有三处变化：

```text
lines[33].notes[0].otherInformations[0]: 103.5 -> 104.5
lines[33].notes[0].otherInformations[1]: 1.0 -> 0.0
lines[33].notes[0].otherInformations[2]: 34.9230766 -> 32.1538467
```

新结束拍 104.5 之前最后一个线节点是 103.5，其 `canvasIndex=0`，与保存结果一致。使用画布 0 计算的结束位置为 `32.1538461538462`，与实际值的绝对误差约 `5.46 × 10^-7`。

## 结论

- `otherInformations[1]` 的跨画布选择规则为结束拍之前最后一个 `linePoint` 的 `canvasIndex`：`confirmed`。
- 判断使用严格 `< endTime`，不是 `<= endTime`：由 147/147 个边界样例支持，状态为 `confirmed`。
- 移动 Hold 结束点跨过线节点边界时，编辑器会自动更新 `[1]` 和 `[2]`：`confirmed`。
- 线节点时间倒序、多个线节点同刻或 Hold 结束早于首个线节点时的行为仍是 `unknown`。
