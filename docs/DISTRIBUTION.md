# RhythmEditorMod 导出、安装与更新

最后更新：2026-10-16

## 目标

把已验收的插件功能导出为不包含原编辑器本体的独立安装包。使用者只需把固定名称的 `RhythmEditorMod/` 文件夹放到编辑器根目录，与 `Rizline Editor.exe` 并列，然后双击 `RhythmEditorMod.Manager.exe`。

当前安装包只用于个人本地使用。它包含从闭源编辑器生成的 IL2CPP interop 和重建 metadata，不应公开分发。

## 三个编辑器副本

```text
RhythmEditor_original/   永久只读基线
RhythmEditor_working/    源码、构建和研究环境
RhythmEditor_modded/     由正式安装包部署，日常使用及最终验收
```

任何导出或安装测试都不得修改 `RhythmEditor_original/`。创建 modded 后应先逐文件比较 original 的 58 个文件和 SHA-256，再安装插件。

## 安装包结构

ZIP 文件名带版本：

```text
RhythmEditorMod-0.3.0-win-x64.zip
```

ZIP 内层目录名保持固定：

```text
RhythmEditorMod/
├─ RhythmEditorMod.Manager.exe
├─ README.md
├─ manifest.json
├─ manager-runtime/
└─ payload/
```

- `RhythmEditorMod.Manager.exe`：使用者唯一需要运行的入口。它是小型原生 Windows x64 启动器。
- `manager-runtime/`：管理器私有 .NET 8 运行环境和 C# 管理逻辑；目标电脑无需预装 .NET。
- `payload/`：BepInEx IL2CPP、预生成 interop、标准 metadata、插件 DLL 和启动文件。
- `manifest.json`：包版本、支持的 `GameAssembly.dll` 哈希以及全部 payload 文件的 SHA-256 和角色。

管理器从自身目录的上一级自动寻找 `Rizline Editor.exe`，通常不需要选择路径。命令行调试时可使用 `--target` 指定目标。

## 为什么完整包必须包含 interop 和 metadata

当前编辑器的原始 `global-metadata.dat` 使用自定义保护，普通 BepInEx 无法直接生成可用 interop。因此只复制两个插件 DLL 不能在干净编辑器中工作。

安装包使用已经验证的：

- BepInEx `6.0.0-be.785`；
- `BepInEx/interop`；
- `BepInEx/metadata/global-metadata.standard.dat`，SHA-256 为 `C358B6FA326B4B91117364B23464CF396BD5559A017B23D701B66B179FFC1FAC`（重建与验证过程见 `research/STAGE5_IL2CPP_METADATA.md`）；
- BepInEx 使用的私有 .NET 6 运行文件；
- `RhythmEditorMod.dll` 和 `RhythmEditorMod.Core.dll`。

导出配置将 `UpdateInteropAssemblies` 设为 `false`，避免在目标电脑上使用受保护的原 metadata 重新生成或访问网络。插件本身及安装器都会检查当前 `GameAssembly.dll` 的精确 SHA-256。

## 使用者操作

### 首次安装

1. 退出编辑器。
2. 把完整 `RhythmEditorMod/` 文件夹放入编辑器根目录。
3. 双击 `RhythmEditorMod.Manager.exe`。
4. 选择“安装或更新”。
5. 启动编辑器并验证功能。

### 更新

1. 退出编辑器和旧管理器。
2. 用新版 ZIP 中的 `RhythmEditorMod/` 替换旧安装包文件夹。
3. 双击新版管理器并选择“安装或更新”。

普通功能更新只会更换清单中发生变化的插件文件。内容相同的运行文件直接跳过；只有安装包明确携带新运行环境时才会更新对应文件。

### 禁用和卸载

- “启用/禁用”：在 `RhythmEditorMod.dll` 与 `.dll.disabled` 之间安全切换。
- “卸载插件”：移除插件 DLL，但保留 BepInEx，方便以后重新安装。
- “完整移除”：只删除管理器创建、哈希未变化的文件；检测到其他插件时拒绝完整移除，检测到已修改文件时保留它。

所有写操作都会先确认编辑器进程已经退出。

## 安装记录和备份

目标编辑器中使用：

```text
.rhythm-editor-mod/
├─ install.json
└─ backups/
```

