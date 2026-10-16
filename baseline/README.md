# 文件 Baseline

## 范围

`SHA256SUMS.txt` 记录 `RhythmEditor_original/` 中全部 58 个文件的 SHA-256，路径相对于程序目录根部。

- 生成日期：2026-08-27
- 文件数量：58
- 文件总大小：236,267,847 字节
- 哈希算法：SHA-256
- 文本编码：UTF-8
- 来源：`../RhythmEditor_original/`

清单生成后同时对 `RhythmEditor_original/` 和 `RhythmEditor_working/` 进行了逐项验证。

## 含义

清单用于回答：

- original 是否仍保持原样；
- working 中哪些原程序文件发生了变化；
- 修改失败时应从 original 恢复哪些文件；
- 当前补丁针对的是不是同一个精确程序版本。

文件 baseline 与运行 baseline 不同。文件完整性由本目录的 SHA-256 清单确认；启动、打开谱面、播放、另存和退出流程已于 2026-08-28 建立运行基线，详见 `RUNTIME_BASELINE.md`。

## 验证原则

- 必须按相对路径匹配文件，不能只比较同名文件。
- 在 Windows PowerShell 5.1 中读取清单时显式使用 `-Encoding UTF8`，否则日文路径可能被错误解码。
- 文件缺失、额外替换或任一 SHA-256 不同都视为不匹配。
- original 不用于运行或修复测试；需要恢复时，从 original 复制到 working。
- 修改后的程序必须保留对应的补丁、脚本或变更说明，不能只记录新哈希。
