# `challengeTimes[].checkPoint` 实验

日期：2026-08-29（Asia/Shanghai）

## 目标

调查 `challengeTimes[].checkPoint` 在编辑器预览、普通保存、自动保存和 Riztime 区间编辑中的行为。业务含义未知，因此实验只记录可观察事实，不根据字段名猜测。

## 静态背景

工具：`tools/Analyze-ChallengeTimes.ps1`

命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Analyze-ChallengeTimes.ps1 `
  -ChartRoot .\#Samples
```

18 个 JSON 中共有 22 个 `challengeTimes` 对象：

- `checkPoint` 观察范围：0–263；
- `checkPoint == start`：3/22；
- `checkPoint < start`：16/22；
- `checkPoint > start`：3/22；
- `checkPoint` 位于 `[start, end]`：3/22。

因此 `checkPoint` 不是 `start` 的别名，也不要求位于 Riztime 区间内。

Unity 场景中能找到 `Riztime Period (Beats)` / `Riztime 时间段（节拍）`，但没有找到 `checkPoint`、`Check Point` 或对应中文标签。两份公开的非官方格式说明也将其作用标为未知：

- `https://rgwiki.stary.pc.pl/wiki/Rizline%3AChart`
- `https://docs.lchzh.top/learning/rizline/`

这些外部页面不是本项目的确认依据，只用于说明目前没有可直接采用的公开解释。

## 第一轮：注入非零值并观察预览

来源为 `#Samples/Sample.json`，原始 Riztime 对象为：

```json
{
  "checkPoint": 0.0,
  "start": 20.0,
  "end": 24.0,
  "transTime": 0.5
}
```

在 `baseline/runtime-artifacts/2026-08-29-checkpoint-01/` 的隔离 before 和 test 中只把 `checkPoint` 改为 12：

- 输入长度：9948 字节；
- 输入 SHA-256：`FE62AD28CD3E7CCFA9038B7DD7494D054C7FBA33038A3AF9AD3F855FDE0B37ED`。

用户从第 10 拍之前播放至第 25 拍以后，观察结果：

- 第 12 拍：无任何明显变化；
- 第 20 拍：Riztime 正常开始；
- 第 24 拍：Riztime 正常结束。

随后用户把已验证的唯一 Hold 结束拍从 28 改为 26，以触发真实保存。保存后：

- 文件长度：9954 字节；
- SHA-256：`7BAE08CA0D659739187BC7D79198AFC7CFA786F0B68525E67951BFDA17B071D7`；
- `checkPoint` 仍为 12；
- 普通保存和两个自动保存 JSON 都保留 12。

递归比较只有已知的两个 Hold 变化：

```text
lines[3].notes[8].otherInformations[0]: 28.0 -> 26.0
lines[3].notes[8].otherInformations[2]: 16.0 -> 14.8571424
```

## 第二轮：修改 Riztime 区间

在 `baseline/runtime-artifacts/2026-08-29-checkpoint-02/` 中再次使用 `checkPoint=12`、`start=20`、`end=24` 的相同输入。

用户通过编辑器把 Riztime 区间改为 10–14，播放确认第 10 拍正常开始、第 14 拍正常结束，然后按 `Ctrl+S` 保存并退出。

保存后：

- 文件长度：9948 字节；
- SHA-256：`44845776DCC4CCEF58556742B448336D17AD370827276BC53447054B2B92CDA2`；
- `checkPoint` 仍为 12；
- 普通保存和两个自动保存 JSON 都保留 12。

递归比较只有：

```text
challengeTimes[0].start: 20.0 -> 10.0
challengeTimes[0].end: 24.0 -> 14.0
```

## 结论

- `checkPoint` 是编辑器已知并会原样保存的 `number` 字段，不会像额外未知顶层字段那样被删除：`confirmed`。
- 在本次编辑器预览中，`checkPoint=12` 没有可见效果；Riztime 仍由 `start` 和 `end` 控制：测试范围内 `confirmed`。
- 修改无关 Hold 或直接修改 Riztime 区间时，编辑器都不重算 `checkPoint`：`confirmed`。
- `start`、`end` 是以拍为单位的 Riztime 开始和结束时刻：`confirmed`。
- `checkPoint` 在游戏运行时、其他编辑器版本或其他场景中的真实用途仍为 `unknown`。

## 兼容性要求

尽管语义未知，转换、保存和补丁工具都必须原样保留 `checkPoint`。不能将它删除、归零、改成 `start`，也不能因为当前编辑器预览无可见效果就断言它在游戏中无效。