`install.json` 记录安装版本、BepInEx 版本、目标哈希、安装时间、每个文件的 SHA-256、角色及是否由管理器创建。更新覆盖旧文件前写入按时间命名的备份目录；操作中断时尝试回滚本轮已经写入的文件。

不要手工编辑 `install.json`。

## 开发者导出

导出环境需要 .NET 8 SDK、可调用的 `g++`（用于编译原生启动器），以及已经验证可用的 working BepInEx 环境。

在 `RhythmEditor_working/` 根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-RhythmEditorModPackage.ps1 -Version 0.4.0
```

脚本依次：

1. 运行插件的全部纯数据测试并编译插件。
2. 编译 C# 管理逻辑和原生 Windows 启动器。
3. 从已验证的 working 运行环境构建 payload。
4. 把 BepInEx 配置转换为不自动重建 interop 的发布配置。
5. 为  payload 逐文件生成 SHA-256 清单。
6. 生成目录包、ZIP 和 ZIP 的 `.sha256` 文件。

同版本输出已存在时脚本会停止。确认只覆盖生成物时可显式使用 `-Force`；通常发布更新应增加版本号，而不是覆盖旧版本。

当前生成位置：

```text
artifacts/releases/RhythmEditorMod-0.4.0-win-x64/
artifacts/releases/RhythmEditorMod-0.4.0-win-x64.zip
artifacts/releases/RhythmEditorMod-0.4.0-win-x64.zip.sha256
```

当前 0.4.0 ZIP SHA-256：

```text
5E460EE6D164FE2D9F14B2BC61704C21DC78B2DE55B5DC869023B84675015C56
```

`0.2.0` / `0.3.0` 的目录包与 ZIP 保留在同一目录，不覆盖旧版本。

## 当前自动验证结果

### 0.4.0（2026-10-16）

- 插件纯数据测试：**51/51** 通过。
- 插件、管理器（.NET 8 SDK 8.0.302）编译与原生启动器（`g++` 14.2.0）编译：0 警告、0 错误。
- 安装包 payload：**387** 个文件、139,509,396 字节；目录包 217,275,891 字节。
- ZIP 实测 SHA-256 与 `.sha256` 一致；包内 `RhythmEditorMod.dll` / `RhythmEditorMod.Core.dll` 与构建产物逐字节一致，且 DLL 内含版本串 `0.4.0`。
- **干净副本验证**（由 `original` 新复制，复制后 58/58 与 baseline 一致）：安装前状态（包 0.4.0 / 未安装 / GameAssembly 受支持）→ **首次安装** → **同版本重复安装**（幂等）→ **禁用 → 启用** → **仅卸载插件 → 重装** → **完整移除 → 重装**，全部成功；完整移除后与重装后该副本的 58 个编辑器文件仍 **58/58** 与 baseline 一致。
- **更新 `RhythmEditor_modded`**：`install.json` 由 `0.3.0` 升到 `0.4.0`（387 条目，`InstalledAt=2026-10-16T00:40:12+08:00`）；更新备份写入 `.rhythm-editor-mod/backups/20261016-004012`（含 2 个被替换的插件 DLL）；安装后两个插件 DLL 与构建产物逐字节一致；58 个编辑器文件仍 **58/58** 一致；管理器提示「保留用户修改的配置：`BepInEx/config/BepInEx.cfg`」。
- 验证用的临时干净副本（`RhythmEditor_verify`）与替换前的包文件夹（`RhythmEditorMod.old`）已在验证完成后删除，工作区恢复为 `original` / `working` / `modded` / `tools` 四项。
- 本次 0.4.0 包含的改动：**跨画布线的复制与粘贴**（D-030）、**整体缩放 40%–200%、每格 10 个百分点**（D-031）、**项目工具状态行独立 + 日志规范化**（D-032）。

### 0.3.0（2026-09-22）

- 插件纯数据测试：**25/25** 通过。
- 插件、管理器编译与原生启动器（`g++`）编译：0 警告、0 错误。
- 安装包 payload：387 个文件，逐文件哈希由管理器在安装前验证。
- ZIP 实测 SHA-256 与 `.sha256` 记录一致；包内 `RhythmEditorMod.dll` / `RhythmEditorMod.Core.dll` 与构建产物逐字节一致，且 DLL 内只含版本串 `0.3.0`。
- **干净副本验证**（由 `original` 新复制，复制后 58/58 与 baseline 一致）依次通过：只读状态检测、首次安装、同版本重复安装/更新、禁用与重新启用、仅卸载插件后重装、完整移除、完整移除后重装。
- 完整移除后、以及重装插件后，该副本的 58 个编辑器文件仍 **58/58** 与 baseline 一致。
- **更新 `RhythmEditor_modded`**：`install.json` 由 `0.2.0` 升到 `0.3.0`；插件 DLL 与包内逐字节一致；更新备份写入 `.rhythm-editor-mod/backups/20260922-200217`；58 个编辑器文件仍 58/58 一致。
- 验证用的临时干净副本已在验证完成后删除，工作区恢复为 `original` / `working` / `modded` / `tools` 四项。

### 0.2.0（2026-08-31，历史）

- original → modded：58/58 个文件 SHA-256 一致。
- 插件纯数据测试：12/12 通过。
- 0.2.0 ZIP SHA-256：`7DDBEFE93FCFD926DB6BEB8EF276A9780157DC041D99919DD273C2D01C30E19F`。
- 使用者在 modded 上确认：管理器双击和状态正常；编辑器启动、流速缓动、流速剪贴板、画布事件剪贴板、Undo/Redo 及保存重开全部正常。

## 使用者验收（0.4.0，已完成）

自动化流程不覆盖、需在 `RhythmEditor_modded` 上人工确认的部分：

- 管理器：从 `RhythmEditor_modded/RhythmEditorMod/` 双击 `RhythmEditorMod.Manager.exe`，窗口与状态显示正常（应显示 0.4.0 / 已启用）。
- 编辑器启动正常；下列本轮改动逐项确认：
  - **跨画布线的复制与粘贴**：线编辑里 `Ctrl+C` 后**按住 `Shift`** 拖框 → 拖动中整条线实时高亮、松手结束；`Ctrl+V` 按鼠标对齐并整体偏移节点画布，`Ctrl+Shift+V` 以鼠标为对称轴镜像；不按 `Shift` 时与原版一致。
  - **整体缩放**：`Ctrl+Shift+滚轮` 每格 10 个百分点，能在 40%–200% 共 17 档之间升降（现在可放大），`Ctrl+M` 复位，关掉「突破缩放极限」同样复位。
  - **项目工具**：状态行只显示项目自己的消息（在画布流速里设缓动不再改写它）；日志不再每帧刷屏。

结论：2026-10-16，使用者确认 0.4.0 可用（「ok」）；管理器与编辑器在 `RhythmEditor_modded` 上运行正常，无遗留问题。

## 使用者验收（0.3.0，已完成）

自动化流程不覆盖、由使用者在 `RhythmEditor_modded` 上人工确认的部分：

- 管理器：从 `RhythmEditor_modded/RhythmEditorMod/` 双击 `RhythmEditorMod.Manager.exe`，窗口与状态显示正常。
- 编辑器：`RhythmEditor_modded` 启动正常；光标「基础设置」面板、颜色编辑剪贴板的左右路由与同拍覆盖、发布统计、`Ctrl+D`、调色板、横向缩放（40%–100%）、预览细线均正常。

结论：0.3.0 已可用，无遗留问题。

验收时使用者在 modded 上又做了一次安装/更新（`InstalledAt` 刷新为 `2026-09-22T20:06:06+08:00`）。由于文件内容与已安装版本一致，管理器没有写入任何文件、也没有产生新备份（备份目录仍只有更新时的 `20260922-200217`，含 2 个被替换的插件 DLL），这同时确认了重复更新的幂等路径。

## 后续版本维护规则

1. 每个功能先在 working 中实现、测试并形成稳定 checkpoint。
2. 修改 `Plugin.Version`，导出脚本使用相同版本号。
3. 重新执行完整导出，不手工修改 payload 或 manifest。
4. 新安装包先在由 original 新复制的干净测试副本中验证管理器流程。
5. 再更新日常使用的 modded。
6. 记录 ZIP 哈希、管理器测试和运行验收结果。
7. 编辑器本体升级时，必须重新调查哈希、metadata、interop 和 Harmony 入口；不得只修改清单哈希强行兼容。
