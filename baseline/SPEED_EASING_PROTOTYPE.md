# 画布流速缓动离散采样原型

日期：2026-08-30

状态：`PASS`。自动检查、编辑器内打开、播放、保存和退出均已通过。

## 目标

在不改变谱面 JSON 格式的前提下，把一段连续缓动目标曲线离散为原游戏能够读取的一系列普通突变流速节点。第一轮先验证算法和运行兼容性，不修改 `Rizline Editor.exe`、`GameAssembly.dll` 或 Unity 资源。

## 实现

工具：`tools/Generate-SpeedEase.py`

当前提供四种由本工具定义的采样曲线：

- `linear`
- `in-quad`
- `out-quad`
- `in-out-quad`

工具根据区间起止拍、起止速度和采样段数生成 `steps + 1` 个流速节点。每个生成节点仍是原格式的普通 `KeyPoint`：

```json
{
  "time": 8.25,
  "value": 0.50046875,
  "easeType": 0,
  "floorPosition": 4.642857143
}
```

原游戏仍只会看到一系列突变速度；“缓动”是这些小间隔阶跃的整体近似效果，不会向 JSON 增加自定义字段。

## 安全边界

原型当前只接受已经确认能够可靠重算 `floorPosition` 的保守子集：

- 恰好一个画布；
- `bpmShifts` 只有 `time=0, value=1` 的恒定 BPM 项；
- 流速节点已严格按时间递增且没有负拍节点；
- 目标区间内没有原有流速节点；
- 所有线点和 Hold 结束点都引用该唯一画布。

不满足条件时工具会停止并报告错误，不会自行排序事件，也不会输出部分结果。输入和输出路径必须不同；已有输出文件不会被覆盖。

这些限制不是格式的永久限制，而是为了避免在多画布、多 BPM 和锚点/重置规则尚未完全确认时写出错误位置。

## 本轮测试谱面

输入：`#Samples/Sample.json`

输入 SHA-256（生成前后相同）：

```text
1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F
```

隔离输出（由 `.gitignore` 排除）：

```text
baseline/runtime-artifacts/2026-08-30-speed-ease-01/
  Speed Easing Test.json
  Speed Easing Test.ogg
```

输出 JSON SHA-256：

```text
4BBF5757AD49BA5586A3728500C50FB2B05CE23830DE57558DF532F73D421B22
```

参数：

```text
canvas=0
startBeat=8
endBeat=28
startSpeed=0.5
endSpeed=2.0
ease=in-out-quad
steps=80
```

因此 8–28 拍每 0.25 拍设置一次新速度；连同原有的第 0 拍节点，共有 82 个流速节点。

复现命令（输出不存在时）：

```powershell
python .\tools\Generate-SpeedEase.py `
  '.\#Samples\Sample.json' `
  '.\baseline\runtime-artifacts\2026-08-30-speed-ease-01\Speed Easing Test.json' `
  --canvas 0 `
  --start-beat 8 `
  --end-beat 28 `
  --start-speed 0.5 `
  --end-speed 2 `
  --ease in-out-quad `
  --steps 80
```

## 自动检查

运行：

```powershell
python -m unittest discover -s .\tests -v
```

结果：4/4 通过。

生成谱面的位置积分复查：

- 流速节点：82/82 匹配；
- 线点：13/13 匹配；
- Hold 结束位置：1/1 匹配；
- 最大已观察误差约 `4.29 × 10^-10`。

## 手动运行验证

使用者于 2026-08-30 在 working 副本中完成验证：

- JSON 和 OGG 能够正常打开；
- 8–28 拍呈现逐渐加速；
- 阶梯感不明显，整体比较平滑；
- 线、音符和 Hold 均正常；
- 没有卡顿、报错或崩溃；
- 已执行 `Ctrl+S` 保存并正常退出。

主测试 JSON 保存后的 SHA-256 仍为：

```text
4BBF5757AD49BA5586A3728500C50FB2B05CE23830DE57558DF532F73D421B22
```

这表示没有编辑操作时，`Ctrl+S` 没有重写主文件；82 个节点、8/18/28 拍的 `0.5/1.25/2.0` 速度和值的严格递增时间顺序均保留。

编辑器另生成了一个自动保存 JSON：

- 仍有 82 个流速节点；
- 与主文件递归比较没有结构、数组长度、数组顺序或非数值差异；
- 141 个数值发生单精度舍入，最大绝对差约 `3.443 × 10^-6`；
- 在 `10^-5` 容差下，164/164 个流速节点位置、26/26 个线点位置和 2/2 个 Hold 结束位置全部通过积分复查。

结论：在本轮单画布、恒定 BPM、每 0.25 拍采样的条件下，离散流速缓动方案有效，状态为 `confirmed`。现在可以开始设计编辑器内的选择方式、缓动类型控件、采样密度控件以及可回滚的 IL2CPP 注入/补丁方案；多画布和 BPM 变化仍需分别处理。
