# 可运行 Baseline

日期：2026-08-28（Asia/Shanghai）

## 结论

状态：`PASS`

未经功能修改的 `RhythmEditor_working` 已完成一次隔离的人工运行测试：能够启动、打开样例、播放和暂停、另存为新文件并正常退出。运行前后未发现原程序工作目录、所监测 AppData 或相关注册表位置发生变化。

本结论只覆盖本文件列出的操作和监测范围，不代表所有编辑、自动保存、异常退出或 Steam 条件均已验证。

## 测试条件

- 程序：`RhythmEditor_working/Rizline Editor.exe`
- 平台：Windows
- Steam：测试期间处于运行状态
- 操作：由使用者在图形界面中执行
- 状态采集和文件校验：由 PowerShell 脚本执行
- 测试副本目录：`baseline/runtime-artifacts/2026-08-27-baseline-01/test-chart/`

运行产物目录已加入 `.gitignore`，不会进入稳定 checkpoint。

## 隔离样例

输入文件：

- `Baseline Sample.json`
- `Baseline Sample.ogg`

另存文件：

- `Baseline Saved.json`

输入 JSON 与另存 JSON：

- 文件长度：均为 9947 字节
- SHA-256：均为 `1B63A8C1EDEC854650240962D4FA8E97E97893689BCDDFE874C6B9E69612CD9F`
- JSON 顶层字段：一致
- JSON 结构：一致
- 字节内容：完全一致

输入 JSON 和 OGG 在测试后也保持不变。测试目录中未发现额外自动保存文件。

## 操作结果

| 项目 | 操作和观察 | 结果 |
|---|---|---|
| 启动 | 手动启动 working 副本；主窗口正常出现 | PASS |
| 打开样例 | 打开隔离的 `Baseline Sample.json`；标题栏显示测试路径 | PASS |
| 播放 | 有声音，时间轴移动 | PASS |
| 暂停 | 播放能够正常暂停 | PASS |
| 另存为 | 使用 `Ctrl+Shift+S` 保存为 `Baseline Saved.json` | PASS |
| 退出 | 正常关闭窗口；随后未发现编辑器进程 | PASS |

本轮同时由使用者确认：

- `Ctrl+S`：保存
- `Ctrl+Shift+S`：另存为

## 运行前后对比

采集时间：

- Before：`2026-08-27T14:02:04.1065659Z`
- After：`2026-08-27T16:51:26.3574162Z`

对比结果：

| 范围 | Before | After | 变化 |
|---|---:|---:|---:|
| 原程序工作目录文件数 | 72 | 72 | 0 |
| 所监测 AppData 文件数 | 15 | 15 | 0 |
| 所监测注册表位置 | 均不存在 | 均不存在 | 无 |
| 编辑器进程 | 未运行 | 未运行 | 无残留 |

工作目录快照有意排除了 `baseline/runtime-artifacts/`，因为其中包含本轮主动创建的隔离样例、快照和另存输出。

AppData 监测范围：

- `%USERPROFILE%\AppData\LocalLow\PigeonGames`
- `%LOCALAPPDATA%\PigeonGames`
- `%APPDATA%\PigeonGames`

其中已有的 `Rizline Editor/Player.log` 和 `Player-prev.log` 在本轮没有更新。

注册表监测范围：

- `HKCU\Software\PigeonGames`
- `HKCU\Software\Unity\UnityEditor\PigeonGames`

两个位置在运行前后均不存在。

原程序完整性复核：`baseline/SHA256SUMS.txt` 的 58 个条目在 original 和 working 中均为 0 个不匹配。

## 自动化工具

- `tools/Capture-RuntimeState.ps1`：采集工作目录、AppData、注册表和相关进程状态。
- `tools/Compare-RuntimeState.ps1`：比较两次采集结果。

这些脚本只用于采集和比较状态，不修改编辑器发布文件。

## 限制与待补测试

- GUI 操作由使用者人工完成；未建立 UI 自动化。
- Steam 在测试期间运行，尚未验证 Steam 未运行时能否启动。
- 只测试了打开与另存，未进行实际谱面语义编辑。
- 未单独测试 `Ctrl+S` 覆盖保存。
- 未测试自动保存、崩溃恢复和异常退出。
- “另存未改变字节”不能证明编辑器在编辑后仍会保留未知 JSON 字段、字段顺序或数值精度。
- AppData 和注册表对比只覆盖上面列出的 PigeonGames 路径，不是整台系统的全量审计。
- 测试期间 Steam 进程曾重新出现不同 PID；现有证据不足以归因于编辑器。

## 判定

阶段 3 的最低可运行 baseline 已建立，可以进入阶段 4 的谱面 JSON 调查。进入功能修改前，仍应继续保留 original 副本、SHA-256 文件基线和本运行基线作为回归参照。
