# RizEditorMod

Rizline Editor 的个人向功能增强插件（IL2CPP / BepInEx 6 + HarmonyX）。
*An unofficial, personal-use feature mod for the Rizline Editor, built with BepInEx 6 and HarmonyX.*

> **本仓库只包含插件自身的源码与文档。** 不包含、也不再分发原编辑器本体、`GameAssembly.dll`、
> `Rizline Editor_Data/`、BepInEx 二进制、由游戏元数据生成的 interop/metadata、音频或任何游戏资源。
> 本项目与游戏官方无关，仅供个人本地研究与使用；请自备正版编辑器副本。
>
> This repository contains **only** the mod's own source code and documentation. No game binaries,
> no BepInEx/interop/metadata artefacts, no audio or other game assets are included or distributed.

---

## 功能一览

当前版本 **0.4.0**。完整逐条记录见 [`docs/CHANGELOG.md`](docs/CHANGELOG.md)，长期设计决定见
[`docs/DECISIONS.md`](docs/DECISIONS.md)。

### 编辑效率

| 功能 | 说明 |
| --- | --- |
| **跨画布线的复制与粘贴** | 一条线的节点可分布在多个画布上，原版既选不中也粘不对。线编辑里 `Ctrl+C` 后**按住 `Shift`** 拖框即可整条线选中（拖动中实时高亮），`Ctrl+V` 按鼠标对齐并**整体偏移节点画布**，`Ctrl+Shift+V` 为以鼠标为对称轴的镜像粘贴；一次 Undo 可撤销。 |
| **流速 / 画布事件 / 颜色 / 摄像机事件的剪贴板** | 框选复制剪切、附着吸附的粘贴、系统剪切板导入导出（带类型与字段校验）。 |
| **额外缓动** | 把曲线（含弹性等）拟合为编辑器已有的 Linear 密集节点，画布与摄像机横向移动均可用；非法缓动索引永不写入谱面，并有越界求值安全网。 |
| **调色板** | 从谱面批量收集配色并写入编辑器调色板槽位。 |
| **「项目」工具** | 运行时新建的工具栏工具：记忆工程槽位、在编辑器内切换工程（切换前强制保存、未保存过的新工程拒绝切换）。 |

### 显示与预览

| 功能 | 说明 |
| --- | --- |
| **整体缩放** | 「突破缩放极限」开启后 `Ctrl+Shift+滚轮`：**40%–200%、每格 10 个百分点**（共 17 档，可放大可缩小），`Ctrl+M` 复位；等比缩放（横竖同比例），不会横向拉伸。 |
| **预览蒙版：开 / 关** | 切到「关」后，带预览播放时隐藏编辑器的深灰覆盖层与四周遮挡侧板（**背景色保留**），两侧与播放头以下的谱面可见。 |
| **游玩区域边框** | 关闭预览蒙版播放时（或缩放不为 100% 时）用 2px 边框标出原本未被遮住、游玩时实际可见的区域；颜色取谱面主题 UI 色（Riztime 自动变色）。 |
| **预览细线** | 带预览播放时把谱面线宽缩到约 75%。 |
| **基础设置面板** | 光标工具的 `F8` 面板：突破缩放极限、预览蒙版、预览细线、面板宽度/透明度。 |

---

## 兼容性

| 项 | 值 |
| --- | --- |
| 编辑器 | Rizline Editor（Unity 2022.3.62f2，Windows x64，IL2CPP） |
| `GameAssembly.dll` SHA-256 | `F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876`（**精确匹配**，否则插件拒绝工作） |
| BepInEx | `6.0.0-be.785`（IL2CPP x64） |
| 插件 | `0.4.0`（`RhythmEditorMod.dll` + `RhythmEditorMod.Core.dll`） |
| 运行时 | .NET 6（插件）/ .NET 8（安装管理器，仅本地导出时需要） |

编辑器升级后 `GameAssembly.dll` 的哈希会变化，插件与安装管理器都会拒绝加载；此时需要重新调查
interop/metadata 与 Harmony 入口，**不能只改清单哈希强行兼容**。

---

## 获取与安装

