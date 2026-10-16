# 未知 JSON 字段保存实验

日期：2026-08-28（Asia/Shanghai）

## 目标

验证编辑器能否读取带有额外未知字段的谱面，以及发生真实编辑并保存后是否会保留该字段。

本实验只在 `baseline/runtime-artifacts/2026-08-28-unknown-field-01/` 中使用隔离副本，没有修改 `#Samples/` 或编辑器发布文件。

## 单一测试变量

来源文件为 `#Samples/Sample.json`。在 JSON 顶层、`fileVersion` 后只增加一个字符串字段：

```json
"codexCompatibilityProbe": "preserve-this-exact-value-2026-08-28"
```

准备完成时，`before` 与 `test-chart` 文件完全相同：

- 长度：10016 字节
- SHA-256：`802638F785D7C2C06EF67C1946958F98DE948BFD9D9E96AF5B229BBA213BF923`
- 两者均可由 PowerShell `ConvertFrom-Json` 正常解析。

## 操作

用户在编辑器中打开隔离测试谱面，将唯一 Hold 的结束拍从 28 改为 26，按 `Ctrl+S` 保存，然后正常退出。

这项 Hold 编辑已经在 `baseline/HOLD_PARAMETER_EXPERIMENT.md` 中独立验证过，用于确保编辑器执行真实的反序列化、数据修改和重新保存，而不是只复制未编辑文件。

## 保存结果

保存后的测试文件：

- 长度：9953 字节
- SHA-256：`6399BC166B97C28D6D8A57357DB31F7D726A21C24FBE92CF7467EFA363CCBBFC`
- 顶层 `codexCompatibilityProbe`：不存在

完整 JSON 递归比较只有三处差异：

```text
codexCompatibilityProbe: "preserve-this-exact-value-2026-08-28" -> <missing>
lines[3].notes[8].otherInformations[0]: 28.0 -> 26.0
lines[3].notes[8].otherInformations[2]: 16.0 -> 14.8571424
```

保存结果与先前不含未知字段、执行相同 Hold 编辑的控制文件字节完全相同，SHA-256 也同为 `6399BC166B97C28D6D8A57357DB31F7D726A21C24FBE92CF7467EFA363CCBBFC`。

本次生成的两个自动保存 JSON 中也都不存在该探针字段。

## 结论

- 编辑器能够打开并编辑带有额外未知顶层字符串字段的谱面：`confirmed`。
- 一旦发生真实编辑并保存，额外未知顶层字段会被删除：`confirmed`。
- 本次自动保存也不会保留该未知顶层字段：`confirmed`。
- 未知嵌套字段、未知对象或数组、缺失标准字段的行为仍是 `unknown`。

## 兼容性影响

目前不能把长期扩展数据直接作为未知顶层字段写进谱面，并期望原编辑器在后续保存时保留。未来若需要附加自定义元数据，应优先考虑独立 sidecar 文件，或由可验证的保存包装层在编辑器保存后安全合并；在实现和测试之前不应修改正式谱面。
