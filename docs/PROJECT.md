# 项目说明

## 项目名称

Rizline Editor 个人长期改造项目。

## 目标

- 尽量沿用现有编辑器 UI 和已有功能。
- 逐项改善不便的交互并增加个人需要的功能。
- 尽量保持原始 JSON 谱面格式兼容。
- 建立可理解、可测试、可回滚的长期维护流程。
- 不以大规模重写或理论上的最佳架构为目标。

## 使用者与开发环境

- 操作系统：Windows
- 主要环境：VS Code + Codex
- 已掌握：C++、Python、算法与信息学竞赛基础
- 需要补充：实际软件项目结构、构建、依赖管理和 Git 工作流经验

当需要使用者手动操作时，说明：为什么、在哪里、执行什么、预期结果，以及出错时应提供什么信息。

## 工作区结构

```text
RZEMOD/
├─ RhythmEditor_original/   原始只读基准（不修改）
├─ RhythmEditor_working/    文档、插件源码、研究与日常开发安装
└─ RhythmEditor_modded/     干净安装副本，用于正式包验收与日常游玩
```

`RhythmEditor_original` 与初始 `working` 在阶段 1 时均为 58 个原程序文件、236,267,847 字节且哈希一致。之后仅在 `working` / `modded` 的 BepInEx 插件目录中增加可卸载的改良层。

## 已确认的技术栈

### 主编辑器

- Unity 2022.3.62f2
- Windows x64
- IL2CPP 原生发布
- 原始业务脚本很可能使用 C#，当前已转换进 `GameAssembly.dll`
- Unity UI、TextMeshPro、LeTai.TrueShadow
- Newtonsoft.Json
- NAudio
- Facepunch.Steamworks 和 Steam x64 插件

### 改良层（本仓库维护）

- BepInEx + Harmony（IL2CPP）
- `src/RhythmEditorMod` + `src/RhythmEditorMod.Core`（net6.0）
- 使用说明见 `docs/USER_GUIDE.md`，状态见 `docs/STATUS.md`

### 独立手册

- Tauri 2.9.3
- WebView2
- VitePress 静态文档
- 与 Unity 编辑器主体分离

## 当前文件性质

原编辑器本体是 Unity IL2CPP 发布包，不是完整 Unity 工程。原程序树中没有：

- Unity 源工程目录 `Assets/`、`Packages/`、`ProjectSettings/`
- 可直接反编译修改的托管 `Assembly-CSharp.dll`

因此不能直接重新构建出编辑器本体。改良功能以 BepInEx 插件形式维护在 `RhythmEditor_working/src/`，不改写原始 EXE / `GameAssembly.dll` / Unity 资源。

## 重要技术限制

- `GameAssembly.dll` 是原生 IL2CPP 代码，普通 .NET 反编译流程不适用。
- `Rizline Editor_Data/il2cpp_data/Metadata/global-metadata.dat` 使用自定义封装：AES-256-GCM 加密后再打乱 31 个元数据区段的头部映射。阶段 5 已建立可重放脚本，从匹配版本的 `GameAssembly.dll` 动态提取密钥表并重建标准 IL2CPP v31 元数据；这恢复了类型、字段、方法签名和原源码路径字符串，但不等于恢复 C# 方法体或 Unity 源工程。
- 阶段 5 已进一步恢复 `Assembly-CSharp.dll` 的方法指针表，把文件读写、撤销重做、线选择/复制粘贴、音符输入、快捷键和播放控制映射到 `GameAssembly.dll` 的具体 RVA。小范围、版本绑定的原生补丁或注入因此具有技术可行性，但仍不能像有源码项目一样重新构建。
- 现阶段适合调查、资源提取、运行时分析、可重放补丁和外部辅助工具；不适合直接进行大规模功能修改。

## 长期技术路线

优先路线：

1. 保留固定、可校验的原始版本。
2. 建立可运行 baseline。
3. 提取场景、资源、类型和方法信息。
4. 验证能否进行最小运行时注入或可重放补丁。
5. 每次实现一个小功能并记录验证方法。

如果无法恢复足够的 IL2CPP 信息，则按顺序考虑：

1. 运行时观察或注入；
2. 资源补丁；
3. 外部谱面辅助工具；
4. 最后才评估独立重写兼容编辑器。

## 兼容性与分发原则

- 保持与原游戏和原编辑器的 JSON 互操作能力。
- 未确认字段不得删除、改名或改变序列化方式。
- 当前只做个人本地修改。
- 不公开分发原软件二进制、资源、音频或嵌入式手册。