本仓库**不发布安装包**：安装包内含 BepInEx 运行时与由游戏元数据生成的 interop/metadata，
不适合公开分发。安装方式有两种：

1. **自行构建（推荐）**：见下方「构建」。插件只需放进
   `<编辑器根目录>/BepInEx/plugins/RhythmEditorMod/`（两个 DLL）。
2. **自行导出安装包**：按 [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md) 在本地执行导出脚本，
   生成带安装管理器的 ZIP，再用管理器安装/更新/禁用/卸载。

前置条件：本机已有一份正版编辑器副本，并已在其中装好 BepInEx 6（IL2CPP）与可用的
`BepInEx/interop`。原版 `global-metadata.dat` 带自定义保护，**interop 必须在本地由 BepInEx 自行生成**，
无法从本仓库获得。

---

## 构建

需要：.NET 6 SDK（插件）、.NET 8 SDK（安装管理器）、`g++`（编译管理器原生启动器）、
PowerShell 5.1（脚本），以及一份装好 BepInEx 的本地编辑器副本。

```powershell
# 运行纯数据测试 + 编译插件 + 安装到本地 BepInEx/plugins（开发用）
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Install-RhythmEditorMod.ps1

# 导出完整安装包（含管理器与 payload；需要 g++ 与 .NET 8 SDK）
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-RhythmEditorModPackage.ps1 -Version 0.4.0
```

自动测试（纯 .NET 6、**不需要游戏**）可单独运行：

```powershell
dotnet run --project .\tests\RhythmEditorMod.Core.Tests\RhythmEditorMod.Core.Tests.csproj -c Release
```

> 注意：`RhythmEditorMod` 项目依赖游戏生成的 interop 程序集，因此**无法在没有本地编辑器副本的
> CI 环境里编译**；CI 只适合运行上面的 Core 测试。

---

## 仓库结构

```text
src/RhythmEditorMod/          插件本体（IMGUI 面板、Harmony 补丁、原生 UI 克隆等）
src/RhythmEditorMod.Core/     与游戏无关的纯逻辑（剪贴板格式、缓动拟合、缩放步进…），可单测
tests/                        纯数据测试（当前 51 条）
tools/                        构建、导出、发布与分析脚本
installer/                    安装管理器（C# 逻辑 + 原生 Windows 启动器）
baseline/                     编辑器行为与谱面格式的实验记录、运行时基线、文件哈希清单
research/                     IL2CPP 元数据与反汇编调查记录
docs/                         用户指南、变更记录、设计决定、代码地图、格式说明、技术调查
AGENTS.md                     本项目的长期工作约定
```

## 文档索引

| 文档 | 内容 |
| --- | --- |
| [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md) | 使用者手册 |
| [`docs/CHANGELOG.md`](docs/CHANGELOG.md) | 变更记录 |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | 长期设计决定（含结论与依据） |
| [`docs/CHART_FORMAT.md`](docs/CHART_FORMAT.md) | 谱面 JSON 格式（带可信度等级） |
| [`docs/CODEMAP.md`](docs/CODEMAP.md) | 代码/类/方法定位与 RVA 表 |
| [`docs/features/`](docs/features) | 各功能的深入记录（含失败记录） |
| [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md) | 导出、安装、更新与发布流程 |
| [`AGENTS.md`](AGENTS.md) | 开发约定：一次一个明确任务、先方案后实施、证据分级…… |

## 已知问题

- **缩放后 UI 点击可能偏移**：整体缩放通过改写相机投影矩阵实现，原版 UI 画布需要临时改为
  Overlay 才能保持可见，代价是命中判定偏移（缩放状态下更高概率出现）。`Ctrl+M` 复位即恢复。
- `Shift` 剪切（跨画布线的 `Ctrl+X`）目前按复制处理，不删除原线。

## 许可

本仓库中**插件自身的源码与文档**采用 [MIT 许可](LICENSE)。
仓库不包含也不授权任何游戏资源；原编辑器及其内容的权利归其权利人所有。
`BepInEx`、`HarmonyX`、`Newtonsoft.Json` 等第三方依赖遵循其各自许可（见原编辑器目录下的
`LICENSE.txt` 与各依赖项目主页）。
